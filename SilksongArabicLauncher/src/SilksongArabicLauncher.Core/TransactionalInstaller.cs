using System.Security.Cryptography;
using System.Text.Json;

namespace SilksongArabicLauncher.Core;

public sealed class TransactionalInstaller
{
    private readonly ManifestValidator _validator;
    private readonly IGameProcessGuard _processGuard;
    private readonly string _dataRoot;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public TransactionalInstaller(string dataRoot, IGameProcessGuard? processGuard = null, ManifestValidator? validator = null)
    {
        _dataRoot = Path.GetFullPath(dataRoot);
        _processGuard = processGuard ?? new WindowsGameProcessGuard();
        _validator = validator ?? new ManifestValidator();
    }

    public string DataRoot => _dataRoot;

    public PreflightResult Inspect(string manifestPath, string packageRoot, string gameRoot) =>
        _validator.Validate(manifestPath, packageRoot, gameRoot);

    public OperationResult Install(string manifestPath, string packageRoot, string gameRoot,
        bool confirmUnsignedRuntime, Action<int, string>? afterCommitForTesting = null,
        bool confirmExistingFilesBackup = false)
    {
        _gate.Wait();
        try
        {
            var preflight = _validator.Validate(manifestPath, packageRoot, gameRoot);
            if (!preflight.Success && !(confirmExistingFilesBackup && preflight.CanProceedAfterBackingUpConflicts))
                return new(false, FormatIssues(preflight.Issues));
            if (preflight.ExistingFileConflicts.Count > 0 && !confirmExistingFilesBackup)
                return new(false, "Existing package-path files require explicit confirmation and a verified backup.", ManualIntervention: preflight.ExistingFileConflicts.Select(conflict => conflict.RelativePath).ToArray());
            if (!confirmUnsignedRuntime)
                return new(false, "Explicit confirmation is required before installing the unsigned runtime components.");
            var executable = SafePaths.UnderRoot(preflight.GameRoot, preflight.Manifest.TargetGame.Executable.RelativePath);
            if (_processGuard.IsRunning(executable)) return new(false, "Close Hollow Knight: Silksong before installing.");
            if (HasInstalledTransaction(preflight.GameRoot)) return new(false, "An active launcher transaction already exists for this game directory.");
            var recoverable = FindRecoverable(preflight.GameRoot);
            var supersededIds = confirmExistingFilesBackup
                ? GetSupersedableRecoverableTransactionIds(preflight.GameRoot, preflight.ExistingFileConflicts)
                : [];
            if (recoverable.Count > 0 && supersededIds.Count != recoverable.Count)
                return new(false, "An interrupted transaction must be recovered before installing.");
            var finalPreflight = _validator.Validate(manifestPath, packageRoot, gameRoot);
            if ((!finalPreflight.Success && !(confirmExistingFilesBackup && finalPreflight.CanProceedAfterBackingUpConflicts)) ||
                !SameConflictSnapshot(preflight.ExistingFileConflicts, finalPreflight.ExistingFileConflicts))
                return new(false, "Files or destinations changed during preflight. " + FormatIssues(finalPreflight.Issues));

            var expectedBackupBytes = preflight.InstallFiles
                .Where(f => f.ProposedAction.Equals("REPLACE", StringComparison.OrdinalIgnoreCase))
                .Sum(f => new FileInfo(SafePaths.UnderRoot(preflight.GameRoot, f.DestinationRelativePath!)).Length)
                + preflight.ExistingFileConflicts.Sum(conflict => conflict.SizeBytes);
            CheckDiskSpace(preflight.TotalBytes, expectedBackupBytes, preflight.GameRoot, _dataRoot);

            var transactionId = Guid.NewGuid().ToString("N");
            var paths = GetTransactionPaths(transactionId);
            var operations = new List<FileOperation>();
            var createdDirectories = GetNewDestinationDirectories(preflight);
            EnsureManagedRoots();
            Directory.CreateDirectory(paths.StageDirectory);
            Directory.CreateDirectory(paths.BackupDirectory);
            SafePaths.RejectIfReparse(paths.StageDirectory);
            SafePaths.RejectIfReparse(paths.BackupDirectory);

            try
            {
                foreach (var item in preflight.InstallFiles)
                {
                    var destination = SafePaths.UnderRoot(preflight.GameRoot, item.DestinationRelativePath!);
                    EnsureNoReparseParents(preflight.GameRoot, destination);
                    var originalExists = File.Exists(destination);
                    string? originalHash = null;
                    long? originalSize = null;
                    string? backupRelative = null;
                    if (originalExists)
                    {
                        originalHash = ManifestValidator.HashFile(destination);
                        var expectedCurrentHash = item.ProposedAction.Equals("REPLACE", StringComparison.OrdinalIgnoreCase)
                            ? item.ExistingDestinationSha256
                            : preflight.ExistingFileConflicts.FirstOrDefault(conflict =>
                                SafePaths.NormalizeRelative(conflict.RelativePath).Equals(SafePaths.NormalizeRelative(item.DestinationRelativePath), StringComparison.OrdinalIgnoreCase))?.Sha256;
                        if (expectedCurrentHash is null ||
                            (item.ProposedAction.Equals("ADD", StringComparison.OrdinalIgnoreCase) && !confirmExistingFilesBackup) ||
                            !originalHash.Equals(expectedCurrentHash, StringComparison.OrdinalIgnoreCase))
                            throw new IOException($"Destination changed before backup: {item.DestinationRelativePath}");
                        originalSize = new FileInfo(destination).Length;
                        backupRelative = item.DestinationRelativePath;
                    }
                    else if (File.Exists(destination) || Directory.Exists(destination))
                        throw new IOException($"Unexpected destination appeared before staging: {item.DestinationRelativePath}");

                    operations.Add(new FileOperation
                    {
                        SourceRelativePath = item.SourceRelativePath,
                        DestinationRelativePath = item.DestinationRelativePath!,
                        SizeBytes = item.SizeBytes,
                        InstalledSha256 = item.Sha256,
                        OriginalExisted = originalExists,
                        OriginalSha256 = originalHash,
                        OriginalSizeBytes = originalSize,
                        BackupRelativePath = backupRelative,
                        TemporaryRelativePath = item.DestinationRelativePath + "." + transactionId + ".slar-tmp"
                    });
                }

                var journal = new TransactionManifest
                {
                    TransactionId = transactionId,
                    GameRoot = preflight.GameRoot,
                    GameExecutableSha256 = preflight.Manifest.TargetGame.Executable.Sha256,
                    State = "Prepared",
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    SupersedesTransactionIds = supersededIds.ToList(),
                    Operations = operations,
                    CreatedDirectories = createdDirectories
                };
                journal.State = "Staging";
                WriteSealed(paths.JournalPath, journal);
                foreach (var operation in operations)
                {
                    var source = SafePaths.UnderRoot(preflight.PackageRoot, operation.SourceRelativePath);
                    SafePaths.RejectReparsePoints(preflight.PackageRoot, source, includeLeaf: true);
                    var staged = SafePaths.UnderRoot(paths.StageDirectory, operation.SourceRelativePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(staged)!);
                    File.Copy(source, staged, overwrite: false);
                    VerifyFile(staged, operation.InstalledSha256, operation.SizeBytes, "Staged file verification failed");
                    if (operation.OriginalExisted)
                    {
                        var destination = SafePaths.UnderRoot(preflight.GameRoot, operation.DestinationRelativePath);
                        if (!File.Exists(destination) || !ManifestValidator.HashFile(destination).Equals(operation.OriginalSha256, StringComparison.OrdinalIgnoreCase))
                            throw new IOException($"Original file changed before backup: {operation.DestinationRelativePath}");
                        var backup = SafePaths.UnderRoot(paths.BackupDirectory, operation.BackupRelativePath!);
                        Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
                        File.Copy(destination, backup, overwrite: false);
                        VerifyFile(backup, operation.OriginalSha256!, operation.OriginalSizeBytes!.Value, "Original-file backup verification failed");
                    }
                }
                journal.State = "Prepared";
                WriteSealed(paths.JournalPath, journal);
                journal.State = "Applying";
                WriteSealed(paths.JournalPath, journal);

                var committed = 0;
                foreach (var operation in operations)
                {
                    var target = SafePaths.UnderRoot(preflight.GameRoot, operation.DestinationRelativePath);
                    var temp = SafePaths.UnderRoot(preflight.GameRoot, operation.TemporaryRelativePath!);
                    EnsureNoReparseParents(preflight.GameRoot, target);
                    EnsureCreatedDirectories(preflight.GameRoot, operation.DestinationRelativePath);
                    if (operation.OriginalExisted)
                    {
                        if (!File.Exists(target) || !ManifestValidator.HashFile(target).Equals(operation.OriginalSha256, StringComparison.OrdinalIgnoreCase))
                            throw new IOException($"Replacement destination changed during commit: {operation.DestinationRelativePath}");
                    }
                    else if (File.Exists(target) || Directory.Exists(target))
                        throw new IOException($"Unexpected file appeared during commit: {operation.DestinationRelativePath}");

                    using (var input = new FileStream(SafePaths.UnderRoot(paths.StageDirectory, operation.SourceRelativePath), FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 128 * 1024, FileOptions.WriteThrough))
                    {
                        input.CopyTo(output);
                        output.Flush(flushToDisk: true);
                    }
                    VerifyFile(temp, operation.InstalledSha256, operation.SizeBytes, "Commit staging verification failed");
                    File.Move(temp, target, overwrite: operation.OriginalExisted);
                    VerifyFile(target, operation.InstalledSha256, operation.SizeBytes, "Installed-file verification failed");
                    committed++;
                    journal.ProcessedDestinations.Add(operation.DestinationRelativePath);
                    WriteSealed(paths.JournalPath, journal);
                    afterCommitForTesting?.Invoke(committed, operation.DestinationRelativePath);
                }

                foreach (var operation in operations)
                    VerifyFile(SafePaths.UnderRoot(preflight.GameRoot, operation.DestinationRelativePath), operation.InstalledSha256, operation.SizeBytes, "Final installation verification failed");

                journal.State = "Installed";
                WriteSealed(paths.TransactionManifestPath, journal);
                journal.State = "Completed";
                WriteSealed(paths.JournalPath, journal);
                MarkSupersededTransactions(supersededIds, transactionId);
                TryDeleteDirectory(paths.StageDirectory);
                return new(true, "تم تثبيت التعريب بنجاح. اختر English داخل اللعبة لاستخدام الترجمة.", transactionId);
            }
            catch (Exception installError)
            {
                var journalPath = paths.JournalPath;
                if (File.Exists(journalPath))
                {
                    try
                    {
                        var journal = ReadSealed(journalPath);
                        var manual = RollbackOperations(journal, paths.BackupDirectory, preflight.GameRoot);
                        journal.State = manual.Count == 0 ? "RolledBack" : "NeedsManualRecovery";
                        WriteSealed(journalPath, journal);
                        MarkFinalTransaction(paths, journal.State, manual);
                        RemoveCreatedDirectories(preflight.GameRoot, journal.CreatedDirectories);
                        TryDeleteDirectory(paths.StageDirectory);
                        return new(false, "Installation failed and rollback " + (manual.Count == 0 ? "completed." : "needs manual recovery."), transactionId, manual);
                    }
                    catch (Exception rollbackError)
                    {
                        return new(false, $"Installation failed: {installError.Message}. Rollback failed: {rollbackError.Message}", transactionId, [journalPath]);
                    }
                }
                TryDeleteDirectory(paths.StageDirectory);
                if (!File.Exists(journalPath)) TryDeleteDirectory(paths.BackupDirectory);
                return new(false, "Installation failed before commit: " + installError.Message, transactionId);
            }
        }
        finally { _gate.Release(); }
    }

    public IReadOnlyList<RecoverableTransaction> FindRecoverable(string gameRoot)
    {
        EnsureManagedRoots();
        var fullGame = Path.GetFullPath(gameRoot);
        var result = new List<RecoverableTransaction>();
        var journalDir = Path.Combine(_dataRoot, "journals");
        if (Directory.Exists(journalDir))
        {
            foreach (var file in Directory.EnumerateFiles(journalDir, "*.pending.json", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    var tx = ReadSealed(file);
                    if (SamePath(tx.GameRoot, fullGame) && tx.State is "Staging" or "Prepared" or "Applying" or "NeedsManualRecovery")
                        result.Add(new(tx.TransactionId, tx.State, file));
                }
                catch { result.Add(new(Path.GetFileNameWithoutExtension(file), "InvalidJournal", file)); }
            }
        }
        var txDir = Path.Combine(_dataRoot, "transactions");
        if (Directory.Exists(txDir))
        {
            foreach (var file in Directory.EnumerateFiles(txDir, "*.json", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    var tx = ReadSealed(file);
                    if (SamePath(tx.GameRoot, fullGame) && tx.State is "Uninstalling" or "NeedsManualRecovery")
                        result.Add(new(tx.TransactionId, tx.State, file));
                }
                catch { result.Add(new(Path.GetFileNameWithoutExtension(file), "InvalidTransaction", file)); }
            }
        }
        return result;
    }

    public IReadOnlyList<string> GetSupersedableRecoverableTransactionIds(string gameRoot,
        IReadOnlyList<ExistingDestinationConflict> conflicts)
    {
        if (conflicts.Count == 0) return [];
        var candidates = FindRecoverable(gameRoot);
        if (candidates.Count != 1 || candidates[0].State != "NeedsManualRecovery" ||
            candidates[0].Path.EndsWith(".pending.json", StringComparison.OrdinalIgnoreCase)) return [];
        try
        {
            var tx = ReadSealed(candidates[0].Path);
            if (tx.State != "NeedsManualRecovery" || !SamePath(tx.GameRoot, Path.GetFullPath(gameRoot))) return [];
            var conflictMap = conflicts.ToDictionary(conflict => SafePaths.NormalizeRelative(conflict.RelativePath), StringComparer.OrdinalIgnoreCase);
            var ownedResiduals = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var operation in tx.Operations)
            {
                var target = SafePaths.UnderRoot(gameRoot, operation.DestinationRelativePath);
                EnsureNoReparseParents(gameRoot, target);
                if (operation.OriginalExisted)
                {
                    if (!File.Exists(target) || !ManifestValidator.HashFile(target).Equals(operation.OriginalSha256, StringComparison.OrdinalIgnoreCase)) return [];
                    continue;
                }
                if (Directory.Exists(target)) return [];
                if (!File.Exists(target)) continue;
                var normalizedDestination = SafePaths.NormalizeRelative(operation.DestinationRelativePath);
                if (!conflictMap.TryGetValue(normalizedDestination, out var conflict)) return [];
                var info = new FileInfo(target);
                if (info.Length != conflict.SizeBytes || !ManifestValidator.HashFile(target).Equals(conflict.Sha256, StringComparison.OrdinalIgnoreCase)) return [];
                ownedResiduals.Add(normalizedDestination);
            }
            return ownedResiduals.Count > 0 ? [tx.TransactionId] : [];
        }
        catch { return []; }
    }

    public OperationResult RecoverInterrupted(string gameRoot)
    {
        _gate.Wait();
        try
        {
            var executable = Path.Combine(Path.GetFullPath(gameRoot), "Hollow Knight Silksong.exe");
            if (_processGuard.IsRunning(executable)) return new(false, "Close Hollow Knight: Silksong before recovering a transaction.");
            var candidates = FindRecoverable(gameRoot);
            if (candidates.Count == 0) return new(true, "No interrupted transaction needs recovery.");
            var manual = new List<string>();
            foreach (var candidate in candidates)
            {
                if (candidate.State.StartsWith("Invalid", StringComparison.OrdinalIgnoreCase)) { manual.Add(candidate.Path); continue; }
                var tx = ReadSealed(candidate.Path);
                var backupDir = GetTransactionPaths(tx.TransactionId).BackupDirectory;
                var localManual = new List<string>();
                if (!candidate.Path.EndsWith(".pending.json", StringComparison.OrdinalIgnoreCase))
                {
                    var result = UninstallCore(tx, candidate.Path, backupDir, gameRoot);
                    localManual.AddRange(result.ManualFiles);
                }
                else
                {
                    localManual.AddRange(RollbackOperations(tx, backupDir, Path.GetFullPath(gameRoot)));
                    tx.State = localManual.Count == 0 ? "RolledBack" : "NeedsManualRecovery";
                    WriteSealed(candidate.Path, tx);
                    MarkFinalTransaction(GetTransactionPaths(tx.TransactionId), tx.State, localManual);
                    RemoveCreatedDirectories(gameRoot, tx.CreatedDirectories);
                }
                TryDeleteDirectory(GetTransactionPaths(tx.TransactionId).StageDirectory);
                manual.AddRange(localManual);
            }
            return new(manual.Count == 0, manual.Count == 0 ? "Interrupted transaction recovery completed." : "Recovery left files for manual review.", null, manual);
        }
        catch (Exception ex) { return new(false, "Recovery failed: " + ex.Message); }
        finally { _gate.Release(); }
    }

    public OperationResult Uninstall(string transactionId, string gameRoot)
    {
        _gate.Wait();
        try
        {
            EnsureManagedRoots();
            var paths = GetTransactionPaths(transactionId);
            if (!File.Exists(paths.TransactionManifestPath)) return new(false, "No completed transaction was found.");
            var tx = ReadSealed(paths.TransactionManifestPath);
            return UninstallCore(tx, paths.TransactionManifestPath, paths.BackupDirectory, gameRoot);
        }
        catch (Exception ex) { return new(false, "Uninstall stopped safely: " + ex.Message); }
        finally { _gate.Release(); }
    }

    public IReadOnlyList<TransactionManifest> GetInstalledTransactions(string gameRoot)
    {
        EnsureManagedRoots();
        var folder = Path.Combine(_dataRoot, "transactions");
        if (!Directory.Exists(folder)) return [];
        var output = new List<TransactionManifest>();
        foreach (var path in Directory.EnumerateFiles(folder, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var tx = ReadSealed(path);
                if (tx.State == "Installed" && SamePath(tx.GameRoot, Path.GetFullPath(gameRoot))) output.Add(tx);
            }
            catch { }
        }
        return output.OrderByDescending(t => t.CreatedAtUtc).ToArray();
    }

    public IReadOnlyList<string> GetInstalledGameRoots()
    {
        var folder = Path.Combine(_dataRoot, "transactions");
        if (!Directory.Exists(folder)) return [];
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in Directory.EnumerateFiles(folder, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var tx = ReadSealed(path);
                if (tx.State == "Installed" && !string.IsNullOrWhiteSpace(tx.GameRoot))
                    roots.Add(Path.GetFullPath(tx.GameRoot));
            }
            catch { }
        }
        return roots.ToArray();
    }

    public OperationResult VerifyInstalledTransaction(string transactionId, string gameRoot)
    {
        try
        {
            var paths = GetTransactionPaths(transactionId);
            if (!File.Exists(paths.TransactionManifestPath)) return new(false, "Installed transaction record was not found.");
            var tx = ReadSealed(paths.TransactionManifestPath);
            if (tx.State != "Installed" || !SamePath(tx.GameRoot, Path.GetFullPath(gameRoot)))
                return new(false, "Transaction is not installed for this game directory.");
            var exe = SafePaths.UnderRoot(gameRoot, "Hollow Knight Silksong.exe");
            if (!File.Exists(exe) || !ManifestValidator.HashFile(exe).Equals(tx.GameExecutableSha256, StringComparison.OrdinalIgnoreCase))
                return new(false, "Game executable changed after the transaction.");
            foreach (var operation in tx.Operations)
            {
                var path = SafePaths.UnderRoot(gameRoot, operation.DestinationRelativePath);
                EnsureNoReparseParents(gameRoot, path);
                VerifyFile(path, operation.InstalledSha256, operation.SizeBytes, "Installed transaction file changed");
            }
            return new(true, "Installed files and transaction record verified.", tx.TransactionId);
        }
        catch (Exception ex) { return new(false, "Installed transaction verification failed: " + ex.Message); }
    }

    private OperationResult UninstallCore(TransactionManifest tx, string manifestPath, string backupDirectory, string gameRoot)
    {
        var game = Path.GetFullPath(gameRoot);
        if (!SamePath(tx.GameRoot, game)) return new(false, "Transaction belongs to a different game directory.");
        if (!IsValidTransaction(tx)) return new(false, "Transaction manifest integrity or paths are invalid.");
        if (tx.State is not ("Installed" or "Uninstalling" or "NeedsManualRecovery")) return new(false, "Transaction is not in an uninstallable state.");
        var exe = SafePaths.UnderRoot(game, "Hollow Knight Silksong.exe");
        if (!File.Exists(exe) || !ManifestValidator.HashFile(exe).Equals(tx.GameExecutableSha256, StringComparison.OrdinalIgnoreCase))
            return new(false, "Game executable identity changed; uninstall stopped.");
        if (_processGuard.IsRunning(exe)) return new(false, "Close Hollow Knight: Silksong before uninstalling or recovering.");
        tx.State = "Uninstalling";
        WriteSealed(manifestPath, tx);
        var manual = new List<string>();
        foreach (var operation in tx.Operations)
        {
            try
            {
                var target = SafePaths.UnderRoot(game, operation.DestinationRelativePath);
                EnsureNoReparseParents(game, target);
                if (!operation.OriginalExisted)
                {
                    if (!File.Exists(target)) { tx.ProcessedDestinations.Add(operation.DestinationRelativePath); WriteSealed(manifestPath, tx); continue; }
                    if (!ManifestValidator.HashFile(target).Equals(operation.InstalledSha256, StringComparison.OrdinalIgnoreCase))
                    { manual.Add(operation.DestinationRelativePath); continue; }
                    File.Delete(target);
                }
                else
                {
                    if (!SafePaths.IsSha256(operation.OriginalSha256) || string.IsNullOrWhiteSpace(operation.BackupRelativePath))
                    { manual.Add(operation.DestinationRelativePath); continue; }
                    if (File.Exists(target) && ManifestValidator.HashFile(target).Equals(operation.OriginalSha256, StringComparison.OrdinalIgnoreCase))
                    { tx.ProcessedDestinations.Add(operation.DestinationRelativePath); WriteSealed(manifestPath, tx); continue; }
                    if (File.Exists(target) && !ManifestValidator.HashFile(target).Equals(operation.InstalledSha256, StringComparison.OrdinalIgnoreCase))
                    { manual.Add(operation.DestinationRelativePath); continue; }
                    var backup = SafePaths.UnderRoot(backupDirectory, operation.BackupRelativePath);
                    SafePaths.RejectReparseAncestors(backup);
                    VerifyFile(backup, operation.OriginalSha256!, operation.OriginalSizeBytes ?? -1, "Backup is missing or corrupted");
                    RestoreFromBackup(backup, target, tx.TransactionId, operation.OriginalSha256!, operation.OriginalSizeBytes ?? -1);
                }
                tx.ProcessedDestinations.Add(operation.DestinationRelativePath);
                WriteSealed(manifestPath, tx);
            }
            catch { manual.Add(operation.DestinationRelativePath); }
        }
        RemoveCreatedDirectories(game, tx.CreatedDirectories);
        tx.State = manual.Count == 0 ? "Uninstalled" : "NeedsManualRecovery";
        WriteSealed(manifestPath, tx);
        return new(manual.Count == 0, manual.Count == 0 ? "Translation removed and original files restored." : "Some files changed after installation and were left untouched.", tx.TransactionId, manual);
    }

    private List<string> RollbackOperations(TransactionManifest tx, string backupDirectory, string gameRoot)
    {
        var manual = new List<string>();
        foreach (var operation in tx.Operations.AsEnumerable().Reverse())
        {
            try
            {
                var target = SafePaths.UnderRoot(gameRoot, operation.DestinationRelativePath);
                EnsureNoReparseParents(gameRoot, target);
                if (!string.IsNullOrWhiteSpace(operation.TemporaryRelativePath))
                {
                    var temp = SafePaths.UnderRoot(gameRoot, operation.TemporaryRelativePath);
                    if (File.Exists(temp)) File.Delete(temp);
                }
                if (!operation.OriginalExisted)
                {
                    if (!File.Exists(target)) continue;
                    if (ManifestValidator.HashFile(target).Equals(operation.InstalledSha256, StringComparison.OrdinalIgnoreCase)) File.Delete(target);
                    else manual.Add(operation.DestinationRelativePath);
                    continue;
                }
                if (!SafePaths.IsSha256(operation.OriginalSha256) || string.IsNullOrWhiteSpace(operation.BackupRelativePath))
                { manual.Add(operation.DestinationRelativePath); continue; }
                if (File.Exists(target) && ManifestValidator.HashFile(target).Equals(operation.OriginalSha256, StringComparison.OrdinalIgnoreCase)) continue;
                if (File.Exists(target) && !ManifestValidator.HashFile(target).Equals(operation.InstalledSha256, StringComparison.OrdinalIgnoreCase))
                { manual.Add(operation.DestinationRelativePath); continue; }
                var backup = SafePaths.UnderRoot(backupDirectory, operation.BackupRelativePath);
                SafePaths.RejectReparseAncestors(backup);
                VerifyFile(backup, operation.OriginalSha256!, operation.OriginalSizeBytes ?? -1, "Rollback backup missing or corrupted");
                RestoreFromBackup(backup, target, tx.TransactionId, operation.OriginalSha256!, operation.OriginalSizeBytes ?? -1);
            }
            catch { manual.Add(operation.DestinationRelativePath); }
        }
        return manual.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void RestoreFromBackup(string backup, string target, string transactionId, string expectedHash, long expectedSize)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var temp = target + "." + transactionId + ".restore-tmp";
        if (File.Exists(temp)) File.Delete(temp);
        File.Copy(backup, temp, overwrite: false);
        VerifyFile(temp, expectedHash, expectedSize, "Restored temporary file failed verification");
        File.Move(temp, target, overwrite: true);
        VerifyFile(target, expectedHash, expectedSize, "Restored file failed verification");
    }

    private bool HasInstalledTransaction(string gameRoot) => GetInstalledTransactions(gameRoot).Count > 0;

    private TransactionPaths GetTransactionPaths(string transactionId)
    {
        if (transactionId.Length != 32 || transactionId.Any(c => !Uri.IsHexDigit(c)))
            throw new InvalidDataException("Invalid transaction ID.");
        var transactions = Path.Combine(_dataRoot, "transactions");
        var backups = Path.Combine(_dataRoot, "backups", transactionId);
        var staging = Path.Combine(_dataRoot, "staging", transactionId);
        var journals = Path.Combine(_dataRoot, "journals");
        return new(transactionId, transactions, backups, staging, journals,
            Path.Combine(journals, transactionId + ".pending.json"),
            Path.Combine(transactions, transactionId + ".json"));
    }

    private static void MarkFinalTransaction(TransactionPaths paths, string state, List<string> manual)
    {
        if (!File.Exists(paths.TransactionManifestPath)) return;
        try
        {
            var final = ReadSealed(paths.TransactionManifestPath);
            if (!final.TransactionId.Equals(paths.Id, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Final transaction ID mismatch.");
            final.State = state;
            WriteSealed(paths.TransactionManifestPath, final);
        }
        catch
        {
            manual.Add(paths.TransactionManifestPath);
        }
    }

    private void MarkSupersededTransactions(IEnumerable<string> transactionIds, string supersededBy)
    {
        foreach (var transactionId in transactionIds)
        {
            var paths = GetTransactionPaths(transactionId);
            var previous = ReadSealed(paths.TransactionManifestPath);
            if (previous.State != "NeedsManualRecovery")
                throw new InvalidDataException("Prior transaction state changed before it could be superseded.");
            previous.State = "Superseded";
            previous.SupersededByTransactionId = supersededBy;
            WriteSealed(paths.TransactionManifestPath, previous);
        }
    }

    private void EnsureManagedRoots()
    {
        SafePaths.RejectReparseAncestors(_dataRoot);
        Directory.CreateDirectory(_dataRoot);
        SafePaths.RejectIfReparse(_dataRoot);
        foreach (var child in new[] { "transactions", "backups", "staging", "journals" })
        {
            var path = Path.Combine(_dataRoot, child);
            Directory.CreateDirectory(path);
            SafePaths.RejectIfReparse(path);
        }
    }

    private static void EnsureNoReparseParents(string gameRoot, string destination)
    {
        var parent = Path.GetDirectoryName(destination)!;
        SafePaths.RejectReparsePoints(gameRoot, parent, includeLeaf: true);
        if (File.Exists(destination) || Directory.Exists(destination))
            SafePaths.RejectReparsePoints(gameRoot, destination, includeLeaf: true);
    }

    private static List<string> GetNewDestinationDirectories(PreflightResult preflight)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in preflight.InstallFiles)
        {
            var directory = Path.GetDirectoryName(file.DestinationRelativePath!.Replace('/', Path.DirectorySeparatorChar));
            var current = preflight.GameRoot;
            if (string.IsNullOrEmpty(directory)) continue;
            foreach (var part in directory.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
            {
                current = Path.Combine(current, part);
                if (!Directory.Exists(current)) result.Add(Path.GetRelativePath(preflight.GameRoot, current));
            }
        }
        return result.OrderBy(p => p.Length).ToList();
    }

    private static void EnsureCreatedDirectories(string gameRoot, string destinationRelative)
    {
        var target = SafePaths.UnderRoot(gameRoot, destinationRelative);
        var parent = Path.GetDirectoryName(target)!;
        Directory.CreateDirectory(parent);
        SafePaths.RejectReparsePoints(gameRoot, parent, includeLeaf: true);
    }

    private static void RemoveCreatedDirectories(string gameRoot, IEnumerable<string> directories)
    {
        foreach (var relative in directories.OrderByDescending(x => x.Count(c => c == Path.DirectorySeparatorChar)))
        {
            try
            {
                var path = SafePaths.UnderRoot(gameRoot, relative);
                if (!Directory.Exists(path)) continue;
                SafePaths.RejectReparsePoints(gameRoot, path, includeLeaf: true);
                if (!Directory.EnumerateFileSystemEntries(path).Any()) Directory.Delete(path, recursive: false);
            }
            catch { }
        }
    }

    private static void CheckDiskSpace(long stagedBytes, long backupBytes, string gameRoot, string dataRoot)
    {
        var gameDrive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(gameRoot))!);
        var dataDrive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(dataRoot))!);
        if (string.Equals(gameDrive.Name, dataDrive.Name, StringComparison.OrdinalIgnoreCase))
        {
            var combined = checked(stagedBytes + stagedBytes + backupBytes + stagedBytes / 10);
            if (gameDrive.AvailableFreeSpace < combined)
                throw new IOException("Not enough free space for both the staged installation and external backups.");
            return;
        }
        if (gameDrive.AvailableFreeSpace < checked(stagedBytes + stagedBytes / 10))
            throw new IOException("Not enough free space on the game drive for the staged installation.");
        if (dataDrive.AvailableFreeSpace < checked(stagedBytes + backupBytes + (stagedBytes / 10)))
            throw new IOException("Not enough free space for package staging and verified backups.");
    }

    private static void VerifyFile(string path, string hash, long size, string error)
    {
        if (!File.Exists(path)) throw new IOException(error + ": file is missing.");
        var info = new FileInfo(path);
        if (size < 0 || info.Length != size || !ManifestValidator.HashFile(path).Equals(hash, StringComparison.OrdinalIgnoreCase))
            throw new IOException(error + ": size or SHA-256 mismatch.");
    }

    private static void WriteSealed(string path, TransactionManifest manifest)
    {
        TransactionIntegrity.Seal(manifest);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(manifest, JsonDefaults.Options);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".write-tmp";
        SafePaths.RejectReparseAncestors(path);
        SafePaths.RejectReparseAncestors(temp);
        using (var output = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 8192, FileOptions.WriteThrough))
        {
            output.Write(bytes);
            output.Flush(flushToDisk: true);
        }
        File.Move(temp, path, overwrite: true);
    }

    private static TransactionManifest ReadSealed(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length <= 0 || info.Length > 4 * 1024 * 1024) throw new InvalidDataException("Transaction record is missing or too large.");
        SafePaths.RejectReparseAncestors(path);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var manifest = JsonSerializer.Deserialize<TransactionManifest>(stream, JsonDefaults.Options) ?? throw new InvalidDataException("Transaction record is invalid.");
        if (!TransactionIntegrity.Verify(manifest) || !IsValidTransaction(manifest)) throw new InvalidDataException("Transaction record integrity check failed.");
        return manifest;
    }

    private static bool IsValidTransaction(TransactionManifest tx)
    {
        if (tx.SchemaVersion != 1 || tx.TransactionId.Length != 32 || tx.TransactionId.Any(c => !Uri.IsHexDigit(c)) ||
            (tx.SupersedesTransactionIds ?? []).Any(id => id.Length != 32 || id.Any(c => !Uri.IsHexDigit(c)) || id.Equals(tx.TransactionId, StringComparison.OrdinalIgnoreCase)) ||
            (tx.SupersededByTransactionId is not null && (tx.SupersededByTransactionId.Length != 32 || tx.SupersededByTransactionId.Any(c => !Uri.IsHexDigit(c)))) ||
            !SafePaths.IsSha256(tx.GameExecutableSha256) || tx.Operations.Count > 1000) return false;
        var dests = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var op in tx.Operations)
        {
            try
            {
                SafePaths.NormalizeRelative(op.DestinationRelativePath);
                if (!dests.Add(op.DestinationRelativePath) || !SafePaths.IsSha256(op.InstalledSha256)) return false;
                if (op.OriginalExisted && (!SafePaths.IsSha256(op.OriginalSha256) || string.IsNullOrWhiteSpace(op.BackupRelativePath) ||
                    !string.Equals(SafePaths.NormalizeRelative(op.BackupRelativePath), SafePaths.NormalizeRelative(op.DestinationRelativePath), StringComparison.OrdinalIgnoreCase))) return false;
                if (!string.IsNullOrWhiteSpace(op.BackupRelativePath)) SafePaths.NormalizeRelative(op.BackupRelativePath);
                if (!string.IsNullOrWhiteSpace(op.TemporaryRelativePath))
                {
                    SafePaths.NormalizeRelative(op.TemporaryRelativePath);
                    if (!op.TemporaryRelativePath.EndsWith("." + tx.TransactionId + ".slar-tmp", StringComparison.OrdinalIgnoreCase)) return false;
                }
            }
            catch { return false; }
        }
        return true;
    }

    private static bool SamePath(string left, string right) =>
        Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar).Equals(Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);

    private static string FormatIssues(IEnumerable<ManifestIssue> issues) => string.Join(Environment.NewLine, issues.Select(i => $"{i.Code}: {i.Message}{(i.Path is null ? "" : " [" + i.Path + "]")}"));

    private static bool SameConflictSnapshot(IReadOnlyList<ExistingDestinationConflict> left, IReadOnlyList<ExistingDestinationConflict> right) =>
        left.Count == right.Count && left.OrderBy(item => item.RelativePath, StringComparer.OrdinalIgnoreCase)
            .Zip(right.OrderBy(item => item.RelativePath, StringComparer.OrdinalIgnoreCase))
            .All(pair => pair.First.RelativePath.Equals(pair.Second.RelativePath, StringComparison.OrdinalIgnoreCase) &&
                pair.First.SizeBytes == pair.Second.SizeBytes && pair.First.Sha256.Equals(pair.Second.Sha256, StringComparison.OrdinalIgnoreCase));

    private static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); }
        catch { }
    }

    private sealed record TransactionPaths(string Id, string TransactionDirectory, string BackupDirectory,
        string StageDirectory, string JournalDirectory, string JournalPath, string TransactionManifestPath);
}
