namespace ArabizeGamesLauncher.Models;

public sealed class LauncherSettings
{
    public string GameDirectory { get; set; } = @"C:\Games\Call of Duty - Modern Warfare 2 CR";
    public string PackageDirectory { get; set; } = string.Empty;
    public string UiLanguage { get; set; } = "ar";
}

public sealed class InstallationTransactionRecord
{
    public string TransactionId { get; set; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string PackageId { get; set; } = string.Empty;
    public string GameId { get; set; } = "mw2-campaign-remastered-pc";
    public string State { get; set; } = "BackupPrepared";
    public List<BackupFileRecord> Files { get; set; } = [];
}

public sealed class BackupFileRecord
{
    public string Destination { get; set; } = string.Empty;
    public string BackupPath { get; set; } = string.Empty;
    public string OriginalSha256 { get; set; } = string.Empty;
    public long OriginalSizeBytes { get; set; }
}

public sealed record RestorePlanItem(string Destination, string BackupPath, string ExpectedSha256, bool IsVerified, string Message);

public sealed record ReportEntry(string Name, string Path, DateTime LastUpdated);
