using System.Security.Cryptography;
using System.Text.Json;
using ArabizeGamesLauncher.Models;

namespace ArabizeGamesLauncher.Services;

public sealed class BackupManager
{
    public async Task<BackupFileRecord> CreateVerifiedBackupAsync(string sourceFile, string backupRoot, string relativeDestination, string transactionId, CancellationToken cancellationToken = default)
    {
        if (!PackageService.IsSafeRelativePath(relativeDestination)) throw new InvalidDataException("The destination must be a safe relative path.");
        if (!File.Exists(sourceFile)) throw new FileNotFoundException("The original file is missing.", sourceFile);
        var backup = Path.GetFullPath(Path.Combine(backupRoot, transactionId, relativeDestination));
        var transactionDirectory = Path.GetFullPath(Path.Combine(backupRoot, transactionId));
        if (!backup.StartsWith(transactionDirectory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The backup path escapes its transaction folder.");
        Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
        var originalHash = await HashAsync(sourceFile, cancellationToken);
        await using (var input = File.OpenRead(sourceFile))
        await using (var output = new FileStream(backup, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            await input.CopyToAsync(output, cancellationToken);
        var backupHash = await HashAsync(backup, cancellationToken);
        if (!backupHash.Equals(originalHash, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(backup);
            throw new IOException("Backup integrity verification failed.");
        }
        var record = new BackupFileRecord { Destination = relativeDestination.Replace('\\', '/'), BackupPath = backup, OriginalSha256 = originalHash, OriginalSizeBytes = new FileInfo(sourceFile).Length };
        var manifestPath = Path.Combine(transactionDirectory, "backup-manifest.json");
        var records = File.Exists(manifestPath) ? JsonSerializer.Deserialize<List<BackupFileRecord>>(await File.ReadAllTextAsync(manifestPath, cancellationToken)) ?? [] : [];
        records.Add(record);
        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
        return record;
    }

    public async Task<IReadOnlyList<RestorePlanItem>> PlanRestoreAsync(string transactionDirectory, CancellationToken cancellationToken = default)
    {
        var manifest = Path.Combine(transactionDirectory, "backup-manifest.json");
        if (!File.Exists(manifest)) throw new FileNotFoundException("Backup manifest is missing.", manifest);
        var records = JsonSerializer.Deserialize<List<BackupFileRecord>>(await File.ReadAllTextAsync(manifest, cancellationToken)) ?? [];
        var plan = new List<RestorePlanItem>();
        foreach (var record in records)
        {
            if (!PackageService.IsSafeRelativePath(record.Destination))
                throw new InvalidDataException("Backup manifest contains an unsafe destination.");
            var inTransaction = Path.GetFullPath(record.BackupPath).StartsWith(Path.GetFullPath(transactionDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
            var exists = inTransaction && File.Exists(record.BackupPath);
            var verified = exists && (await HashAsync(record.BackupPath, cancellationToken)).Equals(record.OriginalSha256, StringComparison.OrdinalIgnoreCase);
            plan.Add(new(record.Destination, record.BackupPath, record.OriginalSha256, verified, verified ? "Backup hash verified." : "Backup is missing, outside this transaction, or has an invalid hash."));
        }
        return plan;
    }

    public static async Task<string> HashAsync(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }
}
