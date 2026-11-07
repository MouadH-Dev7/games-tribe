using System.Text.Json;
using ArabizeGamesLauncher.Models;
using Microsoft.Win32;
using System.Security;

namespace ArabizeGamesLauncher.Services;

public sealed record RegistryMutationAttempt(bool Succeeded, string Message);

/// <summary>
/// Mutation port for transaction orchestration. Production writes remain behind explicit UI confirmation.
/// </summary>
public interface IRegistryMutationAdapter : IRegistryLanguageReader
{
    RegistryMutationAttempt WriteValue(string name, string kind, JsonElement data);
    RegistryMutationAttempt DeleteValue(string name);
    RegistryValueInspection ReadValue(string name);
}

/// <summary>Production adapter limited to the two values in the existing LAZR key.</summary>
public sealed class WindowsRegistryMutationAdapter : IRegistryMutationAdapter
{
    private readonly WindowsRegistryLanguageReader _reader = new();

    public RegistryLanguageReadResult ReadApprovedValues() => _reader.ReadApprovedValues();
    public RegistryValueInspection ReadValue(string name) => _reader.ReadValue(name);

    public RegistryMutationAttempt WriteValue(string name, string kind, JsonElement data)
    {
        try
        {
            WindowsRegistryLanguageReader.EnsureApprovedName(name);
            if (!Enum.TryParse<RegistryValueKind>(kind, out var registryKind) || !Enum.IsDefined(registryKind))
                return new(false, "The snapshot contains an unsupported Registry value kind.");
            var registryData = DecodeData(registryKind, data);
            using var key = Registry.CurrentUser.OpenSubKey(RegistryLanguageScope.SubKey,
                RegistryKeyPermissionCheck.ReadWriteSubTree, System.Security.AccessControl.RegistryRights.SetValue);
            if (key is null) return new(false, "The target LAZR key is missing; it was not created.");
            key.SetValue(name, registryData, registryKind);
            return new(true, "Value write completed; transaction service will independently verify it.");
        }
        catch (UnauthorizedAccessException) { return new(false, "Access to the LAZR Registry key was denied."); }
        catch (SecurityException) { return new(false, "Access to the LAZR Registry key was denied."); }
        catch (Exception) { return new(false, "The approved Registry value could not be written."); }
    }

    public RegistryMutationAttempt DeleteValue(string name)
    {
        try
        {
            WindowsRegistryLanguageReader.EnsureApprovedName(name);
            using var key = Registry.CurrentUser.OpenSubKey(RegistryLanguageScope.SubKey,
                RegistryKeyPermissionCheck.ReadWriteSubTree, System.Security.AccessControl.RegistryRights.SetValue);
            if (key is null) return new(false, "The target LAZR key is missing; it was not created.");
            key.DeleteValue(name, throwOnMissingValue: false);
            return new(true, "Value delete completed; transaction service will independently verify absence.");
        }
        catch (UnauthorizedAccessException) { return new(false, "Access to the LAZR Registry key was denied."); }
        catch (SecurityException) { return new(false, "Access to the LAZR Registry key was denied."); }
        catch (Exception) { return new(false, "The approved Registry value could not be deleted."); }
    }

    private static object DecodeData(RegistryValueKind kind, JsonElement data) => kind switch
    {
        RegistryValueKind.String or RegistryValueKind.ExpandString when data.ValueKind == JsonValueKind.String => data.GetString() ?? throw new InvalidDataException("Registry string data is null."),
        RegistryValueKind.DWord when data.TryGetInt32(out var signed) => signed,
        RegistryValueKind.DWord when data.TryGetUInt32(out var unsigned) => unchecked((int)unsigned),
        RegistryValueKind.QWord when data.TryGetInt64(out var signed) => signed,
        RegistryValueKind.QWord when data.TryGetUInt64(out var unsigned) => unchecked((long)unsigned),
        RegistryValueKind.Binary when data.ValueKind == JsonValueKind.String => Convert.FromBase64String(data.GetString() ?? string.Empty),
        RegistryValueKind.MultiString when data.ValueKind == JsonValueKind.Array => data.EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray(),
        _ => throw new InvalidDataException("Snapshot data does not match its Registry value kind.")
    };
}

public sealed class RegistryTransactionService(IRegistryMutationAdapter adapter)
{
    public RegistryTransactionResult ApplyArabicPair(string? transactionId = null) => ApplyPair(RegistryLanguageScope.ArabicLocale, transactionId);
    public RegistryTransactionResult ApplyEnglishPair(string? transactionId = null) => ApplyPair(RegistryLanguageScope.EnglishLocale, transactionId);

    public RegistryTransactionResult Restore(RegistrySnapshotEnvelope envelope, RegistryGameIdentity currentGame)
    {
        var snapshot = envelope.Snapshot;
        RegistrySnapshotStore.Validate(snapshot);
        var expectedHash = RegistrySnapshotStore.ComputeHash(snapshot);
        byte[] suppliedHash;
        try { suppliedHash = Convert.FromHexString(envelope.Sha256); }
        catch (FormatException ex) { throw new InvalidDataException("Snapshot integrity hash is malformed.", ex); }
        if (suppliedHash.Length != 32 || !System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(Convert.FromHexString(expectedHash), suppliedHash))
            throw new InvalidDataException("Snapshot integrity verification failed.");
        if (snapshot.Game != currentGame)
            throw new InvalidDataException("Snapshot game/build identity does not match the current target.");
        if (!snapshot.KeyExisted && (snapshot.Locale.Exists || snapshot.LocaleAudio.Exists))
            throw new InvalidDataException("Snapshot key/value existence state is inconsistent.");

        if (!snapshot.KeyExisted)
        {
            var current = adapter.ReadApprovedValues();
            if (current.KeyStatus == RegistryKeyReadStatus.MissingKey)
                return new(snapshot.TransactionId,
                [new(RegistryLanguageScope.LocaleName, true, "Original key and value remain absent; verified."),
                 new(RegistryLanguageScope.LocaleAudioName, true, "Original key and value remain absent; verified.")]);
            if (current.KeyStatus != RegistryKeyReadStatus.Success)
                return new(snapshot.TransactionId,
                [new(RegistryLanguageScope.LocaleName, false, "Could not verify the originally absent key."),
                 new(RegistryLanguageScope.LocaleAudioName, false, "Could not verify the originally absent key.")]);
        }
        var operations = new List<RegistryOperationResult> { RestoreValue(snapshot.Locale), RestoreValue(snapshot.LocaleAudio) };
        return new(snapshot.TransactionId, operations);
    }

    private RegistryTransactionResult ApplyPair(string locale, string? transactionId)
    {
        if (locale is not (RegistryLanguageScope.ArabicLocale or RegistryLanguageScope.EnglishLocale))
            throw new ArgumentOutOfRangeException(nameof(locale), "Only the verified Arabic and English locale pairs are supported.");
        var id = transactionId ?? Guid.NewGuid().ToString("N");
        var data = JsonSerializer.SerializeToElement(locale);
        var first = WriteAndVerify(RegistryLanguageScope.LocaleName, "String", data);
        var second = WriteAndVerify(RegistryLanguageScope.LocaleAudioName, "String", data);
        return new(id, [first, second]);
    }

    private RegistryOperationResult RestoreValue(RegistrySnapshotValue value)
    {
        if (!value.Exists)
        {
            RegistryMutationAttempt deletion;
            try { deletion = adapter.DeleteValue(value.Name); }
            catch (Exception ex) { return new(value.Name, false, "Delete attempt failed: " + ex.Message); }
            try
            {
                var actual = adapter.ReadValue(value.Name);
                if (deletion.Succeeded && IsAbsent(actual)) return new(value.Name, true, "Originally absent; absence verified.");
                return new(value.Name, false, deletion.Succeeded ? "Delete was not verified." : "Delete attempt failed and absence was not verified: " + deletion.Message);
            }
            catch (Exception ex) { return new(value.Name, false, "Delete verification failed: " + ex.Message); }
        }
        return WriteAndVerify(value.Name, value.Kind!, value.Data!.Value);
    }

    private RegistryOperationResult WriteAndVerify(string name, string kind, JsonElement data)
    {
        RegistryMutationAttempt attempt;
        try { attempt = adapter.WriteValue(name, kind, data); }
        catch (Exception ex) { attempt = new(false, ex.Message); }
        try
        {
            var actual = adapter.ReadValue(name);
            var verified = actual.Exists && string.Equals(actual.Kind, kind, StringComparison.Ordinal) &&
                           actual.RawData is { } raw && string.Equals(raw.GetRawText(), data.GetRawText(), StringComparison.Ordinal);
            return attempt.Succeeded && verified
                ? new(name, true, "Write completed and independently verified.")
                : new(name, false, (attempt.Succeeded ? "Write completed" : "Write attempt failed: " + attempt.Message) + (verified ? "; read-back matches but operation is still treated as failed." : "; independent read-back did not match."));
        }
        catch (Exception ex) { return new(name, false, "Write verification failed: " + ex.Message); }
    }

    private static bool IsAbsent(RegistryValueInspection value) =>
        !value.Exists && value.Status is RegistryValueReadStatus.Missing or RegistryValueReadStatus.MissingKey;
}
