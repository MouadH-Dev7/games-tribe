namespace ArabizeGamesLauncher.Models;

public enum GameDetectionStatus { NotFound, Unsupported, Detected }

public sealed record EvidenceItem(string Label, string Value, bool IsPositive = true);

public sealed class GameDetectionResult
{
    public GameDetectionStatus Status { get; init; } = GameDetectionStatus.NotFound;
    public string InstallationPath { get; init; } = string.Empty;
    public string Identity { get; init; } = string.Empty;
    public string GameVersion { get; init; } = string.Empty;
    public string BuildProduct { get; init; } = string.Empty;
    public string BuildName { get; init; } = string.Empty;
    public string BuildKey { get; init; } = string.Empty;
    public string Branch { get; init; } = string.Empty;
    public string ExecutablePath { get; init; } = string.Empty;
    public string ExecutableSha256 { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public IReadOnlyList<EvidenceItem> Evidence { get; init; } = [];
    public bool IsDetected => Status == GameDetectionStatus.Detected;
}
