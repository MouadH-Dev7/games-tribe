using System.ComponentModel;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using MessageBox = System.Windows.MessageBox;
using HollowKnightArabicLauncher.Core;

namespace HollowKnightArabicLauncher.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly string _manifestPath;
    private readonly TransactionalInstaller _transactions;
    private readonly ManifestValidator _validator;
    private readonly GameDiscoveryService _discovery = new();
    private string _gameDirectory = "";
    private string _packageDirectory = "";
    private string _gameStatusText = "";
    private string _packageStatusText = "";
    private string _operationStatus = "";
    private bool _isBusy;
    private Phase2Manifest? _manifest;
    private PreflightResult? _preflight;
    private TransactionManifest? _activeTransaction;
    private bool _activeTransactionValid;
    private bool _userSelectedGameDirectory;
    private IReadOnlyList<RecoverableTransaction> _recoverable = [];
    private IReadOnlyList<string> _supersedableRecoveryIds = [];
    private GameCandidate? _selectedCandidate;

    public MainViewModel(string manifestPath, TransactionalInstaller transactions, ManifestValidator? validator = null)
    {
        _manifestPath = Path.GetFullPath(manifestPath);
        _transactions = transactions;
        _validator = validator ?? new ManifestValidator();
        Texts = new TextCatalog();
        CandidateInstallations = [];
        ValidationIssues = [];
        _gameDirectory = @"C:\Program Files (x86)\Steam\steamapps\common\Hollow Knight";
        _packageDirectory = Path.Combine(AppContext.BaseDirectory, "TranslationPackage");

        RefreshCommand = new RelayCommand(() => Refresh(), () => !IsBusy);
        AutoSearchGameCommand = new RelayCommand(AutoSearchGame, () => !IsBusy);
        InstallCommand = new AsyncRelayCommand(InstallAsync, () => !IsBusy);
        UninstallCommand = new AsyncRelayCommand(UninstallAsync, () => !IsBusy);
        RecoverCommand = new AsyncRelayCommand(RecoverAsync, () => CanRecover);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public TextCatalog Texts { get; }
    public ObservableCollection<GameCandidate> CandidateInstallations { get; }
    public ObservableCollection<string> ValidationIssues { get; }
    public ICommand RefreshCommand { get; }
    public ICommand AutoSearchGameCommand { get; }
    public ICommand InstallCommand { get; }
    public ICommand UninstallCommand { get; }
    public ICommand RecoverCommand { get; }

    public string GameDirectory
    {
        get => _gameDirectory;
        set
        {
            _userSelectedGameDirectory = true;
            if (Set(ref _gameDirectory, value ?? "")) Refresh();
        }
    }
    public string PackageDirectory { get => _packageDirectory; set { if (Set(ref _packageDirectory, value ?? "")) Refresh(); } }
    public GameCandidate? SelectedCandidate
    {
        get => _selectedCandidate;
        set
        {
            if (!Set(ref _selectedCandidate, value)) return;
            if (value is not null && !string.Equals(_gameDirectory, value.Path, StringComparison.OrdinalIgnoreCase))
            {
                _gameDirectory = value.Path;
                OnPropertyChanged(nameof(GameDirectory));
                Refresh();
            }
        }
    }
    public string GameStatusText { get => _gameStatusText; private set => Set(ref _gameStatusText, value); }
    public string PackageStatusText { get => _packageStatusText; private set => Set(ref _packageStatusText, value); }
    public string OperationStatus { get => _operationStatus; private set => Set(ref _operationStatus, value); }
    public bool IsBusy { get => _isBusy; private set { if (Set(ref _isBusy, value)) RefreshCommands(); } }
    public System.Windows.FlowDirection CurrentFlowDirection => Texts.IsArabic ? System.Windows.FlowDirection.RightToLeft : System.Windows.FlowDirection.LeftToRight;
    public bool CanInstall => !IsBusy && _preflight is not null &&
        _preflight.Success &&
        _activeTransaction is null && (_recoverable.Count == 0 || _supersedableRecoveryIds.Count == _recoverable.Count);
    public bool CanUninstall => !IsBusy && _activeTransaction is not null;
    public bool CanRecover => !IsBusy && _recoverable.Count > 0 && _supersedableRecoveryIds.Count == 0;
    public bool IsInstalled => _activeTransaction is not null;
    public string TransactionText => _activeTransaction is null ? Texts["NoActive"] : $"{Texts["Transaction"]}: {_activeTransaction.TransactionId}";
    public bool IsIndeterminate => IsBusy;

    public void SetGameDirectory(string path) => GameDirectory = path;
    public void SetPackageDirectory(string path) => PackageDirectory = path;

    public void ReportUiError(string message)
    {
        OperationStatus = message;
    }

    private void AutoSearchGame()
    {
        if (_manifest is null)
        {
            Refresh();
            if (_manifest is null) return;
        }
        var candidates = _discovery.Discover(_manifest, _gameDirectory, _transactions.GetInstalledGameRoots());
        var valid = candidates.FirstOrDefault(candidate => candidate.Valid);
        var installedTargets = candidates.Where(candidate => candidate.Valid &&
            _transactions.GetInstalledTransactions(candidate.Path).Count > 0).Take(2).ToArray();
        if (installedTargets.Length == 1) valid = installedTargets[0];
        if (valid is null)
        {
            CandidateInstallations.Clear();
            foreach (var candidate in candidates) CandidateInstallations.Add(candidate);
            var reasons = candidates.SelectMany(candidate => candidate.Issues.Select(issue => issue.Message)).Distinct().Take(4);
            GameStatusText = "لم نعثر على تثبيت مطابق في المواقع المعروفة. اختر مجلد اللعبة يدويًا. " + string.Join("؛ ", reasons);
            OperationStatus = "تعذر التحقق من اللعبة في مسارات البحث الحالية.";
            RefreshCommands();
            return;
        }
        _userSelectedGameDirectory = false;
        SelectedCandidate = valid;
    }

    public void Refresh(bool preserveOperationStatus = false)
    {
        if (!File.Exists(_manifestPath))
        {
            _manifest = null;
            _activeTransaction = null;
            _activeTransactionValid = false;
            _recoverable = [];
            _supersedableRecoveryIds = [];
            GameStatusText = Texts["NoGame"];
            PackageStatusText = "Manifest not found: " + _manifestPath;
            _preflight = null;
            RefreshCommands();
            return;
        }
        try
        {
            _manifest = _validator.Read(_manifestPath);
            var candidates = _discovery.Discover(_manifest, _gameDirectory, _transactions.GetInstalledGameRoots());
            CandidateInstallations.Clear();
            foreach (var candidate in candidates) CandidateInstallations.Add(candidate);
            var valid = candidates.Where(c => c.Valid).ToArray();
            var installedTargets = valid.Where(candidate => _transactions.GetInstalledTransactions(candidate.Path).Count > 0).Take(2).ToArray();
            var preferredInstalledTarget = !_userSelectedGameDirectory && installedTargets.Length == 1 ? installedTargets[0] : null;
            if (preferredInstalledTarget is not null)
            {
                _selectedCandidate = preferredInstalledTarget;
                _gameDirectory = preferredInstalledTarget.Path;
                OnPropertyChanged(nameof(SelectedCandidate));
                OnPropertyChanged(nameof(GameDirectory));
                GameStatusText = "عُثر على تثبيت التعريب المسجل: " + preferredInstalledTarget.Path;
            }
            else if (valid.Length == 1)
            {
                _selectedCandidate = valid[0];
                _gameDirectory = valid[0].Path;
                OnPropertyChanged(nameof(SelectedCandidate));
                OnPropertyChanged(nameof(GameDirectory));
                GameStatusText = "تم التحقق من اللعبة: " + valid[0].Path;
            }
            else if (valid.Length > 1)
            {
                var current = valid.FirstOrDefault(c => string.Equals(c.Path, _gameDirectory, StringComparison.OrdinalIgnoreCase));
                _selectedCandidate = current;
                OnPropertyChanged(nameof(SelectedCandidate));
                GameStatusText = current is null ? "عُثر على أكثر من تثبيت مطابق؛ اختر مجلد اللعبة." : "المجلد المحدد: " + current.Path;
            }
            else
            {
                _selectedCandidate = null;
                OnPropertyChanged(nameof(SelectedCandidate));
                var issues = candidates.FirstOrDefault(c => string.Equals(c.Path, _gameDirectory, StringComparison.OrdinalIgnoreCase))?.Issues;
                GameStatusText = issues is { Count: > 0 }
                    ? $"{Texts["NoGame"]} توجد {issues.Count} ملاحظات؛ اختر مجلد اللعبة الصحيح."
                    : Texts["NoGame"];
            }

            var root = _gameDirectory;
            _recoverable = Directory.Exists(root) ? _transactions.FindRecoverable(root) : [];
            var installed = Directory.Exists(root) ? _transactions.GetInstalledTransactions(root) : [];
            _activeTransaction = installed.FirstOrDefault();
            _activeTransactionValid = _activeTransaction is not null && _transactions.VerifyInstalledTransaction(_activeTransaction.TransactionId, root).Success;
            if (_activeTransaction is not null)
            {
                PackageStatusText = _activeTransactionValid ? GetInstalledPackageStatus(_activeTransaction) : "Installed transaction detected, but files changed; launch is blocked and safe removal can still be attempted.";
                _preflight = null;
                ValidationIssues.Clear();
            }
            else
            {
                _preflight = Directory.Exists(root) ? _transactions.Inspect(_manifestPath, _packageDirectory, root) : null;
                PackageStatusText = _preflight is null ? Texts["NoPackage"] :
                    _preflight.ExistingFileConflicts.Count > 0
                        ? $"يوجد {_preflight.ExistingFileConflicts.Count} ملفًا في مسارات الحزمة؛ لن يستبدلها اللانشر. أزل التعارض يدوياً بعد التحقق منه."
                    :
                    _preflight.Success ? $"الحزمة جاهزة: اجتاز {_preflight.InstallFiles.Count} ملفًا التحقق. التوافق مع هذا الإصدار غير مؤكد." :
                    _preflight.CanProceedAfterBackingUpConflicts
                        ? $"الحزمة جاهزة. سيُحفظ { _preflight.ExistingFileConflicts.Count } ملفًا متعارضًا في نسخة احتياطية قبل التثبيت."
                        : $"الحزمة غير جاهزة للتثبيت. راجع ملفاتها أو أعد اختيار مجلد اللعبة. ({_preflight.Issues.Count} ملاحظات)";
                ValidationIssues.Clear();
                if (_preflight is not null)
                    foreach (var issue in _preflight.Issues) ValidationIssues.Add($"{issue.Code}: {issue.Message}{(issue.Path is null ? "" : " — " + issue.Path)}");
            }
            _supersedableRecoveryIds = _preflight is not null && _preflight.CanProceedAfterBackingUpConflicts
                ? _transactions.GetSupersedableRecoverableTransactionIds(root, _preflight.ExistingFileConflicts)
                : [];
            if (!preserveOperationStatus)
            {
                if (_recoverable.Count > 0 && _supersedableRecoveryIds.Count == _recoverable.Count)
                    OperationStatus = "سيُحفظ الملف المتبقي من التثبيت السابق ضمن النسخة الاحتياطية عند المتابعة.";
                else if (_recoverable.Count > 0) OperationStatus = $"توجد {_recoverable.Count} عملية تثبيت متوقفة وتحتاج إلى استعادة.";
                else if (_activeTransaction is not null) OperationStatus = _activeTransactionValid ? Texts["Installed"] : Texts["Manual"];
                else OperationStatus = _preflight?.Success == true ? Texts["Ready"] : Texts["InstallNotReady"];
            }
            OnPropertyChanged(nameof(IsInstalled));
            OnPropertyChanged(nameof(TransactionText));
            RefreshCommands();
        }
        catch (Exception)
        {
            _preflight = null;
            _activeTransaction = null;
            _activeTransactionValid = false;
            _supersedableRecoveryIds = [];
            ValidationIssues.Clear();
            GameStatusText = Texts["NoGame"];
            PackageStatusText = "تعذر التحقق من ملفات التعريب. أعد فتح اللانشر أو تحقق من ملفات الحزمة.";
            if (!preserveOperationStatus) OperationStatus = Texts["InstallNotReady"];
            RefreshCommands();
        }
    }

    private async Task InstallAsync()
    {
        if (IsBusy) return;
        if (!CanInstall || _manifest is null)
        {
            OperationStatus = _manifest is null
                ? "تعذر تحميل بيانات الحزمة. أعد تشغيل اللانشر."
                : _activeTransaction is not null
                    ? "التعريب مثبت بالفعل. استخدم زر الاستعادة أولًا."
                    : _recoverable.Count > 0 && _supersedableRecoveryIds.Count != _recoverable.Count
                        ? "توجد عملية سابقة غير مكتملة. استخدم الاستعادة قبل التثبيت."
                        : _preflight is null
                            ? "اختر مجلد اللعبة الصحيح أولًا."
                            : "التثبيت متوقف لحماية ملفاتك. تحقق من حالة الحزمة واللعبة أعلاه.";
            return;
        }
        IsBusy = true;
        OperationStatus = Texts["Busy"];
        try
        {
            var result = await Task.Run(() => _transactions.Install(_manifestPath, PackageDirectory, GameDirectory,
                confirmUnsignedRuntime: true, confirmExistingFilesBackup: false));
            OperationStatus = result.Success ? result.Message : FormatInstallResult(result);
        }
        catch (Exception ex) { OperationStatus = ex.Message; }
        finally { IsBusy = false; Refresh(preserveOperationStatus: true); }
    }

    private async Task UninstallAsync()
    {
        if (IsBusy) return;
        if (!CanUninstall || _activeTransaction is null)
        {
            OperationStatus = "لا يوجد تثبيت مسجل يمكن استعادته بأمان.";
            return;
        }
        if (MessageBox.Show(Texts["UninstallConfirm"], Texts["UninstallConfirmTitle"], MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        { return; }
        var id = _activeTransaction.TransactionId;
        IsBusy = true;
        try
        {
            var result = await Task.Run(() => _transactions.Uninstall(id, GameDirectory));
            OperationStatus = FormatUninstallResult(result);
        }
        catch (Exception ex) { OperationStatus = ex.Message; }
        finally { IsBusy = false; Refresh(preserveOperationStatus: true); }
    }

    private async Task RecoverAsync()
    {
        if (!CanRecover) return;
        if (MessageBox.Show(Texts["RecoverConfirm"], Texts["RecoverConfirmTitle"], MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        { return; }
        IsBusy = true;
        try
        {
            var result = await Task.Run(() => _transactions.RecoverInterrupted(GameDirectory));
            OperationStatus = result.Message;
        }
        catch (Exception ex) { OperationStatus = ex.Message; }
        finally { IsBusy = false; Refresh(preserveOperationStatus: true); }
    }

    private static string FormatUninstallResult(OperationResult result)
    {
        if (result.Success) return "تمت إزالة التعريب واستعادة الملفات الأصلية.";
        if (result.Message.Contains("Close Hollow Knight", StringComparison.OrdinalIgnoreCase))
            return "أغلق اللعبة بالكامل أولًا، ثم اضغط زر إزالة التعريب مرة أخرى.";
        if (result.ManualFiles.Count > 0)
            return $"توقفت الإزالة لحماية {result.ManualFiles.Count} ملفًا تغيّر بعد التثبيت. لم تُحذف هذه الملفات.";
        return "لم تكتمل الإزالة. لم تُحذف الملفات التي تعذّر التحقق منها؛ راجع حالة التعريب أو أعد فتح اللانشر.";
    }

    private static string FormatInstallResult(OperationResult result)
    {
        if (result.Message.Contains("UnauthorizedAccessException", StringComparison.OrdinalIgnoreCase) ||
            result.Message.Contains("access to the path", StringComparison.OrdinalIgnoreCase) ||
            result.Message.Contains("access is denied", StringComparison.OrdinalIgnoreCase))
            return "تعذر الكتابة إلى مجلد اللعبة. أعد فتح اللانشر ووافق على طلب صلاحية المسؤول، ثم حاول مجددًا.";
        if (result.Message.Contains("Close Hollow Knight", StringComparison.OrdinalIgnoreCase))
            return "أغلق اللعبة بالكامل أولًا، ثم اضغط زر تثبيت التعريب مرة أخرى.";
        if (result.Message.Contains("rollback needs manual recovery", StringComparison.OrdinalIgnoreCase))
            return "فشل التثبيت ويحتاج الاسترجاع إلى مراجعة. لا تعاود التثبيت؛ استخدم زر الاستعادة.";
        return "فشل التثبيت وتمت استعادة الملفات الأصلية. أعد المحاولة؛ وإذا تكرر الخطأ فأرسل هذه الرسالة: " + result.Message;
    }

    private static string GetInstalledPackageStatus(TransactionManifest transaction)
    {
        const string defaultStatus = "تم تثبيت الملفات. اختر English من إعدادات اللغة داخل اللعبة لعرض التعريب.";
        try
        {
            var gameLog = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "AppData", "LocalLow", "Team Cherry", "Hollow Knight", "output_log.txt");
            var info = new FileInfo(gameLog);
            if (!info.Exists || info.Length <= 0 || info.Length > 8 * 1024 * 1024 || info.LastWriteTimeUtc < transaction.CreatedAtUtc.UtcDateTime)
                return defaultStatus;
            var log = File.ReadAllText(gameLog);
            if (log.Contains("TypeLoadException", StringComparison.OrdinalIgnoreCase) &&
                log.Contains("SnakeArabicPlugin.Awake", StringComparison.OrdinalIgnoreCase))
                return "ملفات التعريب مثبتة، لكن الإضافة توقفت بسبب عدم توافقها مع إصدار اللعبة.";
            if (log.Contains("Loaded saved language code 'FR'", StringComparison.OrdinalIgnoreCase) ||
                log.Contains("Restored language code 'FR'", StringComparison.OrdinalIgnoreCase))
                return "اختر English من إعدادات اللغة داخل اللعبة لتفعيل التعريب.";
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return defaultStatus;
    }

    private void RefreshCommands()
    {
        (RefreshCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (AutoSearchGameCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (InstallCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (UninstallCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (RecoverCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(CanInstall));
        OnPropertyChanged(nameof(CanUninstall));
        OnPropertyChanged(nameof(CanRecover));
        OnPropertyChanged(nameof(IsIndeterminate));
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

