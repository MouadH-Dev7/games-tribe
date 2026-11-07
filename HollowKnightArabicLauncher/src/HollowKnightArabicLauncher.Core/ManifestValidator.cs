using System.Security.Cryptography;
using System.Text.Json;

namespace HollowKnightArabicLauncher.Core;

public sealed class ManifestValidator
{
    private const int MaximumManifestBytes = 8 * 1024 * 1024;
    private const int MaximumFiles = 1000;
    private const long MaximumFileBytes = 512L * 1024 * 1024;
    private const long MaximumTotalBytes = 2L * 1024 * 1024 * 1024;
    private readonly string? _expectedManifestSha256;

    public ManifestValidator(string? expectedManifestSha256 = null)
    {
        if (expectedManifestSha256 is not null && !SafePaths.IsSha256(expectedManifestSha256))
            throw new ArgumentException("Expected manifest digest must be a SHA-256 hex string.", nameof(expectedManifestSha256));
        _expectedManifestSha256 = expectedManifestSha256;
    }

    public Phase2Manifest Read(string manifestPath)
    {
        SafePaths.RejectReparseAncestors(manifestPath);
        var info = new FileInfo(manifestPath);
        if (!info.Exists || info.Length <= 0 || info.Length > MaximumManifestBytes)
            throw new InvalidDataException("The manifest is missing, empty, or exceeds the size limit.");
        if (_expectedManifestSha256 is not null && !HashFile(manifestPath).Equals(_expectedManifestSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The bundled package manifest has changed and cannot be trusted.");
        using var stream = new FileStream(manifestPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return JsonSerializer.Deserialize<Phase2Manifest>(stream, JsonDefaults.Options)
            ?? throw new InvalidDataException("The manifest is empty or invalid.");
    }

    public PreflightResult Validate(string manifestPath, string packageRoot, string gameRoot)
    {
        var manifest = Read(manifestPath);
        var issues = new List<ManifestIssue>();
        var existingFileConflicts = new List<ExistingDestinationConflict>();
        var installFiles = manifest.Files.Where(f => f.IncludedPerReadme).ToArray();
        long totalBytes = 0;
        string package = Path.GetFullPath(packageRoot);
        string game = Path.GetFullPath(gameRoot);

        if (manifest.SchemaVersion != 1) issues.Add(new("manifest-version", "Unsupported manifest schema."));
        if (!string.Equals(manifest.TargetGame.Title, "Hollow Knight: Godmaster (PC)", StringComparison.OrdinalIgnoreCase))
            issues.Add(new("wrong-game", "This package manifest is not for Hollow Knight: Godmaster PC."));
        if (!string.Equals(manifest.TargetGame.Executable.RelativePath, "hollow_knight.exe", StringComparison.OrdinalIgnoreCase))
            issues.Add(new("unexpected-executable", "The manifest executable identity is not recognized."));
        if (!SafePaths.IsSha256(manifest.TargetGame.Executable.Sha256) || manifest.TargetGame.Executable.SizeBytes <= 0)
            issues.Add(new("invalid-game-identity", "The manifest does not contain a valid game executable identity."));
        if (manifest.Files.Count is 0 or > MaximumFiles || installFiles.Length == 0)
            issues.Add(new("file-count", "Manifest file count is outside the supported range."));
        if (!Directory.Exists(package)) issues.Add(new("package-missing", "Translation package directory was not found.", package));
        if (!Directory.Exists(game)) issues.Add(new("game-missing", "Game directory was not found.", game));

        if (Directory.Exists(package))
        {
            try { SafePaths.RejectReparseAncestors(package); }
            catch (Exception ex) { issues.Add(new("package-reparse", ex.Message, package)); }
        }
        if (Directory.Exists(game))
        {
            try { SafePaths.RejectIfReparse(game); }
            catch (Exception ex) { issues.Add(new("game-reparse", ex.Message, game)); }
        }

        var sourcePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var destinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in manifest.Files)
        {
            if (item.SizeBytes < 0 || item.SizeBytes > MaximumFileBytes)
            {
                issues.Add(new("file-size", "File size is outside the allowed limit.", item.SourceRelativePath));
                continue;
            }
            try { totalBytes = checked(totalBytes + item.SizeBytes); }
            catch (OverflowException) { issues.Add(new("total-size", "Package total size overflowed.")); break; }
            if (totalBytes > MaximumTotalBytes) issues.Add(new("total-size", "Package exceeds the total size limit."));
            if (!SafePaths.IsSha256(item.Sha256)) issues.Add(new("invalid-hash", "Manifest contains an invalid SHA-256.", item.SourceRelativePath));
            if (!item.IncludedPerReadme && !item.SourceRelativePath.Equals(manifest.PackageIdentity.InstructionsRelativePath, StringComparison.OrdinalIgnoreCase))
                issues.Add(new("unexpected-excluded-file", "Only the declared instructions file may be excluded.", item.SourceRelativePath));
            if (item.IncludedPerReadme && string.IsNullOrWhiteSpace(item.DestinationRelativePath))
                issues.Add(new("missing-destination", "An included file has no destination path.", item.SourceRelativePath));
            if (!item.IncludedPerReadme && item.DestinationRelativePath is not null)
                issues.Add(new("excluded-has-destination", "An excluded file must not have a destination.", item.SourceRelativePath));

            try
            {
                var sourceRel = SafePaths.NormalizeRelative(item.SourceRelativePath);
                if (!sourcePaths.Add(sourceRel)) issues.Add(new("duplicate-source", "Duplicate package source path.", item.SourceRelativePath));
                var source = SafePaths.UnderRoot(package, sourceRel);
                if (File.Exists(source)) SafePaths.RejectReparsePoints(package, source, includeLeaf: true);
                if (Directory.Exists(package)) SafePaths.RejectReparsePoints(package, source, includeLeaf: false);

                if (!File.Exists(source))
                {
                    issues.Add(new("source-missing", "Package file is missing.", item.SourceRelativePath));
                    continue;
                }
                var sourceInfo = new FileInfo(source);
                if (sourceInfo.Length != item.SizeBytes)
                    issues.Add(new("source-size-mismatch", "Package file size differs from the manifest.", item.SourceRelativePath));
                var sourceHash = HashFile(source);
                if (!sourceHash.Equals(item.Sha256, StringComparison.OrdinalIgnoreCase))
                    issues.Add(new("source-hash-mismatch", "Package SHA-256 differs from the manifest.", item.SourceRelativePath));

                if (!item.IncludedPerReadme) continue;

                var destRel = SafePaths.NormalizeRelative(item.DestinationRelativePath);
                if (!destinations.Add(destRel)) issues.Add(new("duplicate-destination", "Duplicate destination path.", item.DestinationRelativePath));
                var target = SafePaths.UnderRoot(game, destRel);
                if (File.Exists(target) || Directory.Exists(Path.GetDirectoryName(target)!))
                    SafePaths.RejectReparsePoints(game, target, includeLeaf: File.Exists(target));

                var expectedReplacement = item.ProposedAction.Equals("REPLACE", StringComparison.OrdinalIgnoreCase);
                if (expectedReplacement)
                {
                    if (!item.DestinationCurrentlyExists || !SafePaths.IsSha256(item.ExistingDestinationSha256))
                        issues.Add(new("replacement-not-authorized", "Replacement lacks a verified original-file hash.", item.DestinationRelativePath));
                    if (!File.Exists(target))
                        issues.Add(new("replacement-missing", "Expected original file is missing.", item.DestinationRelativePath));
                    else if (SafePaths.IsSha256(item.ExistingDestinationSha256) &&
                             !HashFile(target).Equals(item.ExistingDestinationSha256, StringComparison.OrdinalIgnoreCase))
                        issues.Add(new("replacement-changed", "Existing file no longer matches the recorded original hash.", item.DestinationRelativePath));
                }
                else if (item.ProposedAction.Equals("ADD", StringComparison.OrdinalIgnoreCase))
                {
                    if (item.DestinationCurrentlyExists)
                        issues.Add(new("unrecorded-existing-state", "Manifest incorrectly expects a new destination.", item.DestinationRelativePath));
                    if (File.Exists(target))
                    {
                        var current = new FileInfo(target);
                        existingFileConflicts.Add(new(destRel, current.Length, HashFile(target)));
                        issues.Add(new("unexpected-conflict", "Destination already exists and will not be overwritten.", item.DestinationRelativePath));
                    }
                    else if (Directory.Exists(target))
                        issues.Add(new("destination-is-directory", "A directory occupies a package file destination.", item.DestinationRelativePath));
                }
                else
                {
                    issues.Add(new("unsupported-action", "Included file has an unsupported proposed action.", item.DestinationRelativePath));
                }
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException)
            {
                issues.Add(new("unsafe-path", ex.Message, item.DestinationRelativePath ?? item.SourceRelativePath));
            }
        }

        if (Directory.Exists(package))
        {
            try
            {
                var actual = EnumeratePackageFiles(package).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var declared = sourcePaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var extra in actual.Except(declared)) issues.Add(new("unlisted-package-file", "Package contains a file not declared by the manifest.", extra));
                foreach (var missing in declared.Except(actual)) issues.Add(new("package-file-inventory", "Manifest lists a file outside the package inventory.", missing));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                issues.Add(new("package-inventory", ex.Message, package));
            }
        }

        if (Directory.Exists(game)) issues.AddRange(GameIdentityValidator.Validate(manifest, game));
        return new PreflightResult
        {
            Manifest = manifest,
            ManifestPath = Path.GetFullPath(manifestPath),
            PackageRoot = package,
            GameRoot = game,
            InstallFiles = installFiles,
            Issues = issues,
            ExistingFileConflicts = existingFileConflicts,
            TotalBytes = totalBytes
        };
    }

    public static string HashFile(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static IEnumerable<string> EnumeratePackageFiles(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory, "*", SearchOption.TopDirectoryOnly))
            {
                var attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    throw new IOException($"Package contains a reparse point: {entry}");
                if ((attributes & FileAttributes.Directory) != 0) pending.Push(entry);
                else yield return Path.GetRelativePath(root, entry);
            }
        }
    }
}


