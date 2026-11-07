using System.IO;
using System.Windows;
using SilksongArabicLauncher.Core;
using SilksongArabicLauncher.ViewModels;

namespace SilksongArabicLauncher;

public partial class App : System.Windows.Application
{
    private const string Phase2ManifestSha256 = "5F5ADC9909360940639E2D4DD35D5CC49557C33851A4C3E81CF228E72DE9E9B4";

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var manifest = Path.Combine(AppContext.BaseDirectory, "phase-2-installation-manifest-proposal.json");
        var dataRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SilksongArabicLauncher");
        var manifestValidator = new ManifestValidator(Phase2ManifestSha256);
        var transactions = new TransactionalInstaller(dataRoot, validator: manifestValidator);
        var viewModel = new MainViewModel(manifest, transactions, manifestValidator);
        var window = new MainWindow { DataContext = viewModel };
        MainWindow = window;
        window.Show();
        viewModel.Refresh();
    }
}
