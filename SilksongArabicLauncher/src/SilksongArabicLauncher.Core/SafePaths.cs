namespace SilksongArabicLauncher.Core;

internal static class SafePaths
{
    public static string NormalizeRelative(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidDataException("A relative path is required.");
        var normalized = value.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(normalized) || normalized.StartsWith(Path.DirectorySeparatorChar))
            throw new InvalidDataException($"Rooted path is not allowed: {value}");

        var parts = normalized.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || parts.Any(p => p is "." or ".." || p.Contains(':') || p.Contains('\0')))
            throw new InvalidDataException($"Unsafe relative path: {value}");
        return Path.Combine(parts);
    }

    public static string UnderRoot(string root, string relative)
    {
        var safeRelative = NormalizeRelative(relative);
        var fullRoot = Path.GetFullPath(root);
        var fullPath = Path.GetFullPath(Path.Combine(fullRoot, safeRelative));
        var prefix = Path.EndsInDirectorySeparator(fullRoot) ? fullRoot : fullRoot + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Path escapes its root: {relative}");
        return fullPath;
    }

    public static void RejectReparsePoints(string root, string fullPath, bool includeLeaf)
    {
        var canonicalRoot = Path.GetFullPath(root);
        if (!Directory.Exists(canonicalRoot)) throw new DirectoryNotFoundException(canonicalRoot);
        RejectIfReparse(canonicalRoot);
        var relative = Path.GetRelativePath(canonicalRoot, Path.GetFullPath(fullPath));
        if (relative == ".") return;

        var parts = relative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        var current = canonicalRoot;
        for (var i = 0; i < parts.Length; i++)
        {
            current = Path.Combine(current, parts[i]);
            var isLeaf = i == parts.Length - 1;
            if (isLeaf && !includeLeaf) break;
            if (File.Exists(current) || Directory.Exists(current)) RejectIfReparse(current);
        }
    }

    public static void RejectIfReparse(string path)
    {
        var attrs = File.GetAttributes(path);
        if ((attrs & FileAttributes.ReparsePoint) != 0)
            throw new IOException($"Reparse points are not allowed in managed paths: {path}");
    }

    public static void RejectReparseAncestors(string path)
    {
        var current = Path.GetFullPath(path);
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(current) || Directory.Exists(current)) RejectIfReparse(current);
            var parent = Path.GetDirectoryName(current);
            if (string.IsNullOrEmpty(parent) || parent.Equals(current, StringComparison.OrdinalIgnoreCase)) break;
            current = parent;
        }
    }

    public static bool IsSha256(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
}
