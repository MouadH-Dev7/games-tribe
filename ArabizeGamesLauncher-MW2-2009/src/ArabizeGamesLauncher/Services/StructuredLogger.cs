using System.Text.Json;

namespace ArabizeGamesLauncher.Services;

public sealed class StructuredLogger(AppPaths paths)
{
    private readonly object _gate = new();
    private readonly JsonSerializerOptions _options = new() { WriteIndented = false };

    public string LogFile => Path.Combine(paths.Logs, $"launcher-{DateTime.Now:yyyy-MM-dd}.jsonl");

    public void Write(string level, string category, string message, Exception? exception = null)
    {
        var entry = new LogEntry(DateTimeOffset.Now, level, category, message, exception?.ToString());
        lock (_gate)
            File.AppendAllText(LogFile, JsonSerializer.Serialize(entry, _options) + Environment.NewLine);
    }

    public IReadOnlyList<LogEntry> ReadRecent(int limit = 200)
    {
        if (!Directory.Exists(paths.Logs)) return [];
        return Directory.EnumerateFiles(paths.Logs, "*.jsonl")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .SelectMany(file => File.ReadLines(file).Reverse())
            .Take(limit)
            .Select(line => { try { return JsonSerializer.Deserialize<LogEntry>(line); } catch { return null; } })
            .Where(entry => entry is not null)
            .Cast<LogEntry>()
            .ToArray();
    }
}

public sealed record LogEntry(DateTimeOffset Timestamp, string Level, string Category, string Message, string? Exception);
