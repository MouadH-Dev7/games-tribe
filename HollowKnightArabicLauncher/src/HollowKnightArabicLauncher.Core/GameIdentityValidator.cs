using System.Diagnostics;
using System.Text.RegularExpressions;

namespace HollowKnightArabicLauncher.Core;

public static class GameIdentityValidator
{
    public static IReadOnlyList<ManifestIssue> Validate(Phase2Manifest manifest, string gameRoot)
    {
        var issues = new List<ManifestIssue>();
        var game = Path.GetFullPath(gameRoot);
        if (!Directory.Exists(game)) return [new("game-missing", "Game directory was not found.", game)];
        try
        {
            SafePaths.RejectReparseAncestors(game);
            var identity = manifest.TargetGame.Executable;
            var relative = SafePaths.NormalizeRelative(identity.RelativePath);
            var exe = SafePaths.UnderRoot(game, relative);
            if (!File.Exists(exe)) return [new("game-exe-missing", "hollow_knight.exe was not found.", relative)];
            SafePaths.RejectReparsePoints(game, exe, includeLeaf: true);
            if (SafePaths.IsSha256(identity.Sha256) && !ManifestValidator.HashFile(exe).Equals(identity.Sha256, StringComparison.OrdinalIgnoreCase))
                issues.Add(new("game-exe-changed", "Game executable does not match the locally audited identity.", relative));
            if (identity.SizeBytes > 0 && new FileInfo(exe).Length != identity.SizeBytes)
                issues.Add(new("game-exe-size", "Game executable size differs from the locally audited identity.", relative));

            var player = Path.Combine(game, "UnityPlayer.dll");
            if (!File.Exists(player)) issues.Add(new("unity-player-missing", "UnityPlayer.dll was not found."));
            else
            {
                SafePaths.RejectReparsePoints(game, player, includeLeaf: true);
                if (!string.IsNullOrWhiteSpace(identity.FileVersion) &&
                    !(FileVersionInfo.GetVersionInfo(player).FileVersion ?? "").Equals(identity.FileVersion, StringComparison.OrdinalIgnoreCase))
                    issues.Add(new("unity-version-mismatch", "Unity player version differs from the audited game candidate."));
            }

            foreach (var marker in new[] { "hollow_knight_Data\\globalgamemanagers", "hollow_knight_Data\\Managed\\Assembly-CSharp.dll" })
            {
                var path = SafePaths.UnderRoot(game, marker);
                if (!File.Exists(path)) issues.Add(new("game-marker-missing", "Required Hollow Knight game data marker is missing.", marker));
                else SafePaths.RejectReparsePoints(game, path, includeLeaf: true);
            }
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException or InvalidDataException)
        {
            issues.Add(new("game-validation", ex.Message));
        }
        return issues;
    }

    public static string GetExecutablePath(Phase2Manifest manifest, string gameRoot) =>
        SafePaths.UnderRoot(gameRoot, manifest.TargetGame.Executable.RelativePath);

    public static bool LooksLikeSha256(string? value) => SafePaths.IsSha256(value);
}

public sealed record GameCandidate(string Path, bool Valid, IReadOnlyList<ManifestIssue> Issues);

public sealed class GameDiscoveryService
{
    public IReadOnlyList<GameCandidate> Discover(Phase2Manifest manifest, string? manuallySelectedPath = null,
        IEnumerable<string>? additionalPaths = null)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (Directory.Exists(manifest.TargetGame.CandidateInstallationPath)) paths.Add(Path.GetFullPath(manifest.TargetGame.CandidateInstallationPath));
        if (!string.IsNullOrWhiteSpace(manuallySelectedPath) && Directory.Exists(manuallySelectedPath)) paths.Add(Path.GetFullPath(manuallySelectedPath));
        if (additionalPaths is not null)
            foreach (var path in additionalPaths)
                if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path)) paths.Add(Path.GetFullPath(path));
        foreach (var path in FindKnownSteamCandidates()) if (Directory.Exists(path)) paths.Add(Path.GetFullPath(path));
        return paths.Select(path =>
        {
            var issues = GameIdentityValidator.Validate(manifest, path);
            return new GameCandidate(path, issues.Count == 0, issues);
        }).OrderByDescending(c => c.Valid).ThenBy(c => c.Path, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static IEnumerable<string> FindKnownSteamCandidates()
    {
        var roots = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Steam"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Steam"),
            @"C:\Steam",
            @"C:\SteamLibrary",
            @"C:\Games\SteamLibrary"
        }.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase);
        var libraries = new HashSet<string>(roots, StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots)
        {
            var vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(vdf)) continue;
            try
            {
                var text = File.ReadAllText(vdf);
                foreach (Match match in Regex.Matches(text, @"""path""\s+""([^""]+)""", RegexOptions.IgnoreCase))
                    libraries.Add(match.Groups[1].Value.Replace(@"\\", @"\"));
            }
            catch { }
        }
        return libraries.Select(root => Path.Combine(root, "steamapps", "common", "Hollow Knight"));
    }
}


