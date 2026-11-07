using System.Text.Json;
using ArabizeGamesLauncher.Models;

namespace ArabizeGamesLauncher.Services;

public sealed class SettingsStore(AppPaths paths)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public LauncherSettings Load()
    {
        try
        {
            if (File.Exists(paths.SettingsFile))
                return JsonSerializer.Deserialize<LauncherSettings>(File.ReadAllText(paths.SettingsFile)) ?? new();
        }
        catch { }
        return new();
    }

    public void Save(LauncherSettings settings)
    {
        Directory.CreateDirectory(paths.Config);
        var temporary = paths.SettingsFile + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(settings, JsonOptions));
        File.Move(temporary, paths.SettingsFile, true);
    }
}
