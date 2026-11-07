using System.Text.Json;

namespace ArabizeGamesLauncher.Models;

public static class RegistryLanguageScope
{
    public const string Hive = "HKEY_CURRENT_USER";
    public const string SubKey = @"Software\Blizzard Entertainment\Battle.net\Launch Options\LAZR";
    public const string LocaleName = "LOCALE";
    public const string LocaleAudioName = "LOCALE_AUDIO";
    public const string ArabicLocale = "arSA";
    public const string EnglishLocale = "enUS";
}

public enum RegistryKeyReadStatus { Success, MissingKey, AccessDenied, UnexpectedError }
public enum RegistryValueReadStatus { PresentString, Missing, UnsupportedType, AccessDenied, MissingKey, UnexpectedError }
public enum RegistryLanguageClassification { ArabicPair, EnglishPair, Mixed, Missing, Unsupported, Unknown }

public sealed record RegistryValueInspection(
    string Name,
    RegistryValueReadStatus Status,
    bool Exists,
    string? Data,
    string? Kind,
    string? Error = null,
    JsonElement? RawData = null);

public sealed record RegistryLanguageInspection(
    RegistryKeyReadStatus KeyStatus,
    RegistryValueInspection Locale,
    RegistryValueInspection LocaleAudio,
    RegistryLanguageClassification Classification,
    DateTimeOffset InspectedAtUtc,
    string? Error = null);

// The JsonElement preserves type-specific data in a serializable representation.
public sealed record RegistrySnapshotValue(
    string Name,
    bool Exists,
    string? Kind,
    JsonElement? Data);

public sealed record RegistryGameIdentity(
    string GameId,
    string Product,
    string Version,
    string BuildName,
    string BuildKey);

public sealed record RegistrySnapshot(
    int SchemaVersion,
    string TransactionId,
    DateTimeOffset TimestampUtc,
    RegistryGameIdentity Game,
    string Hive,
    string SubKey,
    bool KeyExisted,
    RegistrySnapshotValue Locale,
    RegistrySnapshotValue LocaleAudio);

public sealed record RegistrySnapshotEnvelope(RegistrySnapshot Snapshot, string Sha256);
public sealed record ValidatedRegistrySnapshot(string Path, RegistrySnapshotEnvelope Envelope);

public sealed record RegistryOperationResult(string Name, bool Succeeded, string Message);
public sealed record RegistryTransactionResult(string TransactionId, IReadOnlyList<RegistryOperationResult> Operations)
{
    public bool Succeeded => Operations.Count > 0 && Operations.All(operation => operation.Succeeded);
    public bool IsPartial => Operations.Any(operation => operation.Succeeded) && Operations.Any(operation => !operation.Succeeded);
}
