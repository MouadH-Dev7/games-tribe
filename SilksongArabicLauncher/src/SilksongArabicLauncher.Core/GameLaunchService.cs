using System.Diagnostics;

namespace SilksongArabicLauncher.Core;

public enum GameLaunchStatus { Started, ConfirmationRequired, AlreadyRunning, UnsupportedInstallation, TransactionInvalid, Failed }

public sealed record GameLaunchResult(GameLaunchStatus Status, string Message);

public interface IGameStarter
{
    void Start(string executablePath, string workingDirectory);
}

public sealed class WindowsGameStarter : IGameStarter
{
    public void Start(string executablePath, string workingDirectory)
    {
        var info = new ProcessStartInfo
        {
            FileName = Path.GetFullPath(executablePath),
            WorkingDirectory = Path.GetFullPath(workingDirectory),
            UseShellExecute = false
        };
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Windows did not return a game process handle.");
    }
}

public sealed class GameLaunchService
{
    private readonly TransactionalInstaller _transactions;
    private readonly IGameProcessGuard _processGuard;
    private readonly IGameStarter _starter;
    private readonly ManifestValidator _manifestValidator;

    public GameLaunchService(TransactionalInstaller transactions, IGameProcessGuard processGuard,
        IGameStarter starter, ManifestValidator? manifestValidator = null)
    {
        _transactions = transactions;
        _processGuard = processGuard;
        _starter = starter;
        _manifestValidator = manifestValidator ?? new ManifestValidator();
    }

    public GameLaunchResult Launch(string manifestPath, string gameRoot, string transactionId, bool confirmedUnverifiedRuntime)
    {
        if (!confirmedUnverifiedRuntime)
            return new(GameLaunchStatus.ConfirmationRequired, "Explicit confirmation is required because runtime compatibility has not been confirmed.");
        try
        {
            var manifest = _manifestValidator.Read(manifestPath);
            var issues = GameIdentityValidator.Validate(manifest, gameRoot);
            if (issues.Count != 0) return new(GameLaunchStatus.UnsupportedInstallation, string.Join("; ", issues.Select(i => i.Message)));
            var transaction = _transactions.VerifyInstalledTransaction(transactionId, gameRoot);
            if (!transaction.Success) return new(GameLaunchStatus.TransactionInvalid, transaction.Message);
            var executable = GameIdentityValidator.GetExecutablePath(manifest, gameRoot);
            if (_processGuard.IsRunning(executable)) return new(GameLaunchStatus.AlreadyRunning, "Hollow Knight: Silksong is already running.");
            _starter.Start(executable, Path.GetFullPath(gameRoot));
            return new(GameLaunchStatus.Started, "Game started. Select English in the game for the Arabic package.");
        }
        catch (Exception ex) { return new(GameLaunchStatus.Failed, "Game startup failed: " + ex.Message); }
    }
}
