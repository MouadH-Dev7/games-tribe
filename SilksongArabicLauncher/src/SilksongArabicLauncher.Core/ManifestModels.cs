using System.Text.Json.Serialization;

namespace SilksongArabicLauncher.Core;

public sealed class Phase2Manifest
{
    public int SchemaVersion { get; set; }
    public PackageIdentity PackageIdentity { get; set; } = new();
    public TargetGame TargetGame { get; set; } = new();
    public List<ManifestFile> Files { get; set; } = [];
}

public sealed class PackageIdentity
{
    public string ClaimedName { get; set; } = "";
    public string SourceDirectory { get; set; } = "";
    public string InstructionsRelativePath { get; set; } = "";
}

public sealed class TargetGame
{
    public string Title { get; set; } = "";
    public int SteamAppId { get; set; }
    public string CandidateInstallationPath { get; set; } = "";
    public string UnityEngineVersion { get; set; } = "";
    public ExecutableIdentity Executable { get; set; } = new();
}

public sealed class ExecutableIdentity
{
    public string RelativePath { get; set; } = "";
    public long SizeBytes { get; set; }
    public string FileVersion { get; set; } = "";
    public string ProductVersion { get; set; } = "";
    public string Sha256 { get; set; } = "";
}

public sealed class ManifestFile
{
    public string SourceRelativePath { get; set; } = "";
    public string? DestinationRelativePath { get; set; }
    public bool IncludedPerReadme { get; set; }
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = "";
    public string Category { get; set; } = "";
    public string FileKind { get; set; } = "";
    public bool DestinationCurrentlyExists { get; set; }
    public long? ExistingDestinationSizeBytes { get; set; }
    public string? ExistingDestinationSha256 { get; set; }
    public string ProposedAction { get; set; } = "";
    public List<string> CompatibilityAndProvenanceConcerns { get; set; } = [];
}

public sealed record ManifestIssue(string Code, string Message, string? Path = null);

public sealed class PreflightResult
{
    public required Phase2Manifest Manifest { get; init; }
    public required string ManifestPath { get; init; }
    public required string PackageRoot { get; init; }
    public required string GameRoot { get; init; }
    public required IReadOnlyList<ManifestFile> InstallFiles { get; init; }
    public required IReadOnlyList<ManifestIssue> Issues { get; init; }
    public required IReadOnlyList<ExistingDestinationConflict> ExistingFileConflicts { get; init; }
    public required long TotalBytes { get; init; }
    public bool Success => Issues.Count == 0;
    public bool CanProceedAfterBackingUpConflicts => ExistingFileConflicts.Count > 0 &&
        Issues.Count == ExistingFileConflicts.Count && Issues.All(issue => issue.Code == "unexpected-conflict");
}

public sealed record ExistingDestinationConflict(string RelativePath, long SizeBytes, string Sha256);

public sealed record OperationResult(bool Success, string Message, string? TransactionId = null,
    IReadOnlyList<string>? ManualIntervention = null)
{
    public IReadOnlyList<string> ManualFiles => ManualIntervention ?? Array.Empty<string>();
}

internal static class JsonDefaults
{
    public static readonly System.Text.Json.JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        MaxDepth = 32
    };
}
