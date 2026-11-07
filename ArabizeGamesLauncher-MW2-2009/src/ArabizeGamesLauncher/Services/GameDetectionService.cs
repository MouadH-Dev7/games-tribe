using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using ArabizeGamesLauncher.Models;

namespace ArabizeGamesLauncher.Services;

public interface IExecutableMetadataReader
{
    (string ProductName, string FileVersion) Read(string path);
}

public interface IGameInstallationDetector
{
    GameDetectionResult Detect(string? directory);
}

public sealed class ExecutableMetadataReader : IExecutableMetadataReader
{
    public (string ProductName, string FileVersion) Read(string path)
    {
        var info = FileVersionInfo.GetVersionInfo(path);
        return (info.ProductName ?? string.Empty, info.FileVersion ?? string.Empty);
    }
}

public sealed class GameDetectionService(IExecutableMetadataReader? metadataReader = null) : IGameInstallationDetector
{
    public const string GameId = "mw2-campaign-remastered-pc";
    public const string ExpectedProduct = "Call of Duty: Modern Warfare 2 Campaign Remastered";
    public const string ExpectedBuildProduct = "lazr";
    public const string ExpectedVersion = "1.1.2.1279292";
    public const string ExpectedBuildName = "CRC_1279292A_retail_signed_ship";
    public const string DefaultDirectory = @"C:\Games\Call of Duty - Modern Warfare 2 CR";
    private readonly IExecutableMetadataReader _metadataReader = metadataReader ?? new ExecutableMetadataReader();

    public GameDetectionResult Detect(string? directory)
    {
        var path = string.IsNullOrWhiteSpace(directory) ? string.Empty : Path.GetFullPath(directory.Trim());
        if (path.Length == 0 || !Directory.Exists(path))
            return Result(GameDetectionStatus.NotFound, path, "The selected folder does not exist.");

        var exe = Path.Combine(path, "MW2CR.exe");
        var buildInfo = Path.Combine(path, ".build.info");
        var dataDir = Path.Combine(path, "Data", "data");
        var mainCheck = Path.Combine(path, "main", "fileSysCheck.cfg");
        var launcher = Path.Combine(path, "MW2 Campaign Remastered Launcher.exe");
        var evidence = new List<EvidenceItem>
        {
            new("Game executable", File.Exists(exe) ? "MW2CR.exe found" : "MW2CR.exe missing", File.Exists(exe)),
            new("Build metadata", File.Exists(buildInfo) ? ".build.info found" : ".build.info missing", File.Exists(buildInfo)),
            new("Game data", Directory.Exists(dataDir) ? "Data\\data found" : "Data\\data missing", Directory.Exists(dataDir)),
            new("Game files", File.Exists(mainCheck) ? "main\\fileSysCheck.cfg found" : "main\\fileSysCheck.cfg missing", File.Exists(mainCheck)),
            new("Local launcher", File.Exists(launcher) ? "Campaign Remastered launcher found" : "Launcher missing", File.Exists(launcher))
        };
        var missing = evidence.Where(item => !item.IsPositive).Select(item => item.Label).ToArray();
        if (missing.Length != 0)
            return Result(GameDetectionStatus.Unsupported, path, "Required installation evidence is missing: " + string.Join(", ", missing) + ".", evidence);

        var exeMetadata = _metadataReader.Read(exe);
        var productMatches = exeMetadata.ProductName.Equals(ExpectedProduct, StringComparison.OrdinalIgnoreCase);
        evidence.Add(new("Executable product", exeMetadata.ProductName, productMatches));
        evidence.Add(new("Executable file version", exeMetadata.FileVersion));
        if (!productMatches)
            return Result(GameDetectionStatus.Unsupported, path, $"Executable product mismatch: expected '{ExpectedProduct}', found '{exeMetadata.ProductName}'.", evidence, identity: exeMetadata.ProductName);

        BuildInfoRow active;
        try { active = ReadActiveBuildInfo(buildInfo); }
        catch (InvalidDataException ex)
        {
            return Result(GameDetectionStatus.Unsupported, path, "Build metadata rejected: " + ex.Message, evidence, identity: exeMetadata.ProductName);
        }
        evidence.Add(new("Build Key", active.BuildKey));
        evidence.Add(new("Build version", active.Version, active.Version == ExpectedVersion));
        evidence.Add(new("Build branch", active.Branch));

        if (active.Version != ExpectedVersion)
            return Result(GameDetectionStatus.Unsupported, path, $"Unsupported build version: expected '{ExpectedVersion}', found '{active.Version}'.", evidence, identity: exeMetadata.ProductName, version: active.Version, branch: active.Branch, buildKey: active.BuildKey);

        BuildConfig config;
        try { config = ReadBuildConfig(path, active.BuildKey); }
        catch (InvalidDataException ex)
        {
            return Result(GameDetectionStatus.Unsupported, path, "Active Build Key configuration rejected: " + ex.Message, evidence, identity: exeMetadata.ProductName, version: active.Version, branch: active.Branch, buildKey: active.BuildKey);
        }

        evidence.Add(new("Build product", config.Product, config.Product.Equals(ExpectedBuildProduct, StringComparison.Ordinal)));
        evidence.Add(new("Build name", config.Name, config.Name.Equals(ExpectedBuildName, StringComparison.Ordinal)));
        if (config.Product != ExpectedBuildProduct || config.Name != ExpectedBuildName)
        {
            var reasons = new List<string>();
            if (config.Product != ExpectedBuildProduct) reasons.Add($"product expected '{ExpectedBuildProduct}', found '{config.Product}'");
            if (config.Name != ExpectedBuildName) reasons.Add($"build name expected '{ExpectedBuildName}', found '{config.Name}'");
            return Result(GameDetectionStatus.Unsupported, path, "Unsupported active build configuration: " + string.Join("; ", reasons) + ".", evidence, exeMetadata.ProductName, active.Version, config.Product, active.Branch, active.BuildKey, config.Name);
        }

        using var stream = File.OpenRead(exe);
        var hash = Convert.ToHexString(SHA256.HashData(stream));
        evidence.Add(new("Executable SHA-256", hash));
        return new()
        {
            Status = GameDetectionStatus.Detected, InstallationPath = path, Identity = exeMetadata.ProductName,
            GameVersion = active.Version, BuildProduct = config.Product, BuildName = config.Name, Branch = active.Branch,
            BuildKey = active.BuildKey, ExecutablePath = exe, ExecutableSha256 = hash,
            Message = "Exact supported MW2 Campaign Remastered build verified.", Evidence = evidence
        };
    }

    private static BuildInfoRow ReadActiveBuildInfo(string path)
    {
        var lines = File.ReadAllLines(path);
        if (lines.Length < 2) throw new InvalidDataException(".build.info has no data rows.");
        var headers = lines[0].Split('|').Select(header => header.Split('!')[0]).ToArray();
        var required = new[] { "Build Key", "Active", "Version", "Branch" };
        if (required.Any(name => headers.Count(header => header.Equals(name, StringComparison.OrdinalIgnoreCase)) != 1))
            throw new InvalidDataException(".build.info is missing a unique required column (Build Key, Active, Version, Branch).");

        var rows = new List<BuildInfoRow>();
        foreach (var line in lines.Skip(1).Where(line => !string.IsNullOrWhiteSpace(line)))
        {
            var values = line.Split('|');
            if (values.Length != headers.Length) throw new InvalidDataException("A .build.info row does not match the header column count.");
            string Value(string name) => values[Array.FindIndex(headers, h => h.Equals(name, StringComparison.OrdinalIgnoreCase))].Trim();
            if (Value("Active") != "1") continue;
            rows.Add(new(Value("Build Key"), Value("Version"), Value("Branch")));
        }
        if (rows.Count != 1) throw new InvalidDataException($"Expected exactly one active .build.info row; found {rows.Count}.");
        var row = rows[0];
        if (!Regex.IsMatch(row.BuildKey, "^[0-9a-fA-F]{32}$")) throw new InvalidDataException("The active Build Key is missing or malformed.");
        if (string.IsNullOrWhiteSpace(row.Version) || string.IsNullOrWhiteSpace(row.Branch)) throw new InvalidDataException("The active row has a blank Version or Branch.");
        return row;
    }

    private static BuildConfig ReadBuildConfig(string gameDirectory, string buildKey)
    {
        var configPath = Path.Combine(gameDirectory, "Data", "config", buildKey[..2], buildKey.Substring(2, 2), buildKey);
        if (!File.Exists(configPath)) throw new InvalidDataException($"Configuration for active Build Key '{buildKey}' is missing at Data\\config\\{buildKey[..2]}\\{buildKey.Substring(2, 2)}\\{buildKey}.");
        var fields = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in File.ReadLines(configPath))
        {
            var separator = line.IndexOf('=');
            if (separator < 0) continue;
            var key = line[..separator].Trim();
            if (!key.Equals("build-product", StringComparison.OrdinalIgnoreCase) && !key.Equals("build-name", StringComparison.OrdinalIgnoreCase)) continue;
            if (!fields.TryGetValue(key, out var values)) fields[key] = values = [];
            values.Add(line[(separator + 1)..].Trim());
        }
        string Unique(string key)
        {
            if (!fields.TryGetValue(key, out var values) || values.Count != 1 || string.IsNullOrWhiteSpace(values[0]))
                throw new InvalidDataException($"Active config must contain exactly one non-empty '{key}' field.");
            return values[0];
        }
        return new(Unique("build-product"), Unique("build-name"));
    }

    private static GameDetectionResult Result(GameDetectionStatus status, string path, string message,
        IReadOnlyList<EvidenceItem>? evidence = null, string identity = "", string version = "", string product = "", string branch = "", string buildKey = "", string buildName = "") => new()
    {
        Status = status, InstallationPath = path, Identity = identity, GameVersion = version,
        BuildProduct = product, Branch = branch, BuildKey = buildKey, BuildName = buildName,
        Message = message, Evidence = evidence ?? []
    };

    private sealed record BuildInfoRow(string BuildKey, string Version, string Branch);
    private sealed record BuildConfig(string Product, string Name);
}
