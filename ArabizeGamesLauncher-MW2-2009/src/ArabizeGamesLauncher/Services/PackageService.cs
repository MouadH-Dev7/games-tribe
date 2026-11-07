using System.Security.Cryptography;
using System.Text.Json;
using ArabizeGamesLauncher.Models;

namespace ArabizeGamesLauncher.Services;

public sealed class PackageService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly HashSet<string> ForbiddenExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".exe", ".dll", ".sys", ".asi", ".com", ".scr", ".bat", ".cmd", ".ps1" };

    public IReadOnlyList<PackageEntry> Discover(string packageRoot, string supportedBuild)
    {
        if (!Directory.Exists(packageRoot)) return [];
        var manifests = Directory.EnumerateFiles(packageRoot, "package.json", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(packageRoot, "manifest.json", SearchOption.AllDirectories))
            .Distinct(StringComparer.OrdinalIgnoreCase);
        return manifests.Select(path => Validate(path, supportedBuild)).ToArray();
    }

    public PackageEntry Validate(string manifestPath, string supportedBuild)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(manifestPath)) ?? string.Empty;
        try
        {
            var manifest = JsonSerializer.Deserialize<PackageManifest>(File.ReadAllText(manifestPath), JsonOptions);
            if (manifest is null || !HasRequiredFields(manifest))
                return Invalid(directory, "Manifest is missing required fields.");
            if (!manifest.GameId.Equals(GameDetectionService.GameId, StringComparison.OrdinalIgnoreCase)
                || !manifest.SupportedEdition.Contains("Campaign Remastered", StringComparison.OrdinalIgnoreCase)
                || !manifest.SupportedBuilds.Contains(supportedBuild, StringComparer.OrdinalIgnoreCase))
                return FromManifest(directory, manifest, PackageStatus.Incompatible, "This package does not support the detected Campaign Remastered build.");
            if (manifest.Files.Count == 0)
                return FromManifest(directory, manifest, PackageStatus.Incompatible, "The manifest has no package files; demonstration manifests cannot be installed.");

            foreach (var file in manifest.Files)
            {
                if (!IsSafeRelativePath(file.Source) || !IsSafeRelativePath(file.Destination))
                    return FromManifest(directory, manifest, PackageStatus.Incompatible, "A source or destination path is unsafe.");
                if (ForbiddenExtensions.Contains(Path.GetExtension(file.Destination)))
                    return FromManifest(directory, manifest, PackageStatus.Incompatible, "Executable, library, driver, and script replacements are not allowed.");
                if (string.IsNullOrWhiteSpace(file.Sha256) || file.Sha256.Length != 64 || !file.Sha256.All(Uri.IsHexDigit))
                    return FromManifest(directory, manifest, PackageStatus.Incompatible, "A package file has an invalid SHA-256 value.");
                var source = Path.GetFullPath(Path.Combine(directory, file.Source));
                if (!source.StartsWith(directory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !File.Exists(source))
                    return FromManifest(directory, manifest, PackageStatus.Incompatible, "A package source file is missing or escapes its package folder.");
                using var stream = File.OpenRead(source);
                var actual = Convert.ToHexString(SHA256.HashData(stream));
                if (!actual.Equals(file.Sha256, StringComparison.OrdinalIgnoreCase))
                    return FromManifest(directory, manifest, PackageStatus.Incompatible, $"SHA-256 mismatch for {file.Source}.");
            }
            return FromManifest(directory, manifest, PackageStatus.Validated, "Manifest, build compatibility, paths, and file hashes are valid. Installation remains disabled.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException)
        {
            return Invalid(directory, $"Package validation failed: {ex.Message}");
        }
    }

    public static bool IsSafeRelativePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path)) return false;
        if (path.Contains(':') || path.Contains('\\') && path.StartsWith('\\')) return false;
        var parts = path.Replace('\\', '/').Split('/');
        return parts.All(part => part.Length > 0 && part != "." && part != ".." && part.IndexOfAny(Path.GetInvalidFileNameChars()) < 0);
    }

    private static bool HasRequiredFields(PackageManifest? m) => m is not null &&
        !string.IsNullOrWhiteSpace(m.PackageId) && !string.IsNullOrWhiteSpace(m.GameId) &&
        !string.IsNullOrWhiteSpace(m.SupportedEdition) && m.SupportedBuilds is { Count: > 0 } &&
        !string.IsNullOrWhiteSpace(m.PackageVersion) && !string.IsNullOrWhiteSpace(m.Language) &&
        !string.IsNullOrWhiteSpace(m.InstallationMethod) && !string.IsNullOrWhiteSpace(m.InstallationInstructions) && m.Files is not null;

    private static PackageEntry Invalid(string directory, string reason) => new()
        { PackageDirectory = directory, Title = Path.GetFileName(directory), Status = PackageStatus.Incompatible, StatusMessage = reason };

    private static PackageEntry FromManifest(string directory, PackageManifest m, PackageStatus status, string reason) => new()
        { PackageDirectory = directory, PackageId = m.PackageId, Title = m.PackageId, Version = m.PackageVersion, Language = m.Language, Status = status, StatusMessage = reason, CompatibilityNotes = m.CompatibilityNotes, InstallationMethod = m.InstallationMethod, FileCount = m.Files.Count };
}
