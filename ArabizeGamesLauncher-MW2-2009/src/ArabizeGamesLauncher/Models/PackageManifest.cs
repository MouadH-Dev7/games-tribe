using System.Text.Json.Serialization;

namespace ArabizeGamesLauncher.Models;

public sealed class PackageManifest
{
    [JsonPropertyName("packageId")] public string PackageId { get; set; } = string.Empty;
    [JsonPropertyName("gameId")] public string GameId { get; set; } = string.Empty;
    [JsonPropertyName("supportedEdition")] public string SupportedEdition { get; set; } = string.Empty;
    [JsonPropertyName("supportedBuilds")] public List<string> SupportedBuilds { get; set; } = [];
    [JsonPropertyName("packageVersion")] public string PackageVersion { get; set; } = string.Empty;
    [JsonPropertyName("language")] public string Language { get; set; } = string.Empty;
    [JsonPropertyName("installationMethod")] public string InstallationMethod { get; set; } = string.Empty;
    [JsonPropertyName("compatibilityNotes")] public string CompatibilityNotes { get; set; } = string.Empty;
    [JsonPropertyName("installationInstructions")] public string InstallationInstructions { get; set; } = string.Empty;
    [JsonPropertyName("files")] public List<PackageFileEntry> Files { get; set; } = [];
}

public sealed class PackageFileEntry
{
    [JsonPropertyName("source")] public string Source { get; set; } = string.Empty;
    [JsonPropertyName("destination")] public string Destination { get; set; } = string.Empty;
    [JsonPropertyName("sha256")] public string Sha256 { get; set; } = string.Empty;
}

public enum PackageStatus { NotAvailable, Imported, Validated, Incompatible, ReadyForInstallation, Installed }

public sealed class PackageEntry
{
    public string PackageDirectory { get; init; } = string.Empty;
    public string PackageId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string Language { get; init; } = string.Empty;
    public PackageStatus Status { get; init; }
    public string StatusMessage { get; init; } = string.Empty;
    public string CompatibilityNotes { get; init; } = string.Empty;
    public string InstallationMethod { get; init; } = string.Empty;
    public int FileCount { get; init; }
}
