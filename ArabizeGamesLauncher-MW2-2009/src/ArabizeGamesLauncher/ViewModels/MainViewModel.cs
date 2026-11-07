using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using ArabizeGamesLauncher.Infrastructure;
using ArabizeGamesLauncher.Models;
using ArabizeGamesLauncher.Services;
using Microsoft.Win32;

namespace ArabizeGamesLauncher.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly AppPaths _paths;
    private readonly StructuredLogger _logger;
    private readonly SettingsStore _settingsStore;
    private readonly LauncherSettings _settings;
    private readonly GameDetectionService _detector;
    private readonly PackageService _packageService;
    private readonly BackupManager _backupManager;
    private readonly RegistryLanguageInspectionService? _registryInspectionService;
    private readonly RegistryTransactionService? _registryTransactionService;
    private readonly IRegistrySnapshotStore? _registrySnapshotStore;
    private readonly Func<string, bool> _confirmRegistryApply;
    private readonly Func<string, bool> _confirmRegistryRestore;
    private readonly GameLaunchService? _gameLaunchService;
    private string _gameDirectory;
    private string _packageDirectory;
    private string _currentPage = "Dashboard";
    private string _selectedGameLanguage = "ar";
    private RegistryLanguageInspection? _registryInspection;
    private bool _registryTransactionRunning;
    private bool _backupCreationBlocked;
    private string _registryBackupStatus = string.Empty;
    private string _registryTransactionProgress = string.Empty;
    private string _registryTransactionDetails = string.Empty;
    private string _registryRollbackStatus = string.Empty;
    private string _restoreStatus = string.Empty;
    private string _launchStatus = string.Empty;
    private bool _restoreRunning;
    private bool _launchRunning;
    private ValidatedRegistrySnapshot? _selectedRegistrySnapshot;
    private GameDetectionResult _game = new();
    private PackageEntry? _selectedPackage;
    private ReportEntry? _selectedReport;

    public MainViewModel(AppPaths paths, StructuredLogger logger, SettingsStore settingsStore, LauncherSettings settings,
        GameDetectionService detector, PackageService packageService, BackupManager backupManager,
        RegistryLanguageInspectionService? registryInspectionService = null,
        RegistryTransactionService? registryTransactionService = null,
        IRegistrySnapshotStore? registrySnapshotStore = null,
        Func<string, bool>? confirmRegistryApply = null,
        Func<string, bool>? confirmRegistryRestore = null,
        GameLaunchService? gameLaunchService = null)
    {
        _paths = paths;
        _logger = logger;
        _settingsStore = settingsStore;
        _settings = settings;
        _detector = detector;
        _packageService = packageService;
        _backupManager = backupManager;
        _registryInspectionService = registryInspectionService;
        _registryTransactionService = registryTransactionService;
        _registrySnapshotStore = registrySnapshotStore;
        _confirmRegistryApply = confirmRegistryApply ?? ConfirmRegistryApply;
        _confirmRegistryRestore = confirmRegistryRestore ?? ConfirmRegistryRestore;
        _gameLaunchService = gameLaunchService;
        _gameDirectory = string.IsNullOrWhiteSpace(settings.GameDirectory) ? GameDetectionService.DefaultDirectory : settings.GameDirectory;
        _packageDirectory = string.IsNullOrWhiteSpace(settings.PackageDirectory) ? paths.Packages : settings.PackageDirectory;
        Texts.SetLanguage(settings.UiLanguage);

        NavigateCommand = new RelayCommand<string>(page => CurrentPage = page ?? "Dashboard");
        BrowseGameCommand = new RelayCommand(BrowseGameFolder);
        ValidateGameCommand = new RelayCommand(ValidateGame);
        BrowsePackageCommand = new RelayCommand(BrowsePackageFolder);
        RefreshPackagesCommand = new RelayCommand(RefreshPackages);
        GenerateReportCommand = new RelayCommand(GenerateDiagnosticReport);
        OpenReportsCommand = new RelayCommand(() => OpenFolder(_paths.Diagnostics));
        OpenPackagesCommand = new RelayCommand(() => OpenFolder(_packageDirectory));
        SetArabicCommand = new RelayCommand(() => SetLanguage("ar"));
        SetEnglishCommand = new RelayCommand(() => SetLanguage("en"));
        SelectArabicGameLanguageCommand = new RelayCommand(() => SelectedGameLanguage = "ar");
        SelectEnglishGameLanguageCommand = new RelayCommand(() => SelectedGameLanguage = "en");
        ApplyGameLanguageCommand = new RelayCommand(ApplySelectedGameLanguage, () => CanApplyGameLanguage);
        RestorePreviousSettingsCommand = new RelayCommand(RestoreSelectedSnapshot, () => CanRestorePreviousSettings);
        LaunchGameCommand = new RelayCommand(LaunchVerifiedGame, () => CanLaunchGame);
        RefreshRegistrySnapshotsCommand = new RelayCommand(RefreshRegistrySnapshots, () => _registrySnapshotStore is not null && Game.IsDetected);
        InspectRegistryCommand = new RelayCommand(InspectRegistrySettings, () => _registryInspectionService is not null);
        RefreshActivityCommand = new RelayCommand(RefreshActivity);
    }

    public TextCatalog Texts { get; } = new();
    public ObservableCollection<PackageEntry> Packages { get; } = [];
    public ObservableCollection<InstallationTransactionRecord> Transactions { get; } = [];
    public ObservableCollection<ReportEntry> Reports { get; } = [];
    public ObservableCollection<LogEntry> Activity { get; } = [];
    public RelayCommand<string> NavigateCommand { get; }
    public RelayCommand BrowseGameCommand { get; }
    public RelayCommand ValidateGameCommand { get; }
    public RelayCommand BrowsePackageCommand { get; }
    public RelayCommand RefreshPackagesCommand { get; }
    public RelayCommand GenerateReportCommand { get; }
    public RelayCommand OpenReportsCommand { get; }
    public RelayCommand OpenPackagesCommand { get; }
    public RelayCommand SetArabicCommand { get; }
    public RelayCommand SetEnglishCommand { get; }
    public RelayCommand SelectArabicGameLanguageCommand { get; }
    public RelayCommand SelectEnglishGameLanguageCommand { get; }
    public RelayCommand ApplyGameLanguageCommand { get; }
    public RelayCommand RestorePreviousSettingsCommand { get; }
    public RelayCommand LaunchGameCommand { get; }
    public RelayCommand RefreshRegistrySnapshotsCommand { get; }
    public RelayCommand InspectRegistryCommand { get; }
    public RelayCommand RefreshActivityCommand { get; }

    public string CurrentPage { get => _currentPage; set { if (SetProperty(ref _currentPage, value)) OnPropertyChanged(nameof(CurrentPageTitle)); } }
    public string CurrentPageTitle => Texts[CurrentPage];
    public string GameDirectory
    {
        get => _gameDirectory;
        set { if (SetProperty(ref _gameDirectory, value)) { _settings.GameDirectory = value; _settingsStore.Save(_settings); Game = new() { Status = GameDetectionStatus.NotFound, InstallationPath = value, Message = "Validate the selected installation before using language or launch actions." }; } }
    }
    public string PackageDirectory
    {
        get => _packageDirectory;
        set { if (SetProperty(ref _packageDirectory, value)) { _settings.PackageDirectory = value; _settingsStore.Save(_settings); } }
    }
    public string SelectedGameLanguage
    {
        get => _selectedGameLanguage;
        set { if ((value is "ar" or "en") && SetProperty(ref _selectedGameLanguage, value)) { OnPropertyChanged(nameof(SelectedGameLanguageText)); OnPropertyChanged(nameof(TargetRegistryValuesText)); RefreshApplyCommand(); } }
    }
    public string SelectedGameLanguageText => Texts[SelectedGameLanguage == "ar" ? "GameLanguageArabic" : "GameLanguageEnglish"];
    public string GameBuildNameText => string.IsNullOrEmpty(Game.BuildName) ? Texts["UnknownVersion"] : Game.BuildName;
    public string GameCompatibilityText => Game.IsDetected ? Texts["GameCompatibilityReady"] : Game.Message;
    public string CurrentGameLanguageText => Texts["NotInspected"];
    public string RegistryConfigurationText => _registryInspection is null ? Texts["RegistryNotInspected"] : Texts[_registryInspection.Classification switch
    {
        RegistryLanguageClassification.ArabicPair => "ClassificationArabic",
        RegistryLanguageClassification.EnglishPair => "ClassificationEnglish",
        RegistryLanguageClassification.Mixed => "ClassificationMixed",
        RegistryLanguageClassification.Missing => "ClassificationMissing",
        RegistryLanguageClassification.Unsupported => "ClassificationUnsupported",
        _ => "ClassificationUnknown"
    }];
    public string RegistryInspectionTimeText => _registryInspection is null ? Texts["InspectionNotRun"] : _registryInspection.InspectedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture);
    public string RegistryInspectionErrorText => _registryInspection?.Error ?? string.Empty;
    public string RegistryLocaleStatusText => ValueStatusText(_registryInspection?.Locale);
    public string RegistryLocaleAudioStatusText => ValueStatusText(_registryInspection?.LocaleAudio);
    public string RegistryLocaleDataText => _registryInspection?.Locale.Data ?? "—";
    public string RegistryLocaleAudioDataText => _registryInspection?.LocaleAudio.Data ?? "—";
    public string RegistryLocaleKindText => _registryInspection?.Locale.Kind ?? "—";
    public string RegistryLocaleAudioKindText => _registryInspection?.LocaleAudio.Kind ?? "—";
    public string TargetRegistryValuesText => $"LOCALE={SelectedTargetLocale}; LOCALE_AUDIO={SelectedTargetLocale}";
    public string SelectedTargetLocale => SelectedGameLanguage == "ar" ? RegistryLanguageScope.ArabicLocale : RegistryLanguageScope.EnglishLocale;
    public string RegistryBackupStatusText => string.IsNullOrWhiteSpace(_registryBackupStatus) ? Texts["BackupNotCreated"] : _registryBackupStatus;
    public string RegistryTransactionProgressText => string.IsNullOrWhiteSpace(_registryTransactionProgress) ? Texts["TransactionIdle"] : _registryTransactionProgress;
    public string RegistryTransactionDetailsText => string.IsNullOrWhiteSpace(_registryTransactionDetails) ? Texts["TransactionNoResults"] : _registryTransactionDetails;
    public string RegistryRollbackStatusText => string.IsNullOrWhiteSpace(_registryRollbackStatus) ? Texts["RollbackNotNeeded"] : _registryRollbackStatus;
    public bool CanApplyGameLanguage => CanApplyWithoutRunning && !_registryTransactionRunning;
    public string ApplyLanguageStatusText => GetApplyStatusText();
    public ObservableCollection<ValidatedRegistrySnapshot> RegistrySnapshots { get; } = [];
    public ValidatedRegistrySnapshot? SelectedRegistrySnapshot
    {
        get => _selectedRegistrySnapshot;
        set { if (SetProperty(ref _selectedRegistrySnapshot, value)) { OnPropertyChanged(nameof(SelectedSnapshotDetailsText)); RefreshRestoreCommand(); } }
    }
    public string SnapshotListStatusText { get; private set; } = string.Empty;
    public string SelectedSnapshotDetailsText => _selectedRegistrySnapshot is null ? Texts["NoSnapshotSelected"] : FormatSnapshot(_selectedRegistrySnapshot.Envelope.Snapshot);
    public string RestoreStatusText => string.IsNullOrWhiteSpace(_restoreStatus) ? Texts["RestoreIdle"] : _restoreStatus;
    public string RestoreReadinessText => _restoreRunning ? Texts["RestoreRunning"] : !Game.IsDetected ? Texts["UnsupportedInstallation"] :
        _registrySnapshotStore is null || _registryTransactionService is null ? Texts["RestoreUnavailable"] :
        _registryInspection is null ? Texts["RestoreInspectFirst"] : _registryInspection.KeyStatus != RegistryKeyReadStatus.Success ? Texts["RestoreKeyUnavailable"] :
        !CanInspectForRestore(_registryInspection) ? Texts["RestoreInspectionFailed"] : _selectedRegistrySnapshot is null ? Texts["NoValidSnapshots"] : Texts["RestoreReady"];
    public bool CanRestorePreviousSettings => !_restoreRunning && !_registryTransactionRunning && Game.IsDetected && _registryInspection is not null &&
        _registryInspection.KeyStatus == RegistryKeyReadStatus.Success &&
        (_registryInspection.Locale.Status is RegistryValueReadStatus.PresentString or RegistryValueReadStatus.UnsupportedType or RegistryValueReadStatus.Missing or RegistryValueReadStatus.MissingKey) &&
        (_registryInspection.LocaleAudio.Status is RegistryValueReadStatus.PresentString or RegistryValueReadStatus.UnsupportedType or RegistryValueReadStatus.Missing or RegistryValueReadStatus.MissingKey) &&
        _selectedRegistrySnapshot is not null && _registrySnapshotStore is not null && _registryTransactionService is not null;
    public bool CanLaunchGame => !_launchRunning && !_restoreRunning && !_registryTransactionRunning && Game.IsDetected && File.Exists(Path.Combine(Game.InstallationPath, "MW2CR.exe")) && _gameLaunchService is not null;
    public string LaunchStatusText => string.IsNullOrWhiteSpace(_launchStatus) ? GetLaunchReadinessText() : _launchStatus;
    public GameDetectionResult Game { get => _game; private set { if (SetProperty(ref _game, value)) { OnPropertyChanged(nameof(GameStatusText)); OnPropertyChanged(nameof(GameMessage)); OnPropertyChanged(nameof(GameBuildText)); OnPropertyChanged(nameof(GameIdentityText)); OnPropertyChanged(nameof(GameEvidence)); OnPropertyChanged(nameof(PackageStatusText)); OnPropertyChanged(nameof(GameBuildNameText)); OnPropertyChanged(nameof(GameCompatibilityText)); OnPropertyChanged(nameof(CanLaunchGame)); OnPropertyChanged(nameof(LaunchStatusText)); OnPropertyChanged(nameof(CanRestorePreviousSettings)); RefreshApplyCommand(); RefreshRestoreCommand(); RefreshLaunchCommand(); RefreshRegistrySnapshots(); if (RefreshRegistrySnapshotsCommand is not null) RefreshRegistrySnapshotsCommand.RaiseCanExecuteChanged(); } } }
    public IReadOnlyList<EvidenceItem> GameEvidence => Game.Evidence.Select(item => new EvidenceItem(Texts.EvidenceLabel(item.Label), item.Value, item.IsPositive)).ToArray();
    public PackageEntry? SelectedPackage { get => _selectedPackage; set { if (SetProperty(ref _selectedPackage, value)) { OnPropertyChanged(nameof(PackageDetailsText)); OnPropertyChanged(nameof(SelectedPackageStatusText)); } } }
    public ReportEntry? SelectedReport { get => _selectedReport; set => SetProperty(ref _selectedReport, value); }
    public string PackageStatusText => Packages.Any(p => p.Status == PackageStatus.Validated) ? Texts["StatusValidated"] : Texts["NoPackage"];
    public string GameStatusText => Game.Status switch
    {
        GameDetectionStatus.Detected => Texts["Detected"],
        GameDetectionStatus.Unsupported => Texts["Unsupported"],
        _ => Texts["NotDetected"]
    };
    public string GameMessage => Game.Status switch
    {
        GameDetectionStatus.Detected => Texts["DetectionFound"],
        GameDetectionStatus.Unsupported => Texts["DetectionUnsupported"],
        _ => Texts["DetectionMissing"]
    };
    public string GameBuildText => string.IsNullOrEmpty(Game.GameVersion) ? Texts["UnknownVersion"] : Game.GameVersion;
    public string GameIdentityText => string.IsNullOrEmpty(Game.Identity) ? Texts["GameName"] : Game.Identity;
    public string PackageDetailsText => SelectedPackage is null ? Texts["NoSelection"] : SelectedPackage.StatusMessage;
    public string SelectedPackageStatusText => SelectedPackage is null ? Texts["StatusNotAvailable"] : SelectedPackage.Status switch
    {
        PackageStatus.Validated => Texts["StatusValidated"], PackageStatus.Imported => Texts["StatusImported"],
        PackageStatus.Incompatible => Texts["StatusIncompatible"], PackageStatus.ReadyForInstallation => Texts["StatusReady"],
        PackageStatus.Installed => Texts["StatusInstalled"], _ => Texts["StatusNotAvailable"]
    };
    public FlowDirection WindowFlowDirection => Texts.Language == "ar" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
    public double InstallProgress => 0;
    public bool CanLaunch => CanLaunchGame;
    public string LaunchBlockReason => LaunchStatusText;

    public async Task InitializeAsync()
    {
        try
        {
            _paths.EnsureCreated();
            ValidateGame();
            RefreshPackages();
            LoadTransactions();
            LoadReports();
            RefreshActivity();
            _logger.Write("Information", "Startup", "Launcher initialized in non-destructive mode.");
            RefreshActivity();
        }
        catch (Exception ex)
        {
            _logger.Write("Error", "Startup", "Initialization encountered an error.", ex);
            MessageBox.Show(ex.Message, "Arabize Games", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        await Task.CompletedTask;
    }

    private void BrowseGameFolder()
    {
        var dialog = new OpenFolderDialog { Title = Texts["SelectFolder"], InitialDirectory = Directory.Exists(GameDirectory) ? GameDirectory : Environment.GetFolderPath(Environment.SpecialFolder.Desktop) };
        if (dialog.ShowDialog() == true) { GameDirectory = dialog.FolderName; ValidateGame(); }
    }

    private void BrowsePackageFolder()
    {
        var dialog = new OpenFolderDialog { Title = Texts["PackageLibrary"], InitialDirectory = Directory.Exists(PackageDirectory) ? PackageDirectory : _paths.Packages };
        if (dialog.ShowDialog() == true) { PackageDirectory = dialog.FolderName; RefreshPackages(); }
    }

    private void ValidateGame()
    {
        try
        {
            Game = _detector.Detect(GameDirectory);
            _settings.GameDirectory = GameDirectory;
            _settingsStore.Save(_settings);
            _logger.Write(Game.IsDetected ? "Information" : "Warning", "Detection", Game.Message);
            RefreshActivity();
            RefreshPackages();
        }
        catch (Exception ex)
        {
            Game = new() { Status = GameDetectionStatus.Unsupported, InstallationPath = GameDirectory, Message = ex.Message };
            _logger.Write("Error", "Detection", "Game validation failed.", ex);
            RefreshActivity();
        }
    }

    private void InspectRegistrySettings()
    {
        if (_registryInspectionService is null) return;
        try
        {
            _registryInspection = _registryInspectionService.Inspect();
            _logger.Write("Information", "RegistryInspection", $"Explicit read-only inspection completed: {_registryInspection.Classification}; key status {_registryInspection.KeyStatus}; value statuses {_registryInspection.Locale.Status}/{_registryInspection.LocaleAudio.Status}.");
        }
        catch (Exception ex)
        {
            var failed = new RegistryValueInspection(RegistryLanguageScope.LocaleName, RegistryValueReadStatus.UnexpectedError, false, null, null, "Registry inspection failed.");
            var failedAudio = new RegistryValueInspection(RegistryLanguageScope.LocaleAudioName, RegistryValueReadStatus.UnexpectedError, false, null, null, "Registry inspection failed.");
            _registryInspection = new(RegistryKeyReadStatus.UnexpectedError, failed, failedAudio, RegistryLanguageClassification.Unknown, DateTimeOffset.UtcNow, "Registry inspection failed: " + ex.Message);
            _logger.Write("Warning", "RegistryInspection", "Explicit read-only inspection failed; value data was omitted from the log.");
        }
        NotifyRegistryInspectionChanged();
    }

    private void RefreshRegistrySnapshots()
    {
        if (_registrySnapshotStore is null || !Game.IsDetected)
        {
            RegistrySnapshots.Clear();
            SelectedRegistrySnapshot = null;
            SnapshotListStatusText = Texts["NoValidSnapshots"];
            OnPropertyChanged(nameof(SnapshotListStatusText));
            RefreshRestoreCommand();
            return;
        }
        try
        {
            var previousId = SelectedRegistrySnapshot?.Envelope.Snapshot.TransactionId;
            RegistrySnapshots.Clear();
            foreach (var snapshot in _registrySnapshotStore.ListValidSnapshots(ToRegistryGameIdentity(Game))) RegistrySnapshots.Add(snapshot);
            SelectedRegistrySnapshot = RegistrySnapshots.FirstOrDefault(item => item.Envelope.Snapshot.TransactionId == previousId) ?? RegistrySnapshots.FirstOrDefault();
            SnapshotListStatusText = RegistrySnapshots.Count == 0
                ? Texts["NoValidSnapshots"]
                : string.Format(CultureInfo.CurrentCulture, Texts["ValidSnapshotCount"], RegistrySnapshots.Count);
        }
        catch (Exception)
        {
            RegistrySnapshots.Clear();
            SelectedRegistrySnapshot = null;
            SnapshotListStatusText = Texts["SnapshotListFailed"];
        }
        OnPropertyChanged(nameof(SnapshotListStatusText));
        RefreshRestoreCommand();
    }

    private void RestoreSelectedSnapshot()
    {
        if (!CanRestorePreviousSettings || _registryInspectionService is null || _registrySnapshotStore is null || _registryTransactionService is null || SelectedRegistrySnapshot is null) return;
        _restoreRunning = true;
        _restoreStatus = Texts["RestoreRevalidating"];
        _registryTransactionDetails = string.Empty;
        RefreshRestoreCommand();
        NotifyTransactionChanged();
        RegistrySnapshotEnvelope? verifiedRecovery = null;
        string recoveryPath = string.Empty;
        var restorationStarted = false;
        try
        {
            Game = _detector.Detect(GameDirectory);
            if (!Game.IsDetected)
            {
                _restoreStatus = Texts["UnsupportedInstallation"];
                return;
            }
            _registryInspection = _registryInspectionService.Inspect();
            NotifyRegistryInspectionChanged();
            if (!CanInspectForRestore(_registryInspection))
            {
                _restoreStatus = Texts["RestoreInspectionFailed"];
                return;
            }

            var selectedPath = SelectedRegistrySnapshot.Path;
            var selected = _registrySnapshotStore.LoadAndVerify(selectedPath);
            if (selected.Snapshot.Game != ToRegistryGameIdentity(Game) ||
                selected.Snapshot.TransactionId != SelectedRegistrySnapshot.Envelope.Snapshot.TransactionId)
            {
                _restoreStatus = Texts["SnapshotIncompatible"];
                RefreshRegistrySnapshots();
                return;
            }
            SelectedRegistrySnapshot = new(selectedPath, selected);
            _restoreStatus = Texts["SnapshotVerified"];
            var currentPair = $"LOCALE={FormatCurrentValue(_registryInspection.Locale)}; LOCALE_AUDIO={FormatCurrentValue(_registryInspection.LocaleAudio)}";
            var prompt = string.Format(CultureInfo.CurrentCulture, Texts["ConfirmRestorePrompt"], selected.Snapshot.TransactionId, currentPair, FormatSnapshot(selected.Snapshot));
            if (!_confirmRegistryRestore(prompt))
            {
                _restoreStatus = Texts["RestoreCancelled"];
                return;
            }

            _restoreStatus = Texts["RecoverySnapshotCreating"];
            NotifyRestoreChanged();
            try
            {
                var recovery = RegistrySnapshotFactory.Create(_registryInspection, ToRegistryGameIdentity(Game));
                recoveryPath = _registrySnapshotStore.CreateNew(recovery, Game.InstallationPath);
                verifiedRecovery = _registrySnapshotStore.LoadAndVerify(recoveryPath);
                _registryBackupStatus = Texts["RecoverySnapshotVerified"] + " " + Path.GetFileName(recoveryPath);
            }
            catch (Exception)
            {
                _restoreStatus = Texts["RecoverySnapshotFailed"];
                _registryBackupStatus = Texts["RecoverySnapshotFailed"];
                return;
            }
            NotifyRestoreChanged();
            restorationStarted = true;
            _restoreStatus = Texts["RestorationRunning"];
            var result = _registryTransactionService.Restore(selected, ToRegistryGameIdentity(Game));
            _registryTransactionDetails = FormatOperationResults(result.Operations);
            if (result.Succeeded)
            {
                _restoreStatus = Texts["RestorationSucceeded"];
                _registryInspection = _registryInspectionService.Inspect();
                NotifyRegistryInspectionChanged();
                RefreshRegistrySnapshots();
                return;
            }
            _restoreStatus = result.IsPartial ? Texts["RestorationPartialFailure"] : Texts["RestorationFailed"];
            RunRestoreRollback(verifiedRecovery, recoveryPath);
        }
        catch (Exception)
        {
            _restoreStatus = Texts["RestorationFailed"];
            if (restorationStarted && verifiedRecovery is not null)
                RunRestoreRollback(verifiedRecovery, recoveryPath);
        }
        finally
        {
            _restoreRunning = false;
            RefreshRestoreCommand();
            NotifyRestoreChanged();
        }
    }

    private static bool CanInspectForRestore(RegistryLanguageInspection inspection) =>
        inspection.KeyStatus == RegistryKeyReadStatus.Success &&
        inspection.Locale.Status is not (RegistryValueReadStatus.AccessDenied or RegistryValueReadStatus.UnexpectedError) &&
        inspection.LocaleAudio.Status is not (RegistryValueReadStatus.AccessDenied or RegistryValueReadStatus.UnexpectedError);

    private void RunRestoreRollback(RegistrySnapshotEnvelope recovery, string recoveryPath)
    {
        _registryRollbackStatus = Texts["RollbackRunning"];
        try
        {
            var rollback = _registryTransactionService!.Restore(recovery, ToRegistryGameIdentity(Game));
            _registryTransactionDetails += Environment.NewLine + Texts["RollbackResults"] + Environment.NewLine + FormatOperationResults(rollback.Operations);
            _registryRollbackStatus = rollback.Succeeded
                ? Texts["RollbackSucceeded"]
                : Texts["RestoreRollbackManualRecovery"] + " " + Path.GetFileName(recoveryPath);
            if (!rollback.Succeeded) _restoreStatus = Texts["RestoreRollbackManualRecovery"];
        }
        catch (Exception)
        {
            _registryRollbackStatus = Texts["RestoreRollbackManualRecovery"] + " " + Path.GetFileName(recoveryPath);
            _restoreStatus = Texts["RestoreRollbackManualRecovery"];
        }
        try
        {
            _registryInspection = _registryInspectionService?.Inspect();
            if (_registryInspection is not null) NotifyRegistryInspectionChanged();
        }
        catch { }
        RefreshRegistrySnapshots();
    }

    private string FormatSnapshot(RegistrySnapshot snapshot)
    {
        static string Value(RegistrySnapshotValue item) => item.Exists
            ? $"{item.Name}: {item.Data?.GetRawText() ?? "—"} ({item.Kind})"
            : $"{item.Name}: absent";
        return $"{Texts["SnapshotTransactionId"]}: {snapshot.TransactionId}{Environment.NewLine}" +
               $"{Texts["SnapshotCreatedAt"]}: {snapshot.TimestampUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss zzz}{Environment.NewLine}" +
               $"{Value(snapshot.Locale)}{Environment.NewLine}{Value(snapshot.LocaleAudio)}";
    }

    private void LaunchVerifiedGame()
    {
        if (!CanLaunchGame || _gameLaunchService is null) return;
        _launchRunning = true;
        _launchStatus = Texts["LaunchRevalidating"];
        RefreshLaunchCommand();
        try
        {
            var result = _gameLaunchService.LaunchDetectedGame(GameDirectory);
            _launchStatus = result.Status switch
            {
                GameLaunchStatus.Started => Texts["LaunchSucceeded"],
                GameLaunchStatus.AlreadyRunning => Texts["GameAlreadyRunning"],
                GameLaunchStatus.UnsupportedInstallation => Texts["UnsupportedInstallation"],
                _ => Texts["LaunchFailed"] + " " + result.Message
            };
        }
        catch (Exception)
        {
            _launchStatus = Texts["LaunchFailed"];
        }
        finally
        {
            _launchRunning = false;
            RefreshLaunchCommand();
        }
    }

    private string GetLaunchReadinessText() => _gameLaunchService is null
        ? Texts["LaunchUnavailable"]
        : !Game.IsDetected || !File.Exists(Path.Combine(Game.InstallationPath, "MW2CR.exe"))
            ? Texts["UnsupportedInstallation"]
            : Texts["LaunchReady"];

    private bool ConfirmRegistryRestore(string message) => MessageBox.Show(message, Texts["ConfirmRestoreTitle"], MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;

    private void NotifyRestoreChanged()
    {
        OnPropertyChanged(nameof(RestoreStatusText));
        OnPropertyChanged(nameof(RestoreReadinessText));
        OnPropertyChanged(nameof(SelectedSnapshotDetailsText));
        OnPropertyChanged(nameof(SnapshotListStatusText));
        RefreshRestoreCommand();
        NotifyTransactionChanged();
    }

    private void RefreshRestoreCommand()
    {
        OnPropertyChanged(nameof(CanRestorePreviousSettings));
        OnPropertyChanged(nameof(RestoreReadinessText));
        if (RestorePreviousSettingsCommand is not null) RestorePreviousSettingsCommand.RaiseCanExecuteChanged();
    }

    private void RefreshLaunchCommand()
    {
        OnPropertyChanged(nameof(CanLaunchGame));
        OnPropertyChanged(nameof(CanLaunch));
        OnPropertyChanged(nameof(LaunchStatusText));
        OnPropertyChanged(nameof(LaunchBlockReason));
        if (LaunchGameCommand is not null) LaunchGameCommand.RaiseCanExecuteChanged();
    }

    private bool CanApplyWithoutRunning => _registryTransactionService is not null && _registrySnapshotStore is not null && !_restoreRunning && !_launchRunning &&
        Game.IsDetected && _registryInspection is { KeyStatus: RegistryKeyReadStatus.Success } inspection &&
        IsApplyableState(inspection.Locale) && IsApplyableState(inspection.LocaleAudio) &&
        !IsTargetAlreadyConfigured(inspection) && !_backupCreationBlocked;

    private static bool IsApplyableState(RegistryValueInspection value) =>
        value.Status is RegistryValueReadStatus.PresentString or RegistryValueReadStatus.Missing;

    private bool IsTargetAlreadyConfigured(RegistryLanguageInspection inspection) =>
        inspection.Locale.Status == RegistryValueReadStatus.PresentString &&
        inspection.LocaleAudio.Status == RegistryValueReadStatus.PresentString &&
        inspection.Locale.Data == SelectedTargetLocale && inspection.LocaleAudio.Data == SelectedTargetLocale;

    private string GetApplyStatusText() => _registryTransactionRunning ? Texts["TransactionRunning"] : GetApplyReadinessText();

    private string GetApplyReadinessText()
    {
        if (!Game.IsDetected) return Texts["ApplyNeedsCompatibleGame"];
        if (_registryTransactionService is null || _registrySnapshotStore is null) return Texts["ApplyUnavailable"];
        if (_registryInspection is null) return Texts["InspectBeforeApply"];
        if (_registryInspection.KeyStatus == RegistryKeyReadStatus.MissingKey) return Texts["RegistryTargetKeyMissing"];
        if (_registryInspection.KeyStatus == RegistryKeyReadStatus.AccessDenied) return Texts["RegistryAccessDenied"];
        if (_registryInspection.KeyStatus != RegistryKeyReadStatus.Success) return Texts["RegistryReadError"];
        if (_registryInspection.Locale.Status == RegistryValueReadStatus.UnsupportedType || _registryInspection.LocaleAudio.Status == RegistryValueReadStatus.UnsupportedType)
            return Texts["ApplyUnsupportedValueType"];
        if (!IsApplyableState(_registryInspection.Locale) || !IsApplyableState(_registryInspection.LocaleAudio)) return Texts["ApplyInspectionFailed"];
        if (IsTargetAlreadyConfigured(_registryInspection)) return Texts["TargetAlreadyConfigured"];
        if (_backupCreationBlocked) return Texts["BackupUnavailableBlock"];
        return Texts["ApplyReady"];
    }

    private void ApplySelectedGameLanguage()
    {
        if (!CanApplyGameLanguage || _registryInspectionService is null || _registryTransactionService is null || _registrySnapshotStore is null) return;
        _registryTransactionRunning = true;
        _registryTransactionProgress = Texts["TransactionRefreshing"];
        _registryRollbackStatus = Texts["RollbackNotNeeded"];
        _registryTransactionDetails = string.Empty;
        NotifyTransactionChanged();
        RegistrySnapshotEnvelope? verifiedSnapshot = null;
        var rollbackAttempted = false;
        try
        {
            // Revalidate the exact on-disk build and fresh Registry state immediately before confirmation.
            Game = _detector.Detect(GameDirectory);
            if (!Game.IsDetected)
            {
                _registryTransactionProgress = Texts["ApplyNeedsCompatibleGame"];
                return;
            }
            _registryInspection = _registryInspectionService.Inspect();
            NotifyRegistryInspectionChanged();
            if (!CanApplyWithoutRunning)
            {
                _registryTransactionProgress = GetApplyReadinessText();
                return;
            }

            var currentPair = $"LOCALE={FormatCurrentValue(_registryInspection.Locale)}; LOCALE_AUDIO={FormatCurrentValue(_registryInspection.LocaleAudio)}";
            var targetPair = TargetRegistryValuesText;
            var confirmation = string.Format(CultureInfo.CurrentCulture, Texts["ConfirmSwitchPrompt"], currentPair, targetPair);
            if (!_confirmRegistryApply(confirmation))
            {
                _registryTransactionProgress = Texts["TransactionCancelled"];
                return;
            }

            _registryTransactionProgress = Texts["BackupCreating"];
            NotifyTransactionChanged();
            RegistrySnapshot snapshot;
            string backupPath;
            try
            {
                snapshot = RegistrySnapshotFactory.Create(_registryInspection, ToRegistryGameIdentity(Game));
                backupPath = _registrySnapshotStore.CreateNew(snapshot, Game.InstallationPath);
                verifiedSnapshot = _registrySnapshotStore.LoadAndVerify(backupPath);
                _registryBackupStatus = Texts["BackupVerified"] + " " + Path.GetFileName(backupPath);
            }
            catch (Exception ex)
            {
                _backupCreationBlocked = true;
                _registryBackupStatus = Texts["BackupFailed"] + " " + ex.Message;
                _registryTransactionProgress = Texts["BackupUnavailableBlock"];
                return;
            }
            NotifyTransactionChanged();
            RefreshRegistrySnapshots();

            _registryTransactionProgress = Texts["TransactionApplying"];
            NotifyTransactionChanged();
            try
            {
                var result = SelectedGameLanguage == "ar"
                    ? _registryTransactionService.ApplyArabicPair(snapshot.TransactionId)
                    : _registryTransactionService.ApplyEnglishPair(snapshot.TransactionId);
                _registryTransactionDetails = FormatOperationResults(result.Operations);
                if (result.Succeeded)
                {
                    _registryTransactionProgress = Texts["TransactionSucceeded"];
                    _registryRollbackStatus = Texts["RollbackNotNeeded"];
                    _registryInspection = _registryInspectionService.Inspect();
                    NotifyRegistryInspectionChanged();
                    return;
                }

                _registryTransactionProgress = result.IsPartial ? Texts["TransactionPartialFailure"] : Texts["TransactionFailed"];
                rollbackAttempted = true;
                RunRollback(verifiedSnapshot, backupPath);
            }
            catch (Exception ex)
            {
                _registryTransactionProgress = Texts["TransactionFailed"] + " " + ex.Message;
                rollbackAttempted = true;
                RunRollback(verifiedSnapshot, backupPath);
            }
        }
        catch (Exception ex)
        {
            _registryTransactionProgress = Texts["TransactionFailed"] + " " + ex.Message;
            if (verifiedSnapshot is not null && !rollbackAttempted)
            {
                rollbackAttempted = true;
                RunRollback(verifiedSnapshot, Path.Combine(_paths.Root, "registry-backups", verifiedSnapshot.Snapshot.TransactionId + ".json"));
            }
        }
        finally
        {
            _registryTransactionRunning = false;
            NotifyTransactionChanged();
            RefreshApplyCommand();
        }
    }

    private void RunRollback(RegistrySnapshotEnvelope snapshot, string backupPath)
    {
        _registryRollbackStatus = Texts["RollbackRunning"];
        NotifyTransactionChanged();
        try
        {
            var rollback = _registryTransactionService!.Restore(snapshot, ToRegistryGameIdentity(Game));
            _registryTransactionDetails += Environment.NewLine + Texts["RollbackResults"] + Environment.NewLine + FormatOperationResults(rollback.Operations);
            _registryRollbackStatus = rollback.Succeeded
                ? Texts["RollbackSucceeded"]
                : Texts["RollbackManualRecovery"] + " " + Path.GetFileName(backupPath);
            if (!rollback.Succeeded) _registryTransactionProgress = Texts["RollbackManualRecovery"];
        }
        catch (Exception ex)
        {
            _registryRollbackStatus = Texts["RollbackManualRecovery"] + " " + Path.GetFileName(backupPath) + " " + ex.Message;
            _registryTransactionProgress = Texts["RollbackManualRecovery"];
        }
        try
        {
            _registryInspection = _registryInspectionService?.Inspect();
            if (_registryInspection is not null) NotifyRegistryInspectionChanged();
        }
        catch { /* The per-value read-back result remains authoritative for the rollback status. */ }
    }

    private string FormatOperationResults(IEnumerable<RegistryOperationResult> operations) => string.Join(Environment.NewLine,
        operations.Select(operation => $"{operation.Name}: {(operation.Succeeded ? Texts["OperationVerified"] : Texts["OperationFailed"])} — {operation.Message}"));

    private string FormatCurrentValue(RegistryValueInspection value) => value.Status switch
    {
        RegistryValueReadStatus.Missing => Texts["RegistryMissing"],
        RegistryValueReadStatus.PresentString => value.Data ?? "",
        RegistryValueReadStatus.UnsupportedType => Texts["RegistryUnsupportedType"] + " (" + value.Kind + ")",
        _ => ValueStatusText(value)
    };

    private static RegistryGameIdentity ToRegistryGameIdentity(GameDetectionResult game) =>
        new(GameDetectionService.GameId, game.BuildProduct, game.GameVersion, game.BuildName, game.BuildKey);

    private bool ConfirmRegistryApply(string message) => MessageBox.Show(message, Texts["ConfirmSwitchTitle"], MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;

    private void NotifyRegistryInspectionChanged()
    {
        OnPropertyChanged(nameof(RegistryConfigurationText));
        OnPropertyChanged(nameof(RegistryInspectionTimeText));
        OnPropertyChanged(nameof(RegistryInspectionErrorText));
        OnPropertyChanged(nameof(RegistryLocaleStatusText));
        OnPropertyChanged(nameof(RegistryLocaleAudioStatusText));
        OnPropertyChanged(nameof(RegistryLocaleDataText));
        OnPropertyChanged(nameof(RegistryLocaleAudioDataText));
        OnPropertyChanged(nameof(RegistryLocaleKindText));
        OnPropertyChanged(nameof(RegistryLocaleAudioKindText));
        OnPropertyChanged(nameof(TargetRegistryValuesText));
        OnPropertyChanged(nameof(RegistryBackupStatusText));
        OnPropertyChanged(nameof(RegistryTransactionProgressText));
        OnPropertyChanged(nameof(RegistryTransactionDetailsText));
        OnPropertyChanged(nameof(RegistryRollbackStatusText));
        OnPropertyChanged(nameof(ApplyLanguageStatusText));
        OnPropertyChanged(nameof(RestoreStatusText));
        OnPropertyChanged(nameof(RestoreReadinessText));
        OnPropertyChanged(nameof(SelectedSnapshotDetailsText));
        OnPropertyChanged(nameof(LaunchStatusText));
        RefreshApplyCommand();
        RefreshRestoreCommand();
    }

    private void NotifyTransactionChanged()
    {
        OnPropertyChanged(nameof(RegistryBackupStatusText));
        OnPropertyChanged(nameof(RegistryTransactionProgressText));
        OnPropertyChanged(nameof(RegistryTransactionDetailsText));
        OnPropertyChanged(nameof(RegistryRollbackStatusText));
        OnPropertyChanged(nameof(ApplyLanguageStatusText));
        OnPropertyChanged(nameof(RestoreStatusText));
        OnPropertyChanged(nameof(RestoreReadinessText));
        OnPropertyChanged(nameof(LaunchStatusText));
        OnPropertyChanged(nameof(CanApplyGameLanguage));
        RefreshApplyCommand();
        RefreshRestoreCommand();
        RefreshLaunchCommand();
    }

    private void RefreshApplyCommand()
    {
        OnPropertyChanged(nameof(CanApplyGameLanguage));
        OnPropertyChanged(nameof(ApplyLanguageStatusText));
        if (ApplyGameLanguageCommand is not null) ApplyGameLanguageCommand.RaiseCanExecuteChanged();
    }

    private string ValueStatusText(RegistryValueInspection? value)
    {
        if (value is null) return Texts["InspectionNotRun"];
        var key = value.Status switch
        {
            RegistryValueReadStatus.PresentString => "RegistryString",
            RegistryValueReadStatus.Missing => "RegistryMissing",
            RegistryValueReadStatus.UnsupportedType => "RegistryUnsupportedType",
            RegistryValueReadStatus.AccessDenied => "RegistryAccessDenied",
            RegistryValueReadStatus.MissingKey => "RegistryMissingKey",
            _ => "RegistryReadError"
        };
        return Texts[key];
    }

    private void RefreshPackages()
    {
        Packages.Clear();
        foreach (var package in _packageService.Discover(PackageDirectory, Game.GameVersion)) Packages.Add(package);
        if (SelectedPackage is not null) SelectedPackage = Packages.FirstOrDefault(p => p.PackageId == SelectedPackage.PackageId);
        OnPropertyChanged(nameof(PackageStatusText));
        _logger.Write("Information", "Packages", $"Discovered {Packages.Count} local package manifest(s).");
    }

    private void GenerateDiagnosticReport()
    {
        try
        {
            Directory.CreateDirectory(_paths.Diagnostics);
            var report = new
            {
                reportType = "Arabize Games read-only detection report",
                generatedAt = DateTimeOffset.Now,
                gameId = GameDetectionService.GameId,
                installationPath = GameDirectory,
                detected = Game.IsDetected,
                identity = Game.Identity,
                version = Game.GameVersion,
                buildProduct = Game.BuildProduct,
                buildName = Game.BuildName,
                buildKey = Game.BuildKey,
                branch = Game.Branch,
                executable = Game.ExecutablePath,
                executableSha256 = Game.ExecutableSha256,
                evidence = Game.Evidence,
                warning = "Localization installation and restoration are disabled until a real compatible package and verified method are available."
            };
            var file = Path.Combine(_paths.Diagnostics, $"mw2-campaign-remastered-{DateTime.Now:yyyyMMdd-HHmmss}.json");
            File.WriteAllText(file, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            _logger.Write("Information", "Reports", $"Diagnostic report created: {file}");
            LoadReports();
            SelectedReport = Reports.FirstOrDefault(report => report.Path.Equals(file, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex) { _logger.Write("Error", "Reports", "Could not generate the diagnostic report.", ex); }
        RefreshActivity();
    }

    private void LoadTransactions()
    {
        Transactions.Clear();
        foreach (var file in Directory.EnumerateFiles(_paths.Transactions, "*.json").OrderByDescending(File.GetLastWriteTimeUtc))
        {
            try { var record = JsonSerializer.Deserialize<InstallationTransactionRecord>(File.ReadAllText(file)); if (record is not null) Transactions.Add(record); }
            catch (Exception ex) { _logger.Write("Warning", "History", $"Could not read transaction record {Path.GetFileName(file)}.", ex); }
        }
    }

    private void LoadReports()
    {
        Reports.Clear();
        if (!Directory.Exists(_paths.Diagnostics)) return;
        foreach (var file in Directory.EnumerateFiles(_paths.Diagnostics).OrderByDescending(File.GetLastWriteTimeUtc))
            Reports.Add(new(Path.GetFileName(file), file, File.GetLastWriteTime(file)));
    }

    private void RefreshActivity()
    {
        Activity.Clear();
        foreach (var entry in _logger.ReadRecent(100).Reverse()) Activity.Add(entry);
    }

    private void SetLanguage(string language)
    {
        _settings.UiLanguage = language;
        _settingsStore.Save(_settings);
        Texts.SetLanguage(language);
        OnPropertyChanged(nameof(WindowFlowDirection));
        OnPropertyChanged(nameof(GameStatusText));
        OnPropertyChanged(nameof(GameMessage));
        OnPropertyChanged(nameof(GameBuildText));
        OnPropertyChanged(nameof(PackageStatusText));
        OnPropertyChanged(nameof(LaunchBlockReason));
        OnPropertyChanged(nameof(CurrentPageTitle));
        OnPropertyChanged(nameof(SelectedPackageStatusText));
        OnPropertyChanged(nameof(GameEvidence));
        OnPropertyChanged(nameof(SelectedGameLanguageText));
        OnPropertyChanged(nameof(GameBuildNameText));
        OnPropertyChanged(nameof(GameCompatibilityText));
        OnPropertyChanged(nameof(CurrentGameLanguageText));
        OnPropertyChanged(nameof(RegistryConfigurationText));
        OnPropertyChanged(nameof(RegistryInspectionTimeText));
        OnPropertyChanged(nameof(RegistryInspectionErrorText));
        OnPropertyChanged(nameof(RegistryLocaleStatusText));
        OnPropertyChanged(nameof(RegistryLocaleAudioStatusText));
        OnPropertyChanged(nameof(RegistryLocaleDataText));
        OnPropertyChanged(nameof(RegistryLocaleAudioDataText));
        OnPropertyChanged(nameof(RegistryLocaleKindText));
        OnPropertyChanged(nameof(RegistryLocaleAudioKindText));
        OnPropertyChanged(nameof(TargetRegistryValuesText));
        OnPropertyChanged(nameof(RegistryBackupStatusText));
        OnPropertyChanged(nameof(RegistryTransactionProgressText));
        OnPropertyChanged(nameof(RegistryTransactionDetailsText));
        OnPropertyChanged(nameof(RegistryRollbackStatusText));
        OnPropertyChanged(nameof(ApplyLanguageStatusText));
        OnPropertyChanged(nameof(SnapshotListStatusText));
        OnPropertyChanged(nameof(SelectedSnapshotDetailsText));
        OnPropertyChanged(nameof(RestoreStatusText));
        OnPropertyChanged(nameof(RestoreReadinessText));
        OnPropertyChanged(nameof(LaunchStatusText));
        RefreshApplyCommand();
        RefreshRestoreCommand();
        RefreshLaunchCommand();
    }

    private static void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
    }
}
