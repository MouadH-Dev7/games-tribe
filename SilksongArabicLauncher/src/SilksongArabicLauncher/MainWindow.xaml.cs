using System.IO;
using System.Windows;
using System.Windows.Interop;
using Forms = System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;
using SilksongArabicLauncher.ViewModels;

namespace SilksongArabicLauncher;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    private void BrowseGame_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        try
        {
            using var dialog = new Forms.FolderBrowserDialog
            {
                Description = "اختر مجلد اللعبة الذي يحتوي على Hollow Knight Silksong.exe",
                UseDescriptionForTitle = true,
                SelectedPath = Directory.Exists(vm.GameDirectory) ? vm.GameDirectory : @"C:\Games",
                ShowNewFolderButton = false
            };
            if (dialog.ShowDialog(new WindowOwner(new WindowInteropHelper(this).Handle)) == Forms.DialogResult.OK)
                vm.SetGameDirectory(dialog.SelectedPath);
        }
        catch (Exception ex)
        {
            vm.ReportUiError("تعذر فتح نافذة اختيار مجلد اللعبة: " + ex.Message);
            MessageBox.Show(this, "تعذر فتح نافذة اختيار المجلد.\n" + ex.Message, "Arabify", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private sealed class WindowOwner(IntPtr handle) : Forms.IWin32Window { public IntPtr Handle => handle; }
}
