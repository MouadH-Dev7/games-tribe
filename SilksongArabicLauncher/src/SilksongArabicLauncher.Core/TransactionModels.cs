using System.Text.Json.Serialization;

namespace SilksongArabicLauncher.Core;

public sealed class TransactionManifest
{
    public int SchemaVersion { get; set; } = 1;
    public string TransactionId { get; set; } = "";
    public string GameRoot { get; set; } = "";
    public string GameExecutableSha256 { get; set; } = "";
    public string State { get; set; } = "";
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string IntegritySha256 { get; set; } = "";
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? SupersedesTransactionIds { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SupersededByTransactionId { get; set; }
    public List<FileOperation> Operations { get; set; } = [];
    public List<string> CreatedDirectories { get; set; } = [];
    public List<string> ProcessedDestinations { get; set; } = [];
}

public sealed class FileOperation
{
    public string SourceRelativePath { get; set; } = "";
    public string DestinationRelativePath { get; set; } = "";
    public long SizeBytes { get; set; }
    public string InstalledSha256 { get; set; } = "";
    public bool OriginalExisted { get; set; }
    public string? OriginalSha256 { get; set; }
    public long? OriginalSizeBytes { get; set; }
    public string? BackupRelativePath { get; set; }
    public string? TemporaryRelativePath { get; set; }
}

public sealed record RecoverableTransaction(string TransactionId, string State, string Path);

public interface IGameProcessGuard
{
    bool IsRunning(string executablePath);
}

public sealed class WindowsGameProcessGuard : IGameProcessGuard
{
    public bool IsRunning(string executablePath)
    {
        var name = Path.GetFileNameWithoutExtension(executablePath);
        var processes = System.Diagnostics.Process.GetProcessesByName(name);
        try { return processes.Length > 0; }
        finally { foreach (var process in processes) process.Dispose(); }
    }
}
