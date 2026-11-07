namespace ArabizeGamesLauncher.Services;

public sealed class AppPaths
{
    public string Root { get; }
    public string Config => Path.Combine(Root, "config");
    public string Diagnostics => Path.Combine(Root, "diagnostics");
    public string Packages => Path.Combine(Root, "packages");
    public string Backups => Path.Combine(Root, "backups");
    public string Transactions => Path.Combine(Root, "transactions");
    public string Logs => Path.Combine(Root, "logs");
    public string SettingsFile => Path.Combine(Config, "settings.json");

    public AppPaths(string? rootOverride = null) => Root = string.IsNullOrWhiteSpace(rootOverride) ? ResolveRoot() : Path.GetFullPath(rootOverride);

    public void EnsureCreated()
    {
        foreach (var path in new[] { Config, Diagnostics, Packages, Backups, Transactions, Logs })
            Directory.CreateDirectory(path);
    }

    private static string ResolveRoot()
    {
        if (Environment.GetEnvironmentVariable("ARABIZE_DATA_ROOT") is { Length: > 0 } overridePath)
            return Path.GetFullPath(overridePath);
#if DEBUG
        return Path.Combine(AppContext.BaseDirectory, ".local-data");
#else
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ArabizeGames", "MW2CampaignRemasteredLauncher");
#endif
    }
}
