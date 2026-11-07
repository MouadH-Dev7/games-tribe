using System.IO;
using System.Windows;
using HollowKnightArabicLauncher.Core;
using HollowKnightArabicLauncher.ViewModels;

namespace HollowKnightArabicLauncher;

public partial class App : System.Windows.Application
{
    private const string Phase2ManifestSha256 = "6FCA6C5DF8EAF8848C627FC38CE702DEB2F0EF19511232C427CEC2BDBDE00CF5";

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var manifest = Path.Combine(AppContext.BaseDirectory, "godmaster-package-manifest.json");
        var dataRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HollowKnightArabicLauncher");
        var manifestValidator = new ManifestValidator(Phase2ManifestSha256);
        var transactions = new TransactionalInstaller(dataRoot, validator: manifestValidator);
        var viewModel = new MainViewModel(manifest, transactions, manifestValidator);
        var window = new MainWindow { DataContext = viewModel };
        MainWindow = window;
        window.Show();
        viewModel.Refresh();
    }
}



