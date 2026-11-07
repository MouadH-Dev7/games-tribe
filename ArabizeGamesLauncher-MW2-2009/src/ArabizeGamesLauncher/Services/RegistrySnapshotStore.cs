using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArabizeGamesLauncher.Models;

namespace ArabizeGamesLauncher.Services;

public static class RegistrySnapshotFactory
{
    public const int CurrentSchemaVersion = 1;

    public static RegistrySnapshot Create(RegistryLanguageInspection inspection, RegistryGameIdentity game)
    {
        if (inspection.KeyStatus is RegistryKeyReadStatus.AccessDenied or RegistryKeyReadStatus.UnexpectedError)
            throw new InvalidDataException("Cannot snapshot Registry state after an unsuccessful inspection.");
        var keyExisted = inspection.KeyStatus == RegistryKeyReadStatus.Success;
        return new(CurrentSchemaVersion, Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow, game,
            RegistryLanguageScope.Hive, RegistryLanguageScope.SubKey, keyExisted,
            CreateValue(inspection.Locale, RegistryLanguageScope.LocaleName, keyExisted),
            CreateValue(inspection.LocaleAudio, RegistryLanguageScope.LocaleAudioName, keyExisted));
    }

    private static RegistrySnapshotValue CreateValue(RegistryValueInspection value, string expectedName, bool keyExisted)
    {
        if (!value.Name.Equals(expectedName, StringComparison.Ordinal) || value.Status is RegistryValueReadStatus.AccessDenied or RegistryValueReadStatus.UnexpectedError ||
            (keyExisted && value.Status == RegistryValueReadStatus.MissingKey) || (!keyExisted && value.Status != RegistryValueReadStatus.MissingKey))
            throw new InvalidDataException($"Cannot snapshot '{expectedName}' because its state is not established.");
        if (!value.Exists) return new(expectedName, false, null, null);
        if (value.Kind is null || value.RawData is null)
            throw new InvalidDataException($"Cannot snapshot '{expectedName}' because its type or data is unavailable.");
        return new(expectedName, true, value.Kind, value.RawData);
    }
}

/// <summary>Stores integrity-checked snapshots under the application data root using create-new semantics.</summary>
public interface IRegistrySnapshotStore
{
    string CreateNew(RegistrySnapshot snapshot, string gameDirectory);
    RegistrySnapshotEnvelope LoadAndVerify(string path);
    IReadOnlyList<ValidatedRegistrySnapshot> ListValidSnapshots(RegistryGameIdentity currentGame);
}

public sealed class RegistrySnapshotStore(AppPaths paths) : IRegistrySnapshotStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    public string BackupDirectory => Path.Combine(paths.Root, "registry-backups");

    public string CreateNew(RegistrySnapshot snapshot, string gameDirectory)
    {
        Validate(snapshot);
        var appRoot = Path.GetFullPath(paths.Root);
        var backupRoot = Path.GetFullPath(BackupDirectory);
        var gameRoot = Path.GetFullPath(gameDirectory);
        if (!IsWithin(backupRoot, appRoot) || IsWithin(backupRoot, gameRoot))
            throw new InvalidOperationException("Registry snapshots must be stored under app data and outside the game directory.");

        Directory.CreateDirectory(backupRoot);
        EnsureNotReparsePoint(backupRoot, directory: true);
        var target = Path.Combine(backupRoot, snapshot.TransactionId + ".json");
        var temporary = Path.Combine(backupRoot, "." + snapshot.TransactionId + "." + Guid.NewGuid().ToString("N") + ".tmp");
        var envelope = new RegistrySnapshotEnvelope(snapshot, ComputeHash(snapshot));
        var bytes = JsonSerializer.SerializeToUtf8Bytes(envelope, JsonOptions);
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, target, overwrite: false);
            _ = LoadAndVerify(target);
            return target;
        }
        catch
        {
            if (File.Exists(temporary)) File.Delete(temporary);
            throw;
        }
    }

    public RegistrySnapshotEnvelope LoadAndVerify(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (!IsWithin(fullPath, Path.GetFullPath(BackupDirectory)))
            throw new InvalidDataException("Snapshot path is outside the registry-backups directory.");
        EnsureNotReparsePoint(Path.GetFullPath(BackupDirectory), directory: true);
        EnsureNotReparsePoint(fullPath, directory: false);
        RegistrySnapshotEnvelope envelope;
        try
        {
            if (new FileInfo(fullPath).Length > 1024 * 1024) throw new InvalidDataException("Snapshot exceeds the 1 MiB size limit.");
            using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            envelope = JsonSerializer.Deserialize<RegistrySnapshotEnvelope>(stream, JsonOptions)
                ?? throw new InvalidDataException("Snapshot is empty.");
        }
        catch (JsonException ex) { throw new InvalidDataException("Snapshot JSON is invalid.", ex); }
        if (envelope.Snapshot is null || string.IsNullOrWhiteSpace(envelope.Sha256)) throw new InvalidDataException("Snapshot envelope is incomplete.");
        Validate(envelope.Snapshot);
        var expected = ComputeHash(envelope.Snapshot);
        byte[] actualHash;
        try { actualHash = Convert.FromHexString(envelope.Sha256); }
        catch (FormatException ex) { throw new InvalidDataException("Snapshot SHA-256 field is malformed.", ex); }
        if (actualHash.Length != 32 || !CryptographicOperations.FixedTimeEquals(Convert.FromHexString(expected), actualHash))
            throw new InvalidDataException("Snapshot SHA-256 integrity check failed.");
        return envelope;
    }

    public IReadOnlyList<ValidatedRegistrySnapshot> ListValidSnapshots(RegistryGameIdentity currentGame)
    {
        var directory = Path.GetFullPath(BackupDirectory);
        if (!Directory.Exists(directory)) return [];
        EnsureNotReparsePoint(directory, directory: true);
        var result = new List<ValidatedRegistrySnapshot>();
        foreach (var path in Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                EnsureNotReparsePoint(path, directory: false);
                var envelope = LoadAndVerify(path);
                if (envelope.Snapshot.Game != currentGame) continue;
                if (!Path.GetFileName(path).Equals(envelope.Snapshot.TransactionId + ".json", StringComparison.OrdinalIgnoreCase)) continue;
                result.Add(new(path, envelope));
            }
            catch (Exception) { /* Invalid files are excluded from restorable choices. */ }
        }
        return result.OrderByDescending(item => item.Envelope.Snapshot.TimestampUtc).ToArray();
    }

    public static string ComputeHash(RegistrySnapshot snapshot) => Convert.ToHexString(SHA256.HashData(SerializeCanonical(snapshot)));

    public static byte[] SerializeCanonical(RegistrySnapshot snapshot) => JsonSerializer.SerializeToUtf8Bytes(snapshot, JsonOptions);

    public static void Validate(RegistrySnapshot snapshot)
    {
        if (snapshot.SchemaVersion != RegistrySnapshotFactory.CurrentSchemaVersion) throw new InvalidDataException("Unsupported snapshot schema version.");
        if (!Guid.TryParseExact(snapshot.TransactionId, "N", out _)) throw new InvalidDataException("Snapshot transaction ID is invalid.");
        if (snapshot.TimestampUtc.Offset != TimeSpan.Zero) throw new InvalidDataException("Snapshot timestamp must be UTC.");
        if (snapshot.Hive != RegistryLanguageScope.Hive || snapshot.SubKey != RegistryLanguageScope.SubKey) throw new InvalidDataException("Snapshot Registry scope is not approved.");
        if (snapshot.Game is null || string.IsNullOrWhiteSpace(snapshot.Game.GameId) || string.IsNullOrWhiteSpace(snapshot.Game.Product) ||
            string.IsNullOrWhiteSpace(snapshot.Game.Version) || string.IsNullOrWhiteSpace(snapshot.Game.BuildName) || string.IsNullOrWhiteSpace(snapshot.Game.BuildKey))
            throw new InvalidDataException("Snapshot game identity is incomplete.");
        if (snapshot.Game.GameId != GameDetectionService.GameId || snapshot.Game.Product != GameDetectionService.ExpectedBuildProduct ||
            snapshot.Game.Version != GameDetectionService.ExpectedVersion || snapshot.Game.BuildName != GameDetectionService.ExpectedBuildName ||
            !System.Text.RegularExpressions.Regex.IsMatch(snapshot.Game.BuildKey, "^[0-9a-fA-F]{32}$"))
            throw new InvalidDataException("Snapshot game identity does not match the supported exact build.");
        ValidateValue(snapshot.Locale, RegistryLanguageScope.LocaleName);
        ValidateValue(snapshot.LocaleAudio, RegistryLanguageScope.LocaleAudioName);
        if (!snapshot.KeyExisted && (snapshot.Locale.Exists || snapshot.LocaleAudio.Exists))
            throw new InvalidDataException("A missing Registry key cannot contain existing values.");
    }

    private static void ValidateValue(RegistrySnapshotValue value, string expectedName)
    {
        if (value is null || value.Name != expectedName) throw new InvalidDataException("Snapshot contains a missing or unapproved value name.");
        if (value.Exists)
        {
            if (string.IsNullOrWhiteSpace(value.Kind) || !Enum.TryParse<Microsoft.Win32.RegistryValueKind>(value.Kind, out var kind) || !Enum.IsDefined(kind) || value.Data is null)
                throw new InvalidDataException($"Snapshot value '{expectedName}' is missing a supported kind or data.");
            if (!KindDataMatches(value.Kind, value.Data.Value))
                throw new InvalidDataException($"Snapshot value '{expectedName}' data does not match its Registry kind.");
        }
        if (!value.Exists && (value.Kind is not null || value.Data is not null)) throw new InvalidDataException($"Absent snapshot value '{expectedName}' must not have kind or data.");

        static bool KindDataMatches(string kind, JsonElement data) => kind switch
        {
            "String" or "ExpandString" => data.ValueKind == JsonValueKind.String,
            "DWord" => data.ValueKind == JsonValueKind.Number && (data.TryGetInt32(out _) || data.TryGetUInt32(out _)),
            "QWord" => data.ValueKind == JsonValueKind.Number && (data.TryGetInt64(out _) || data.TryGetUInt64(out _)),
            "Binary" => data.ValueKind == JsonValueKind.String && IsBase64(data.GetString()),
            "MultiString" => data.ValueKind == JsonValueKind.Array && data.EnumerateArray().All(item => item.ValueKind == JsonValueKind.String),
            _ => false
        };

        static bool IsBase64(string? text)
        {
            if (text is null) return false;
            try { _ = Convert.FromBase64String(text); return true; } catch (FormatException) { return false; }
        }
    }

    private static bool IsWithin(string candidate, string root)
    {
        var fullCandidate = Path.GetFullPath(candidate).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return fullCandidate.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsureNotReparsePoint(string path, bool directory)
    {
        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException(directory
                ? "The registry-backups directory must not be a reparse point."
                : "Registry snapshot files must not be reparse points.");
    }
}
