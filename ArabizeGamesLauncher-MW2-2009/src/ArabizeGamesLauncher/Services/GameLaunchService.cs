using System.Diagnostics;
using System.Text.RegularExpressions;
using ArabizeGamesLauncher.Models;

namespace ArabizeGamesLauncher.Services;

public enum GameLaunchStatus { Started, AlreadyRunning, UnsupportedInstallation, Failed }
public sealed record GameLaunchResult(GameLaunchStatus Status, string Message, string? ExecutablePath = null);

public interface IGameProcessLauncher
{
    bool IsGameRunning();
    void Start(string absoluteExecutablePath, string workingDirectory);
}

public sealed class WindowsGameProcessLauncher : IGameProcessLauncher
{
    public bool IsGameRunning()
    {
        var processes = Process.GetProcessesByName("MW2CR");
        try { return processes.Length > 0; }
        finally { foreach (var process in processes) process.Dispose(); }
    }

    public void Start(string absoluteExecutablePath, string workingDirectory)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = absoluteExecutablePath,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false
        };
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Windows did not return a process handle.");
    }
}

/// <summary>Launches only the executable returned by a fresh exact-build validation.</summary>
public sealed class GameLaunchService(IGameInstallationDetector detector, IGameProcessLauncher processLauncher)
{
    private static readonly object LaunchGate = new();
    public GameLaunchResult LaunchDetectedGame(string installationDirectory)
    {
        GameDetectionResult validated;
        try { validated = detector.Detect(installationDirectory); }
        catch (Exception) { return new(GameLaunchStatus.UnsupportedInstallation, "Exact game build validation failed."); }

        if (!validated.IsDetected || validated.Identity != GameDetectionService.ExpectedProduct ||
            validated.BuildProduct != GameDetectionService.ExpectedBuildProduct || validated.GameVersion != GameDetectionService.ExpectedVersion ||
            validated.BuildName != GameDetectionService.ExpectedBuildName || string.IsNullOrWhiteSpace(validated.ExecutablePath) ||
            !Regex.IsMatch(validated.BuildKey, "^[0-9a-fA-F]{32}$") ||
            !File.Exists(validated.ExecutablePath))
            return new(GameLaunchStatus.UnsupportedInstallation, validated.Message.Length == 0 ? "The exact supported game installation was not detected." : validated.Message);

        var expectedExecutable = Path.GetFullPath(Path.Combine(validated.InstallationPath, "MW2CR.exe"));
        var expectedDirectory = Path.GetFullPath(validated.InstallationPath);
        if (!Path.GetFullPath(validated.ExecutablePath).Equals(expectedExecutable, StringComparison.OrdinalIgnoreCase) ||
            !Path.GetDirectoryName(Path.GetFullPath(validated.ExecutablePath))!.Equals(expectedDirectory, StringComparison.OrdinalIgnoreCase))
            return new(GameLaunchStatus.UnsupportedInstallation, "Validated executable path does not match the MW2CR.exe in the detected installation.");

        try
        {
            lock (LaunchGate)
            {
                if (processLauncher.IsGameRunning())
                    return new(GameLaunchStatus.AlreadyRunning, "An MW2CR process is already running.", expectedExecutable);
                processLauncher.Start(expectedExecutable, expectedDirectory);
            }
            return new(GameLaunchStatus.Started, "MW2CR was started from the verified installation directory.", expectedExecutable);
        }
        catch (Exception ex)
        {
            return new(GameLaunchStatus.Failed, $"Windows could not start the verified MW2CR executable ({ex.GetType().Name}: {ex.Message}).", expectedExecutable);
        }
    }
}
