using ArabizeGamesLauncher.Services;
using ArabizeGamesLauncher.ViewModels;
using System.Windows;

namespace ArabizeGamesLauncher;

public partial class App : Application
{
    private async void OnStartup(object sender, StartupEventArgs e)
    {
        var paths = new AppPaths();
        paths.EnsureCreated();
        var logger = new StructuredLogger(paths);
        var settingsStore = new SettingsStore(paths);
        var settings = settingsStore.Load();
        var detector = new GameDetectionService();
        var packages = new PackageService();
        var backupManager = new BackupManager();
        var registryInspection = new RegistryLanguageInspectionService(new WindowsRegistryLanguageReader());
        var registryMutation = new WindowsRegistryMutationAdapter();
        var registryTransaction = new RegistryTransactionService(registryMutation);
        var registrySnapshots = new RegistrySnapshotStore(paths);
        var gameLaunch = new GameLaunchService(detector, new WindowsGameProcessLauncher());
        var viewModel = new MainViewModel(paths, logger, settingsStore, settings, detector, packages, backupManager,
            registryInspection, registryTransaction, registrySnapshots, gameLaunchService: gameLaunch);
        var window = new MainWindow { DataContext = viewModel };
        MainWindow = window;
        window.Show();
        await viewModel.InitializeAsync();
    }
}
