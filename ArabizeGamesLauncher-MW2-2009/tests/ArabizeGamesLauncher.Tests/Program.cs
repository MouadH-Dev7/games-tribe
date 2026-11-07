using System.Security.Cryptography;
using System.Text.Json;
using ArabizeGamesLauncher.Models;
using ArabizeGamesLauncher.Services;
using ArabizeGamesLauncher.ViewModels;

var tests = new (string Name, Func<Task> Run)[]
{
    ("detect supported Campaign Remastered fixture", DetectSupportedGame),
    ("reject wrong build version", RejectWrongVersion),
    ("reject wrong build name", RejectWrongBuildName),
    ("reject wrong build product", RejectWrongBuildProduct),
    ("reject missing executable and game data", RejectMissingRequiredFiles),
    ("match config by active Build Key", MatchActiveBuildKey),
    ("reject missing and contradictory build metadata", RejectBuildMetadata),
    ("reject unsupported executable identity", RejectUnsupportedIdentity),
    ("reject invalid game path", RejectInvalidGamePath),
    ("parse compatible package and verify SHA-256", ValidatePackage),
    ("reject incompatible package build", RejectPackageBuild),
    ("reject path traversal", RejectTraversal),
    ("reject traversal in package manifest", RejectManifestTraversal),
    ("reject corrupted package payload", RejectCorruptPayload),
    ("reject missing package manifest", RejectMissingManifest),
    ("create and verify isolated backup", BackupIntegrity),
    ("plan restoration without touching a destination", RestorationPlan),
    ("refuse restoration plan for missing backup", MissingBackup),
    ("detect corrupted backup and refuse restore plan", CorruptBackup),
    ("switch interface language strings", SwitchUiLanguage)
    ,("keep game language separate and actions disabled", GameLanguageUiState)
    ,("inspect Arabic Registry pair using fake adapter", InspectArabicRegistryPair)
    ,("classify missing Registry values", InspectMissingRegistryValues)
    ,("classify unsupported Registry value types", InspectUnsupportedRegistryType)
    ,("distinguish denied access and missing key", InspectRegistryErrors)
    ,("classify mixed language values", InspectMixedRegistryPair)
    ,("read only approved Registry value names", RegistryReadScope)
    ,("snapshot round trip and integrity verification", SnapshotRoundTrip)
    ,("reject tampered snapshot", RejectTamperedSnapshot)
    ,("reject unsupported snapshot schema", RejectUnsupportedSnapshotSchema)
    ,("reject duplicate backup filename without overwrite", RejectDuplicateSnapshot)
    ,("preserve missing state and exact value kind on restore", RestoreSnapshotExactState)
    ,("report partial fake transaction failure", PartialTransactionFailure)
    ,("do not inspect Registry until explicit UI action", RegistryUiExplicitAction)
    ,("apply Arabic after a verified pre-change snapshot", ApplyArabicWithVerifiedSnapshot)
    ,("apply English locale pair", ApplyEnglishPair)
    ,("snapshot verification failure prevents mutation", SnapshotVerificationFailureBlocksApply)
    ,("partial apply rolls back successfully", PartialApplySuccessfulRollback)
    ,("failed rollback reports manual recovery and retains snapshot", PartialApplyFailedRollback)
    ,("missing key, denied access, and unsupported type block apply", RegistryConditionsBlockApply)
    ,("cancelled confirmation performs no writes", CancelApplyConfirmation)
    ,("already configured target disables apply", AlreadyConfiguredDisablesApply)
    ,("read-back mismatch is reported", ReadBackMismatch)
    ,("list only integrity-checked compatible snapshots in approved folder", ListOnlyValidSnapshots)
    ,("restore verified English snapshot from Arabic Registry pair", RestoreEnglishSnapshot)
    ,("restore verified Arabic snapshot from English Registry pair", RestoreArabicSnapshot)
    ,("missing LAZR key blocks UI restoration without creating key", MissingKeyBlocksRestore)
    ,("recovery snapshot exists before first restoration write", RecoverySnapshotBeforeRestore)
    ,("recovery snapshot verification failure blocks restoration", RecoveryVerificationFailureBlocksRestore)
    ,("partial restoration rolls back successfully", PartialRestoreSuccessfulRollback)
    ,("failed restoration rollback reports recovery file", PartialRestoreFailedRollback)
    ,("launch service uses only freshly validated executable and directory", LaunchUsesValidatedPaths)
    ,("reject unsupported launch build and invalid detection", LaunchRejectsUnsupportedBuild)
    ,("reject missing and mismatched launch executable", LaunchRejectsMissingOrMismatchedExecutable)
    ,("duplicate launch is blocked when game already runs", LaunchRejectsExistingProcess)
    ,("process startup error is reported", LaunchReportsStartFailure)
    ,("launch UI waits for explicit click and keeps launcher open", LaunchUiExplicitClick)
    ,("restore UI requires confirmation and supports cancellation", RestoreUiConfirmationCancellation)
    ,("restore and launch messages are bilingual", RestoreLaunchMessagesBilingual)
    ,("reject incompatible build identity during restoration", RejectIncompatibleRestoreSnapshot)
    ,("unsupported game disables Restore and Launch in UI", UnsupportedUiActionsDisabled)
};

var passed = 0;
foreach (var test in tests)
{
    await test.Run();
    Console.WriteLine($"PASS {test.Name}");
    passed++;
}
Console.WriteLine($"RESULT {passed}/{tests.Length} passed");

static async Task DetectSupportedGame()
{
    using var fixture = new TempFixture();
    var game = fixture.CreateGame();
    var detector = new GameDetectionService(new FakeMetadata(GameDetectionService.ExpectedProduct));
    var result = detector.Detect(game);
    Assert(result.IsDetected, result.Message);
    Assert(result.GameVersion == "1.1.2.1279292", "Build version was not read.");
    Assert(result.BuildProduct == "lazr", "Build product was not read.");
    Assert(result.BuildName == GameDetectionService.ExpectedBuildName, "Build name was not read from the active config.");
}

static Task RejectWrongVersion()
{
    using var fixture = new TempFixture();
    var game = fixture.CreateGame(version: "1.1.2.1279291");
    var result = new GameDetectionService(new FakeMetadata(GameDetectionService.ExpectedProduct)).Detect(game);
    Assert(!result.IsDetected && result.Message.Contains("version", StringComparison.OrdinalIgnoreCase), "Wrong version was accepted.");
    return Task.CompletedTask;
}

static Task RejectWrongBuildName()
{
    using var fixture = new TempFixture();
    var game = fixture.CreateGame(buildName: "CRC_wrong_build");
    var result = new GameDetectionService(new FakeMetadata(GameDetectionService.ExpectedProduct)).Detect(game);
    Assert(!result.IsDetected && result.Message.Contains("build name", StringComparison.OrdinalIgnoreCase), "Wrong build name was accepted.");
    return Task.CompletedTask;
}

static Task RejectWrongBuildProduct()
{
    using var fixture = new TempFixture();
    var game = fixture.CreateGame(product: "other-product");
    var result = new GameDetectionService(new FakeMetadata(GameDetectionService.ExpectedProduct)).Detect(game);
    Assert(!result.IsDetected && result.Message.Contains("product", StringComparison.OrdinalIgnoreCase), "Wrong build product was accepted.");
    return Task.CompletedTask;
}

static Task RejectMissingRequiredFiles()
{
    using var fixture = new TempFixture();
    var game = fixture.CreateGame();
    File.Delete(Path.Combine(game, "MW2CR.exe"));
    var missingExe = new GameDetectionService(new FakeMetadata(GameDetectionService.ExpectedProduct)).Detect(game);
    Assert(!missingExe.IsDetected && missingExe.Message.Contains("Game executable", StringComparison.Ordinal), "Missing executable was accepted.");
    using var fixture2 = new TempFixture();
    var game2 = fixture2.CreateGame();
    Directory.Delete(Path.Combine(game2, "Data", "data"));
    var missingData = new GameDetectionService(new FakeMetadata(GameDetectionService.ExpectedProduct)).Detect(game2);
    Assert(!missingData.IsDetected && missingData.Message.Contains("Game data", StringComparison.Ordinal), "Missing data directory was accepted.");
    return Task.CompletedTask;
}

static Task MatchActiveBuildKey()
{
    using var fixture = new TempFixture();
    var game = fixture.CreateGame();
    Directory.CreateDirectory(Path.Combine(game, "Data", "config", "zz", "zz"));
    File.WriteAllText(Path.Combine(game, "Data", "config", "zz", "zz", "decoy"), "build-product = wrong\nbuild-name = wrong\n");
    var result = new GameDetectionService(new FakeMetadata(GameDetectionService.ExpectedProduct)).Detect(game);
    Assert(result.IsDetected && result.BuildKey == TempFixture.TestBuildKey, "Detector did not use the config belonging to the active Build Key.");
    return Task.CompletedTask;
}

static Task RejectBuildMetadata()
{
    using var fixture = new TempFixture();
    var game = fixture.CreateGame();
    File.Delete(TempFixture.ConfigPath(game));
    var missingConfig = new GameDetectionService(new FakeMetadata(GameDetectionService.ExpectedProduct)).Detect(game);
    Assert(!missingConfig.IsDetected && missingConfig.Message.Contains("missing", StringComparison.OrdinalIgnoreCase), "Missing active build config was accepted.");
    using var fixture2 = new TempFixture();
    var game2 = fixture2.CreateGame();
    File.AppendAllText(Path.Combine(game2, ".build.info"), "eu|1|11111111111111111111111111111111|1.1.2.1279292\n");
    var conflict = new GameDetectionService(new FakeMetadata(GameDetectionService.ExpectedProduct)).Detect(game2);
    Assert(!conflict.IsDetected && conflict.Message.Contains("exactly one active", StringComparison.OrdinalIgnoreCase), "Multiple active build rows were accepted.");
    return Task.CompletedTask;
}

static Task RejectUnsupportedIdentity()
{
    using var fixture = new TempFixture();
    var game = fixture.CreateGame();
    var result = new GameDetectionService(new FakeMetadata("Call of Duty: Modern Warfare II")).Detect(game);
    Assert(result.Status == GameDetectionStatus.Unsupported, "Wrong product identity was accepted.");
    return Task.CompletedTask;
}

static Task RejectInvalidGamePath()
{
    var result = new GameDetectionService(new FakeMetadata(GameDetectionService.ExpectedProduct)).Detect(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
    Assert(result.Status == GameDetectionStatus.NotFound, "Missing directory was not rejected.");
    return Task.CompletedTask;
}

static async Task ValidatePackage()
{
    using var fixture = new TempFixture();
    var manifest = fixture.CreatePackage("1.1.2.1279292", "assets/ar/text.bin");
    var entry = new PackageService().Validate(manifest, "1.1.2.1279292");
    Assert(entry.Status == PackageStatus.Validated, entry.StatusMessage);
    await Task.CompletedTask;
}

static Task RejectPackageBuild()
{
    using var fixture = new TempFixture();
    var manifest = fixture.CreatePackage("1.1.2.1279291", "assets/ar/text.bin");
    var entry = new PackageService().Validate(manifest, "1.1.2.1279292");
    Assert(entry.Status == PackageStatus.Incompatible, "A different game build was accepted.");
    return Task.CompletedTask;
}

static Task RejectTraversal()
{
    Assert(!PackageService.IsSafeRelativePath("../../outside.bin"), "Parent traversal was accepted.");
    Assert(!PackageService.IsSafeRelativePath("C:\\outside.bin"), "Rooted path was accepted.");
    Assert(PackageService.IsSafeRelativePath("localized/ar/ui.bin"), "A normal relative path was rejected.");
    return Task.CompletedTask;
}

static Task RejectManifestTraversal()
{
    using var fixture = new TempFixture();
    var manifest = fixture.CreatePackage("1.1.2.1279292", "../../outside.bin");
    var entry = new PackageService().Validate(manifest, "1.1.2.1279292");
    Assert(entry.Status == PackageStatus.Incompatible && entry.StatusMessage.Contains("unsafe", StringComparison.OrdinalIgnoreCase), "Manifest path traversal was accepted.");
    return Task.CompletedTask;
}

static Task RejectCorruptPayload()
{
    using var fixture = new TempFixture();
    var manifest = fixture.CreatePackage("1.1.2.1279292", "assets/ar/text.bin", corruptHash: true);
    var entry = new PackageService().Validate(manifest, "1.1.2.1279292");
    Assert(entry.Status == PackageStatus.Incompatible && entry.StatusMessage.Contains("SHA-256", StringComparison.OrdinalIgnoreCase), "Corrupt package payload was accepted.");
    return Task.CompletedTask;
}

static Task RejectMissingManifest()
{
    using var fixture = new TempFixture();
    var entry = new PackageService().Validate(Path.Combine(fixture.Root, "missing.json"), "1.1.2.1279292");
    Assert(entry.Status == PackageStatus.Incompatible, "Missing manifest was not rejected.");
    return Task.CompletedTask;
}

static async Task BackupIntegrity()
{
    using var fixture = new TempFixture();
    var source = Path.Combine(fixture.Root, "original.bin");
    await File.WriteAllTextAsync(source, "temporary fake game file");
    var record = await new BackupManager().CreateVerifiedBackupAsync(source, Path.Combine(fixture.Root, "backups"), "Data\\sample.bin", "test-transaction");
    Assert(File.Exists(record.BackupPath), "Backup was not created.");
    Assert(record.OriginalSha256 == await BackupManager.HashAsync(record.BackupPath), "Backup hash did not match source.");
}

static async Task RestorationPlan()
{
    using var fixture = new TempFixture();
    var source = Path.Combine(fixture.Root, "original.bin");
    await File.WriteAllTextAsync(source, "temporary data");
    var root = Path.Combine(fixture.Root, "backups");
    var record = await new BackupManager().CreateVerifiedBackupAsync(source, root, "Data/sample.bin", "restore-plan");
    var plan = await new BackupManager().PlanRestoreAsync(Path.Combine(root, "restore-plan"));
    Assert(plan.Count == 1 && plan[0].IsVerified, "Valid backup was not included as verified in plan.");
    Assert(File.Exists(source) && await BackupManager.HashAsync(source) == record.OriginalSha256, "Planning changed the source file.");
}

static async Task CorruptBackup()
{
    using var fixture = new TempFixture();
    var source = Path.Combine(fixture.Root, "original.bin");
    await File.WriteAllTextAsync(source, "temporary data");
    var root = Path.Combine(fixture.Root, "backups");
    var record = await new BackupManager().CreateVerifiedBackupAsync(source, root, "Data/sample.bin", "corrupt-backup");
    await File.WriteAllTextAsync(record.BackupPath, "corrupt");
    var plan = await new BackupManager().PlanRestoreAsync(Path.Combine(root, "corrupt-backup"));
    Assert(plan.Count == 1 && !plan[0].IsVerified, "Corrupt backup was marked safe to restore.");
}

static async Task MissingBackup()
{
    using var fixture = new TempFixture();
    var source = Path.Combine(fixture.Root, "original.bin");
    await File.WriteAllTextAsync(source, "temporary data");
    var root = Path.Combine(fixture.Root, "backups");
    var record = await new BackupManager().CreateVerifiedBackupAsync(source, root, "Data/sample.bin", "missing-backup");
    File.Delete(record.BackupPath);
    var plan = await new BackupManager().PlanRestoreAsync(Path.Combine(root, "missing-backup"));
    Assert(plan.Count == 1 && !plan[0].IsVerified, "Missing backup was marked safe to restore.");
}

static Task SwitchUiLanguage()
{
    var texts = new TextCatalog();
    texts.SetLanguage("ar");
    Assert(texts["Dashboard"] == "الرئيسية", "Arabic text catalog failed.");
    Assert(texts.EvidenceLabel("Game executable") == "الملف التنفيذي للعبة", "Arabic evidence label failed.");
    texts.SetLanguage("en");
    Assert(texts["Dashboard"] == "Dashboard", "English text catalog failed.");
    Assert(texts.EvidenceLabel("Game executable") == "Game executable", "English evidence label failed.");
    Assert(!string.IsNullOrWhiteSpace(texts["GameLanguagePage"]) && !string.IsNullOrWhiteSpace(texts["ApplyDisabledReason"]), "English Game Language strings are missing.");
    texts.SetLanguage("ar");
    Assert(texts["GameLanguagePage"] == "لغة اللعبة" && !string.IsNullOrWhiteSpace(texts["RestoreDisabledReason"]), "Arabic Game Language strings are missing.");
    Assert(!string.IsNullOrWhiteSpace(texts["InspectCurrentSettings"]) && !string.IsNullOrWhiteSpace(texts["ClassificationArabic"]), "Arabic Registry inspection strings are missing.");
    texts.SetLanguage("en");
    Assert(texts["InspectCurrentSettings"] == "Inspect Current Settings" && !string.IsNullOrWhiteSpace(texts["ClassificationUnknown"]), "English Registry inspection strings are missing.");
    return Task.CompletedTask;
}

static Task GameLanguageUiState()
{
    using var fixture = new TempFixture();
    var previous = Environment.GetEnvironmentVariable("ARABIZE_DATA_ROOT");
    Environment.SetEnvironmentVariable("ARABIZE_DATA_ROOT", Path.Combine(fixture.Root, "app-data"));
    try
    {
        var paths = new AppPaths();
        var settings = new LauncherSettings { UiLanguage = "ar", GameDirectory = fixture.Root };
        var vm = new MainViewModel(paths, new StructuredLogger(paths), new SettingsStore(paths), settings,
            new GameDetectionService(new FakeMetadata(GameDetectionService.ExpectedProduct)), new PackageService(), new BackupManager());
        vm.SelectEnglishGameLanguageCommand.Execute(null);
        vm.SetEnglishCommand.Execute(null);
        Assert(vm.Texts.Language == "en" && vm.SelectedGameLanguage == "en", "English UI/game language state did not persist independently.");
        vm.SelectArabicGameLanguageCommand.Execute(null);
        Assert(vm.Texts.Language == "en" && vm.SelectedGameLanguage == "ar", "Changing game language changed the launcher UI language.");
        Assert(!vm.ApplyGameLanguageCommand.CanExecute(null) && !vm.RestorePreviousSettingsCommand.CanExecute(null) && !vm.LaunchGameCommand.CanExecute(null), "A guarded action is enabled.");
    }
    finally { Environment.SetEnvironmentVariable("ARABIZE_DATA_ROOT", previous); }
    return Task.CompletedTask;
}

static Task InspectArabicRegistryPair()
{
    var adapter = FakeRegistry.WithPair("arSA", "arSA");
    var result = new RegistryLanguageInspectionService(adapter).Inspect();
    Assert(result.Classification == RegistryLanguageClassification.ArabicPair, "Arabic pair was not recognized.");
    Assert(result.Locale.Exists && result.Locale.Data == "arSA" && result.Locale.Kind == "String", "LOCALE state/data/type was not preserved.");
    Assert(result.LocaleAudio.Exists && result.LocaleAudio.Data == "arSA", "LOCALE_AUDIO was not inspected.");
    return Task.CompletedTask;
}

static Task InspectMissingRegistryValues()
{
    var adapter = new FakeRegistry(RegistryKeyReadStatus.Success, FakeRegistry.Missing(RegistryLanguageScope.LocaleName), FakeRegistry.Missing(RegistryLanguageScope.LocaleAudioName));
    var result = new RegistryLanguageInspectionService(adapter).Inspect();
    Assert(result.Classification == RegistryLanguageClassification.Missing, "Missing values were not classified.");
    var oneMissing = new FakeRegistry(RegistryKeyReadStatus.Success, FakeRegistry.Missing(RegistryLanguageScope.LocaleName), FakeRegistry.String(RegistryLanguageScope.LocaleAudioName, "enUS"));
    Assert(new RegistryLanguageInspectionService(oneMissing).Inspect().Classification == RegistryLanguageClassification.Missing, "One missing value was not reported.");
    return Task.CompletedTask;
}

static Task InspectUnsupportedRegistryType()
{
    var adapter = new FakeRegistry(RegistryKeyReadStatus.Success, FakeRegistry.String(RegistryLanguageScope.LocaleName, "arSA", "ExpandString"), FakeRegistry.String(RegistryLanguageScope.LocaleAudioName, "arSA"));
    var result = new RegistryLanguageInspectionService(adapter).Inspect();
    Assert(result.Locale.Status == RegistryValueReadStatus.UnsupportedType && result.Classification == RegistryLanguageClassification.Unsupported, "Non-REG_SZ value was accepted as supported.");
    return Task.CompletedTask;
}

static Task InspectRegistryErrors()
{
    var denied = new FakeRegistry(RegistryKeyReadStatus.AccessDenied, FakeRegistry.Error(RegistryLanguageScope.LocaleName, RegistryValueReadStatus.AccessDenied), FakeRegistry.Error(RegistryLanguageScope.LocaleAudioName, RegistryValueReadStatus.AccessDenied));
    Assert(new RegistryLanguageInspectionService(denied).Inspect().Classification == RegistryLanguageClassification.Unknown, "Access denied was not distinguished as unknown.");
    var missingKey = new FakeRegistry(RegistryKeyReadStatus.MissingKey, FakeRegistry.Error(RegistryLanguageScope.LocaleName, RegistryValueReadStatus.MissingKey), FakeRegistry.Error(RegistryLanguageScope.LocaleAudioName, RegistryValueReadStatus.MissingKey));
    Assert(new RegistryLanguageInspectionService(missingKey).Inspect().Classification == RegistryLanguageClassification.Missing, "Missing key was not distinguished.");
    var unexpected = new FakeRegistry(RegistryKeyReadStatus.UnexpectedError, FakeRegistry.Error(RegistryLanguageScope.LocaleName, RegistryValueReadStatus.UnexpectedError), FakeRegistry.Error(RegistryLanguageScope.LocaleAudioName, RegistryValueReadStatus.UnexpectedError));
    Assert(new RegistryLanguageInspectionService(unexpected).Inspect().Classification == RegistryLanguageClassification.Unknown, "Unexpected Registry failure was not distinguished.");
    return Task.CompletedTask;
}

static Task InspectMixedRegistryPair()
{
    var adapter = FakeRegistry.WithPair("arSA", "enUS");
    Assert(new RegistryLanguageInspectionService(adapter).Inspect().Classification == RegistryLanguageClassification.Mixed, "Mixed values were not classified.");
    return Task.CompletedTask;
}

static Task RegistryReadScope()
{
    var adapter = FakeRegistry.WithPair("enUS", "enUS");
    _ = new RegistryLanguageInspectionService(adapter).Inspect();
    Assert(adapter.ReadNames.SequenceEqual(["LOCALE", "LOCALE_AUDIO"]), "Inspection requested names outside the two approved values.");
    return Task.CompletedTask;
}

static Task SnapshotRoundTrip()
{
    using var fixture = new TempFixture();
    var inspection = new RegistryLanguageInspectionService(FakeRegistry.WithPair("enUS", "enUS")).Inspect();
    var snapshot = RegistrySnapshotFactory.Create(inspection, TestGameIdentity());
    var store = new RegistrySnapshotStore(new AppPaths(Path.Combine(fixture.Root, "app-data")));
    var path = store.CreateNew(snapshot, Path.Combine(fixture.Root, "game"));
    var loaded = store.LoadAndVerify(path);
    Assert(RegistrySnapshotStore.SerializeCanonical(loaded.Snapshot).SequenceEqual(RegistrySnapshotStore.SerializeCanonical(snapshot)), "Snapshot did not round-trip canonically.");
    Assert(loaded.Sha256 == RegistrySnapshotStore.ComputeHash(snapshot), "Snapshot hash was not stable.");
    return Task.CompletedTask;
}

static Task RejectTamperedSnapshot()
{
    using var fixture = new TempFixture();
    var snapshot = RegistrySnapshotFactory.Create(new RegistryLanguageInspectionService(FakeRegistry.WithPair("enUS", "enUS")).Inspect(), TestGameIdentity());
    var paths = new AppPaths(Path.Combine(fixture.Root, "app-data"));
    var store = new RegistrySnapshotStore(paths);
    var path = store.CreateNew(snapshot, Path.Combine(fixture.Root, "game"));
    var content = File.ReadAllText(path).Replace("enUS", "frFR", StringComparison.Ordinal);
    File.WriteAllText(path, content);
    var rejected = false;
    try { _ = store.LoadAndVerify(path); } catch (InvalidDataException) { rejected = true; }
    Assert(rejected, "Tampered snapshot was accepted.");
    return Task.CompletedTask;
}

static Task RejectUnsupportedSnapshotSchema()
{
    var inspection = new RegistryLanguageInspectionService(FakeRegistry.WithPair("enUS", "enUS")).Inspect();
    var snapshot = RegistrySnapshotFactory.Create(inspection, TestGameIdentity()) with { SchemaVersion = 99 };
    var rejected = false;
    try { RegistrySnapshotStore.Validate(snapshot); } catch (InvalidDataException) { rejected = true; }
    Assert(rejected, "Unsupported snapshot schema was accepted.");
    return Task.CompletedTask;
}

static Task RejectDuplicateSnapshot()
{
    using var fixture = new TempFixture();
    var snapshot = RegistrySnapshotFactory.Create(new RegistryLanguageInspectionService(FakeRegistry.WithPair("enUS", "enUS")).Inspect(), TestGameIdentity());
    var store = new RegistrySnapshotStore(new AppPaths(Path.Combine(fixture.Root, "app-data")));
    var first = store.CreateNew(snapshot, Path.Combine(fixture.Root, "game"));
    var rejected = false;
    try { _ = store.CreateNew(snapshot, Path.Combine(fixture.Root, "game")); } catch (IOException) { rejected = true; }
    Assert(rejected && File.Exists(first), "Duplicate backup filename overwrote or removed the original.");
    return Task.CompletedTask;
}

static Task RestoreSnapshotExactState()
{
    using var fixture = new TempFixture();
    var fake = FakeRegistry.WithPair("enUS", "enUS");
    var inspection = new RegistryLanguageInspectionService(fake).Inspect();
    var missingLocaleInspection = inspection with { Locale = FakeRegistry.Missing(RegistryLanguageScope.LocaleName), LocaleAudio = FakeRegistry.Unsupported(RegistryLanguageScope.LocaleAudioName, "DWord", 7) };
    var snapshot = RegistrySnapshotFactory.Create(missingLocaleInspection, TestGameIdentity());
    var envelope = new RegistrySnapshotEnvelope(snapshot, RegistrySnapshotStore.ComputeHash(snapshot));
    var result = new RegistryTransactionService(fake).Restore(envelope, TestGameIdentity());
    Assert(result.Succeeded, "Snapshot restore did not verify.");
    var after = fake.ReadApprovedValues();
    Assert(!after.Locale.Exists && after.Locale.Status == RegistryValueReadStatus.Missing, "Originally missing value was not removed in fake adapter.");
    Assert(after.LocaleAudio.Kind == "DWord" && after.LocaleAudio.RawData?.GetRawText() == "7", "Original value kind/data were not restored exactly.");
    return Task.CompletedTask;
}

static Task PartialTransactionFailure()
{
    var fake = FakeRegistry.WithPair("enUS", "enUS");
    fake.FailWriteName = RegistryLanguageScope.LocaleAudioName;
    var result = new RegistryTransactionService(fake).ApplyArabicPair();
    Assert(result.IsPartial && !result.Succeeded, "Partial transaction failure was not reported accurately.");
    Assert(result.Operations[0].Succeeded && !result.Operations[1].Succeeded, "Per-value transaction outcomes were lost.");
    return Task.CompletedTask;
}

static Task RegistryUiExplicitAction()
{
    using var fixture = new TempFixture();
    var oldRoot = Environment.GetEnvironmentVariable("ARABIZE_DATA_ROOT");
    Environment.SetEnvironmentVariable("ARABIZE_DATA_ROOT", Path.Combine(fixture.Root, "ui-data"));
    try
    {
        var paths = new AppPaths();
        paths.EnsureCreated();
        var fake = FakeRegistry.WithPair("enUS", "enUS");
        var vm = new MainViewModel(paths, new StructuredLogger(paths), new SettingsStore(paths), new LauncherSettings(),
            new GameDetectionService(new FakeMetadata(GameDetectionService.ExpectedProduct)), new PackageService(), new BackupManager(), new RegistryLanguageInspectionService(fake));
        Assert(fake.ReadNames.Count == 0, "Registry was inspected during view-model construction.");
        Assert(vm.RegistryConfigurationText == vm.Texts["RegistryNotInspected"], "Initial UI state did not remain not inspected.");
        vm.InspectRegistryCommand.Execute(null);
        Assert(fake.ReadNames.SequenceEqual(["LOCALE", "LOCALE_AUDIO"]), "Explicit inspection did not read exactly the approved values.");
        Assert(vm.RegistryConfigurationText == vm.Texts["ClassificationEnglish"], "Inspection result was not displayed.");
        Assert(vm.RegistryInspectionTimeText != vm.Texts["InspectionNotRun"], "Inspection timestamp was not displayed.");
    }
    finally { Environment.SetEnvironmentVariable("ARABIZE_DATA_ROOT", oldRoot); }
    return Task.CompletedTask;
}

static Task ApplyArabicWithVerifiedSnapshot()
{
    using var fixture = new TempFixture();
    var fake = FakeRegistry.WithPair("enUS", "enUS");
    var prepared = PrepareSwitchVm(fixture, fake, _ => true);
    prepared.Vm.ValidateGameCommand.Execute(null);
    prepared.Vm.InspectRegistryCommand.Execute(null);
    Assert(prepared.Vm.ApplyGameLanguageCommand.CanExecute(null), "Apply should be enabled after compatible detection and valid inspection.");
    Assert(!prepared.Vm.RestorePreviousSettingsCommand.CanExecute(null) && prepared.Vm.LaunchGameCommand.CanExecute(null), "Restore should wait for a snapshot while exact-build Launch should be enabled.");
    fake.BeforeWrite = _ => Assert(Directory.EnumerateFiles(prepared.Store.BackupDirectory, "*.json").Any(), "A Registry write was attempted before its snapshot existed.");
    prepared.Vm.ApplyGameLanguageCommand.Execute(null);
    var current = fake.ReadApprovedValues();
    Assert(current.Locale.Data == "arSA" && current.LocaleAudio.Data == "arSA", "Arabic pair was not applied by the fake adapter.");
    Assert(prepared.Vm.RegistryTransactionProgressText == prepared.Vm.Texts["TransactionSucceeded"], "The UI did not report verified success.");
    Assert(prepared.Vm.RegistryBackupStatusText.Contains(".json", StringComparison.Ordinal), "Verified backup was not displayed.");
    Assert(prepared.Vm.RegistrySnapshots.Count == 1, "The newly created Apply snapshot was not listed for later restoration.");
    Assert(fake.WriteNames.SequenceEqual(["LOCALE", "LOCALE_AUDIO"]), "Apply wrote names outside the approved pair or wrong order.");
    Assert(!prepared.Vm.ApplyGameLanguageCommand.CanExecute(null), "Apply remained enabled after the selected pair was already configured.");
    return Task.CompletedTask;
}

static Task ApplyEnglishPair()
{
    using var fixture = new TempFixture();
    var fake = FakeRegistry.WithPair("arSA", "arSA");
    var prepared = PrepareSwitchVm(fixture, fake, _ => true);
    prepared.Vm.SelectEnglishGameLanguageCommand.Execute(null);
    prepared.Vm.ValidateGameCommand.Execute(null);
    prepared.Vm.InspectRegistryCommand.Execute(null);
    Assert(prepared.Vm.ApplyGameLanguageCommand.CanExecute(null), "English target should be available.");
    prepared.Vm.ApplyGameLanguageCommand.Execute(null);
    var current = fake.ReadApprovedValues();
    Assert(current.Locale.Data == "enUS" && current.LocaleAudio.Data == "enUS", "English pair was not applied.");
    return Task.CompletedTask;
}

static Task SnapshotVerificationFailureBlocksApply()
{
    using var fixture = new TempFixture();
    var fake = FakeRegistry.WithPair("enUS", "enUS");
    var prepared = PrepareSwitchVm(fixture, fake, _ => true, failSnapshotLoad: true);
    prepared.Vm.ValidateGameCommand.Execute(null);
    prepared.Vm.InspectRegistryCommand.Execute(null);
    prepared.Vm.ApplyGameLanguageCommand.Execute(null);
    Assert(fake.WriteNames.Count == 0, "Mutation began despite snapshot verification failure.");
    Assert(prepared.Vm.RegistryBackupStatusText.Contains("failed", StringComparison.OrdinalIgnoreCase), "Snapshot failure was not shown.");
    Assert(!prepared.Vm.ApplyGameLanguageCommand.CanExecute(null), "Apply was not disabled after snapshot creation failed verification.");
    Assert(Directory.EnumerateFiles(prepared.Store.BackupDirectory, "*.json").Any(), "A created snapshot was automatically removed after verification failure.");
    return Task.CompletedTask;
}

static Task PartialApplySuccessfulRollback()
{
    using var fixture = new TempFixture();
    var fake = FakeRegistry.WithPair("enUS", "enUS");
    fake.FailWriteOnceName = RegistryLanguageScope.LocaleAudioName;
    var prepared = PrepareSwitchVm(fixture, fake, _ => true);
    prepared.Vm.ValidateGameCommand.Execute(null);
    prepared.Vm.InspectRegistryCommand.Execute(null);
    prepared.Vm.ApplyGameLanguageCommand.Execute(null);
    var current = fake.ReadApprovedValues();
    Assert(current.Locale.Data == "enUS" && current.LocaleAudio.Data == "enUS", "Rollback did not restore the original pair.");
    Assert(prepared.Vm.RegistryRollbackStatusText == prepared.Vm.Texts["RollbackSucceeded"], "Successful rollback was not reported.");
    Assert(prepared.Vm.RegistryTransactionDetailsText.Contains("LOCALE_AUDIO: failed", StringComparison.Ordinal), "Per-value apply failure was concealed.");
    Assert(Directory.EnumerateFiles(prepared.Store.BackupDirectory, "*.json").Any(), "Recovery snapshot was not retained.");
    return Task.CompletedTask;
}

static Task PartialApplyFailedRollback()
{
    using var fixture = new TempFixture();
    var fake = FakeRegistry.WithPair("enUS", "enUS");
    fake.FailWriteName = RegistryLanguageScope.LocaleName;
    var prepared = PrepareSwitchVm(fixture, fake, _ => true);
    prepared.Vm.ValidateGameCommand.Execute(null);
    prepared.Vm.InspectRegistryCommand.Execute(null);
    prepared.Vm.ApplyGameLanguageCommand.Execute(null);
    Assert(prepared.Vm.RegistryRollbackStatusText.Contains("manual recovery", StringComparison.OrdinalIgnoreCase), "Failed rollback did not warn about manual recovery.");
    Assert(Directory.EnumerateFiles(prepared.Store.BackupDirectory, "*.json").Any(), "Snapshot was not retained after rollback failure.");
    Assert(prepared.Vm.RegistryTransactionDetailsText.Contains("LOCALE: failed", StringComparison.Ordinal), "Failed rollback value outcome is missing.");
    return Task.CompletedTask;
}

static Task RegistryConditionsBlockApply()
{
    using var fixture1 = new TempFixture();
    var missingKey = new FakeRegistry(RegistryKeyReadStatus.MissingKey,
        FakeRegistry.Error(RegistryLanguageScope.LocaleName, RegistryValueReadStatus.MissingKey), FakeRegistry.Error(RegistryLanguageScope.LocaleAudioName, RegistryValueReadStatus.MissingKey));
    var first = PrepareSwitchVm(fixture1, missingKey, _ => true);
    first.Vm.ValidateGameCommand.Execute(null);
    first.Vm.InspectRegistryCommand.Execute(null);
    Assert(!first.Vm.ApplyGameLanguageCommand.CanExecute(null) && missingKey.WriteNames.Count == 0, "Missing key did not block apply.");

    using var fixture2 = new TempFixture();
    var denied = new FakeRegistry(RegistryKeyReadStatus.AccessDenied,
        FakeRegistry.Error(RegistryLanguageScope.LocaleName, RegistryValueReadStatus.AccessDenied), FakeRegistry.Error(RegistryLanguageScope.LocaleAudioName, RegistryValueReadStatus.AccessDenied));
    var second = PrepareSwitchVm(fixture2, denied, _ => true);
    second.Vm.ValidateGameCommand.Execute(null);
    second.Vm.InspectRegistryCommand.Execute(null);
    Assert(!second.Vm.ApplyGameLanguageCommand.CanExecute(null), "Denied Registry read did not block apply.");

    using var fixture3 = new TempFixture();
    var wrongType = new FakeRegistry(RegistryKeyReadStatus.Success,
        FakeRegistry.Unsupported(RegistryLanguageScope.LocaleName, "DWord", 1), FakeRegistry.String(RegistryLanguageScope.LocaleAudioName, "enUS"));
    var third = PrepareSwitchVm(fixture3, wrongType, _ => true);
    third.Vm.ValidateGameCommand.Execute(null);
    third.Vm.InspectRegistryCommand.Execute(null);
    Assert(!third.Vm.ApplyGameLanguageCommand.CanExecute(null), "Unsupported Registry value type did not block apply.");
    return Task.CompletedTask;
}

static Task CancelApplyConfirmation()
{
    using var fixture = new TempFixture();
    var fake = FakeRegistry.WithPair("enUS", "enUS");
    var promptSeen = false;
    var prepared = PrepareSwitchVm(fixture, fake, message => { promptSeen = message.Contains("LOCALE=", StringComparison.Ordinal) && message.Contains("arSA", StringComparison.Ordinal); return false; });
    prepared.Vm.ValidateGameCommand.Execute(null);
    prepared.Vm.InspectRegistryCommand.Execute(null);
    prepared.Vm.ApplyGameLanguageCommand.Execute(null);
    Assert(promptSeen && fake.WriteNames.Count == 0, "Confirmation was skipped or cancellation still mutated values.");
    Assert(!Directory.Exists(prepared.Store.BackupDirectory) || !Directory.EnumerateFiles(prepared.Store.BackupDirectory, "*.json").Any(), "A backup was created before user confirmation.");
    return Task.CompletedTask;
}

static Task AlreadyConfiguredDisablesApply()
{
    using var fixture = new TempFixture();
    var fake = FakeRegistry.WithPair("arSA", "arSA");
    var prepared = PrepareSwitchVm(fixture, fake, _ => throw new InvalidOperationException("Confirmation should not appear."));
    prepared.Vm.ValidateGameCommand.Execute(null);
    prepared.Vm.InspectRegistryCommand.Execute(null);
    Assert(!prepared.Vm.ApplyGameLanguageCommand.CanExecute(null), "Apply was enabled for an already configured target.");
    Assert(prepared.Vm.ApplyLanguageStatusText == prepared.Vm.Texts["TargetAlreadyConfigured"], "Already-configured reason was not shown.");
    return Task.CompletedTask;
}

static Task ReadBackMismatch()
{
    var fake = FakeRegistry.WithPair("enUS", "enUS");
    fake.MismatchReadName = RegistryLanguageScope.LocaleName;
    var result = new RegistryTransactionService(fake).ApplyArabicPair();
    Assert(!result.Operations[0].Succeeded && result.Operations[1].Succeeded, "Read-back mismatch was not detected per value.");
    return Task.CompletedTask;
}

static (MainViewModel Vm, FakeRegistry Registry, RegistrySnapshotStore Store) PrepareSwitchVm(
    TempFixture fixture, FakeRegistry registry, Func<string, bool> confirm, bool failSnapshotLoad = false, Func<string, bool>? restoreConfirm = null)
{
    var root = Path.Combine(fixture.Root, "app-data");
    var paths = new AppPaths(root);
    paths.EnsureCreated();
    var game = fixture.CreateGame();
    var settings = new LauncherSettings { GameDirectory = game, UiLanguage = "en" };
    var inspection = new RegistryLanguageInspectionService(registry);
    var transaction = new RegistryTransactionService(registry);
    var realStore = new RegistrySnapshotStore(paths);
    IRegistrySnapshotStore storePort = failSnapshotLoad ? new FailSnapshotVerificationStore(realStore) : realStore;
    var vm = new MainViewModel(paths, new StructuredLogger(paths), new SettingsStore(paths), settings,
        new GameDetectionService(new FakeMetadata(GameDetectionService.ExpectedProduct)), new PackageService(), new BackupManager(),
        inspection, transaction, storePort, confirm, restoreConfirm,
        new GameLaunchService(new GameDetectionService(new FakeMetadata(GameDetectionService.ExpectedProduct)), new FakeProcessLauncher()));
    return (vm, registry, realStore);
}

static Task ListOnlyValidSnapshots()
{
    using var fixture = new TempFixture();
    var game = fixture.CreateGame();
    var store = new RegistrySnapshotStore(new AppPaths(Path.Combine(fixture.Root, "app-data")));
    var good = RegistrySnapshotFactory.Create(new RegistryLanguageInspectionService(FakeRegistry.WithPair("enUS", "enUS")).Inspect(), TestGameIdentity());
    var goodPath = store.CreateNew(good, game);
    Directory.CreateDirectory(Path.Combine(fixture.Root, "elsewhere"));
    File.WriteAllText(Path.Combine(fixture.Root, "elsewhere", "outside.json"), File.ReadAllText(goodPath));
    File.WriteAllText(Path.Combine(store.BackupDirectory, "corrupt.json"), "not a snapshot");
    var wrongGame = TestGameIdentity() with { Version = "1.1.2.1279291" };
    var wrong = good with { TransactionId = Guid.NewGuid().ToString("N"), Game = wrongGame };
    File.WriteAllText(Path.Combine(store.BackupDirectory, wrong.TransactionId + ".json"), JsonSerializer.Serialize(new RegistrySnapshotEnvelope(wrong, RegistrySnapshotStore.ComputeHash(wrong)), new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    var listed = store.ListValidSnapshots(TestGameIdentity());
    Assert(listed.Count == 1 && listed[0].Envelope.Snapshot.TransactionId == good.TransactionId, "Snapshot listing included corrupt, incompatible, or out-of-directory files.");
    return Task.CompletedTask;
}

static Task RestoreEnglishSnapshot()
{
    using var fixture = new TempFixture();
    var current = FakeRegistry.WithPair("arSA", "arSA");
    var prepared = PrepareSwitchVm(fixture, current, _ => true, restoreConfirm: _ => true);
    prepared.Vm.ValidateGameCommand.Execute(null);
    prepared.Vm.InspectRegistryCommand.Execute(null);
    var prior = RegistrySnapshotFactory.Create(new RegistryLanguageInspectionService(FakeRegistry.WithPair("enUS", "enUS")).Inspect(), TestGameIdentity());
    prepared.Store.CreateNew(prior, prepared.Vm.Game.InstallationPath);
    prepared.Vm.RefreshRegistrySnapshotsCommand.Execute(null);
    Assert(prepared.Vm.CanRestorePreviousSettings, "Valid English snapshot was not offered for restoration.");
    Assert(prepared.Vm.SelectedSnapshotDetailsText.Contains("enUS", StringComparison.Ordinal), "Saved English values were not displayed.");
    prepared.Vm.RestorePreviousSettingsCommand.Execute(null);
    var after = current.ReadApprovedValues();
    Assert(after.Locale.Data == "enUS" && after.LocaleAudio.Data == "enUS", "English snapshot was not restored.");
    Assert(prepared.Vm.RestoreStatusText == prepared.Vm.Texts["RestorationSucceeded"], "Successful restore was not reported.");
    Assert(Directory.EnumerateFiles(prepared.Store.BackupDirectory, "*.json").Count() == 2, "Recovery snapshot was not retained.");
    return Task.CompletedTask;
}

static Task RestoreArabicSnapshot()
{
    using var fixture = new TempFixture();
    var current = FakeRegistry.WithPair("enUS", "enUS");
    var prepared = PrepareSwitchVm(fixture, current, _ => true);
    prepared.Vm.ValidateGameCommand.Execute(null);
    prepared.Vm.InspectRegistryCommand.Execute(null);
    var prior = RegistrySnapshotFactory.Create(new RegistryLanguageInspectionService(FakeRegistry.WithPair("arSA", "arSA")).Inspect(), TestGameIdentity());
    prepared.Store.CreateNew(prior, prepared.Vm.Game.InstallationPath);
    prepared.Vm.RefreshRegistrySnapshotsCommand.Execute(null);
    var result = new RegistryTransactionService(current).Restore(prepared.Vm.SelectedRegistrySnapshot!.Envelope, TestGameIdentity());
    Assert(result.Succeeded, "Arabic snapshot did not restore through the transaction service.");
    var after = current.ReadApprovedValues();
    Assert(after.Locale.Data == "arSA" && after.LocaleAudio.Data == "arSA", "Arabic values were not restored.");
    return Task.CompletedTask;
}

static Task MissingKeyBlocksRestore()
{
    using var fixture = new TempFixture();
    var missing = new FakeRegistry(RegistryKeyReadStatus.MissingKey, FakeRegistry.Error(RegistryLanguageScope.LocaleName, RegistryValueReadStatus.MissingKey), FakeRegistry.Error(RegistryLanguageScope.LocaleAudioName, RegistryValueReadStatus.MissingKey));
    var prepared = PrepareSwitchVm(fixture, missing, _ => true);
    prepared.Vm.ValidateGameCommand.Execute(null);
    prepared.Vm.InspectRegistryCommand.Execute(null);
    var absentInspection = new RegistryLanguageInspectionService(missing).Inspect();
    var snapshot = RegistrySnapshotFactory.Create(absentInspection, TestGameIdentity());
    prepared.Store.CreateNew(snapshot, prepared.Vm.Game.InstallationPath);
    prepared.Vm.RefreshRegistrySnapshotsCommand.Execute(null);
    Assert(!prepared.Vm.RestorePreviousSettingsCommand.CanExecute(null), "A missing LAZR key enabled Restore.");
    Assert(missing.WriteNames.Count == 0 && missing.DeleteNames.Count == 0, "Missing-key UI inspection mutated fake Registry values.");
    return Task.CompletedTask;
}

static Task RecoverySnapshotBeforeRestore()
{
    using var fixture = new TempFixture();
    var current = FakeRegistry.WithPair("arSA", "arSA");
    var prepared = PrepareSwitchVm(fixture, current, _ => true, restoreConfirm: _ => true);
    prepared.Vm.ValidateGameCommand.Execute(null);
    prepared.Vm.InspectRegistryCommand.Execute(null);
    var prior = RegistrySnapshotFactory.Create(new RegistryLanguageInspectionService(FakeRegistry.WithPair("enUS", "enUS")).Inspect(), TestGameIdentity());
    prepared.Store.CreateNew(prior, prepared.Vm.Game.InstallationPath);
    prepared.Vm.RefreshRegistrySnapshotsCommand.Execute(null);
    current.BeforeWrite = _ => Assert(Directory.EnumerateFiles(prepared.Store.BackupDirectory, "*.json").Count() >= 2, "Restore wrote before creating and verifying a recovery snapshot.");
    prepared.Vm.RestorePreviousSettingsCommand.Execute(null);
    Assert(current.WriteNames.Count == 2, "Expected two per-value fake writes during restoration.");
    return Task.CompletedTask;
}

static Task RecoveryVerificationFailureBlocksRestore()
{
    using var fixture = new TempFixture();
    var current = FakeRegistry.WithPair("arSA", "arSA");
    var prepared = PrepareSwitchVm(fixture, current, _ => true, restoreConfirm: _ => true);
    prepared.Vm.ValidateGameCommand.Execute(null);
    prepared.Vm.InspectRegistryCommand.Execute(null);
    var prior = RegistrySnapshotFactory.Create(new RegistryLanguageInspectionService(FakeRegistry.WithPair("enUS", "enUS")).Inspect(), TestGameIdentity());
    prepared.Store.CreateNew(prior, prepared.Vm.Game.InstallationPath);
    prepared.Vm.RefreshRegistrySnapshotsCommand.Execute(null);
    var faultStore = new FailNthSnapshotLoadStore(prepared.Store, 2);
    prepared = ReplaceSnapshotStore(prepared, fixture, current, faultStore);
    prepared.Vm.ValidateGameCommand.Execute(null);
    prepared.Vm.InspectRegistryCommand.Execute(null);
    prepared.Vm.RefreshRegistrySnapshotsCommand.Execute(null);
    prepared.Vm.RestorePreviousSettingsCommand.Execute(null);
    Assert(current.WriteNames.Count == 0 && current.DeleteNames.Count == 0, "Restoration began without a verified recovery snapshot.");
    Assert(prepared.Vm.RestoreStatusText == prepared.Vm.Texts["RecoverySnapshotFailed"], "Recovery verification failure was not shown.");
    return Task.CompletedTask;
}

static Task PartialRestoreSuccessfulRollback()
{
    using var fixture = new TempFixture();
    var current = FakeRegistry.WithPair("arSA", "arSA");
    current.FailWriteOnceName = RegistryLanguageScope.LocaleAudioName;
    var prepared = PrepareSwitchVm(fixture, current, _ => true, restoreConfirm: _ => true);
    prepared.Vm.ValidateGameCommand.Execute(null);
    prepared.Vm.InspectRegistryCommand.Execute(null);
    var prior = RegistrySnapshotFactory.Create(new RegistryLanguageInspectionService(FakeRegistry.WithPair("enUS", "enUS")).Inspect(), TestGameIdentity());
    prepared.Store.CreateNew(prior, prepared.Vm.Game.InstallationPath);
    prepared.Vm.RefreshRegistrySnapshotsCommand.Execute(null);
    prepared.Vm.RestorePreviousSettingsCommand.Execute(null);
    var after = current.ReadApprovedValues();
    Assert(after.Locale.Data == "arSA" && after.LocaleAudio.Data == "arSA", "Failed restore was not rolled back to the current pre-restore values.");
    Assert(prepared.Vm.RegistryRollbackStatusText == prepared.Vm.Texts["RollbackSucceeded"], "Successful rollback was not reported.");
    Assert(prepared.Vm.RegistryTransactionDetailsText.Contains("LOCALE_AUDIO: failed", StringComparison.Ordinal), "Partial restore outcome was not shown.");
    Assert(Directory.EnumerateFiles(prepared.Store.BackupDirectory, "*.json").Count() == 2, "Snapshot pair was not preserved.");
    return Task.CompletedTask;
}

static Task PartialRestoreFailedRollback()
{
    using var fixture = new TempFixture();
    var current = FakeRegistry.WithPair("arSA", "arSA");
    current.FailWriteName = RegistryLanguageScope.LocaleName;
    var prepared = PrepareSwitchVm(fixture, current, _ => true, restoreConfirm: _ => true);
    prepared.Vm.ValidateGameCommand.Execute(null);
    prepared.Vm.InspectRegistryCommand.Execute(null);
    var prior = RegistrySnapshotFactory.Create(new RegistryLanguageInspectionService(FakeRegistry.WithPair("enUS", "enUS")).Inspect(), TestGameIdentity());
    prepared.Store.CreateNew(prior, prepared.Vm.Game.InstallationPath);
    prepared.Vm.RefreshRegistrySnapshotsCommand.Execute(null);
    prepared.Vm.RestorePreviousSettingsCommand.Execute(null);
    Assert(prepared.Vm.RegistryRollbackStatusText.Contains("recovery", StringComparison.OrdinalIgnoreCase), "Failed rollback did not identify recovery snapshot.");
    Assert(Directory.EnumerateFiles(prepared.Store.BackupDirectory, "*.json").Count() == 2, "Recovery snapshot was not preserved after rollback failure.");
    return Task.CompletedTask;
}

static Task LaunchUsesValidatedPaths()
{
    using var fixture = new TempFixture();
    var game = fixture.CreateGame();
    var detector = new GameDetectionService(new FakeMetadata(GameDetectionService.ExpectedProduct));
    var process = new FakeProcessLauncher();
    var result = new GameLaunchService(detector, process).LaunchDetectedGame(game);
    Assert(result.Status == GameLaunchStatus.Started, "Valid build launch was not accepted.");
    Assert(process.StartCalls == 1 && process.LastExecutable == Path.Combine(game, "MW2CR.exe"), "Unexpected executable path was launched.");
    Assert(process.LastWorkingDirectory == game, "Game working directory was not set to the validated installation.");
    return Task.CompletedTask;
}

static Task LaunchRejectsUnsupportedBuild()
{
    using var fixture = new TempFixture();
    var game = fixture.CreateGame(version: "1.1.2.1279291");
    var process = new FakeProcessLauncher();
    var result = new GameLaunchService(new GameDetectionService(new FakeMetadata(GameDetectionService.ExpectedProduct)), process).LaunchDetectedGame(game);
    Assert(result.Status == GameLaunchStatus.UnsupportedInstallation && process.StartCalls == 0, "Unsupported build was launched.");
    var invalid = new FakeGameDetector(new() { Status = GameDetectionStatus.Unsupported, Message = "invalid" });
    Assert(new GameLaunchService(invalid, process).LaunchDetectedGame(game).Status == GameLaunchStatus.UnsupportedInstallation, "Invalid detection was accepted for launch.");
    return Task.CompletedTask;
}

static Task LaunchRejectsMissingOrMismatchedExecutable()
{
    using var fixture = new TempFixture();
    var game = fixture.CreateGame();
    var validBeforeRemoval = ValidatedDetection(game);
    var process = new FakeProcessLauncher();
    File.Delete(Path.Combine(game, "MW2CR.exe"));
    var missing = new GameLaunchService(new GameDetectionService(new FakeMetadata(GameDetectionService.ExpectedProduct)), process).LaunchDetectedGame(game);
    Assert(missing.Status == GameLaunchStatus.UnsupportedInstallation && process.StartCalls == 0, "Missing executable was launched.");
    var alternate = Path.Combine(fixture.Root, "alternate.exe");
    File.WriteAllText(alternate, "fake marker");
    var forged = new GameDetectionResult
    {
        Status = validBeforeRemoval.Status, InstallationPath = validBeforeRemoval.InstallationPath,
        Identity = validBeforeRemoval.Identity, GameVersion = validBeforeRemoval.GameVersion,
        BuildProduct = validBeforeRemoval.BuildProduct, BuildName = validBeforeRemoval.BuildName,
        BuildKey = validBeforeRemoval.BuildKey, Branch = validBeforeRemoval.Branch, ExecutablePath = alternate
    };
    var mismatch = new GameLaunchService(new FakeGameDetector(forged), process).LaunchDetectedGame(game);
    Assert(mismatch.Status == GameLaunchStatus.UnsupportedInstallation && process.StartCalls == 0, "Mismatched executable path was launched.");
    return Task.CompletedTask;
}

static Task LaunchRejectsExistingProcess()
{
    using var fixture = new TempFixture();
    var game = fixture.CreateGame();
    var process = new FakeProcessLauncher { Running = true };
    var result = new GameLaunchService(new GameDetectionService(new FakeMetadata(GameDetectionService.ExpectedProduct)), process).LaunchDetectedGame(game);
    Assert(result.Status == GameLaunchStatus.AlreadyRunning && process.StartCalls == 0, "A duplicate game process was started.");
    return Task.CompletedTask;
}

static Task LaunchReportsStartFailure()
{
    using var fixture = new TempFixture();
    var game = fixture.CreateGame();
    var process = new FakeProcessLauncher { ThrowOnStart = true };
    var result = new GameLaunchService(new GameDetectionService(new FakeMetadata(GameDetectionService.ExpectedProduct)), process).LaunchDetectedGame(game);
    Assert(result.Status == GameLaunchStatus.Failed && process.StartCalls == 1, "Process start error was not returned.");
    return Task.CompletedTask;
}

static Task LaunchUiExplicitClick()
{
    using var fixture = new TempFixture();
    var game = fixture.CreateGame();
    var process = new FakeProcessLauncher();
    var detector = new GameDetectionService(new FakeMetadata(GameDetectionService.ExpectedProduct));
    var paths = new AppPaths(Path.Combine(fixture.Root, "app-data"));
    paths.EnsureCreated();
    var vm = new MainViewModel(paths, new StructuredLogger(paths), new SettingsStore(paths), new LauncherSettings { GameDirectory = game }, detector,
        new PackageService(), new BackupManager(), gameLaunchService: new GameLaunchService(detector, process));
    Assert(process.StartCalls == 0, "Game launch occurred before explicit user action.");
    vm.ValidateGameCommand.Execute(null);
    Assert(vm.LaunchGameCommand.CanExecute(null) && process.StartCalls == 0, "Validated UI launch was started automatically.");
    vm.LaunchGameCommand.Execute(null);
    Assert(process.StartCalls == 1 && vm.LaunchStatusText == vm.Texts["LaunchSucceeded"], "Explicit launch action did not invoke the service/report success.");
    return Task.CompletedTask;
}

static Task RestoreUiConfirmationCancellation()
{
    using var fixture = new TempFixture();
    var current = FakeRegistry.WithPair("arSA", "arSA");
    var prompt = false;
    var prepared = PrepareSwitchVm(fixture, current, _ => true, restoreConfirm: message => { prompt = message.Contains("enUS", StringComparison.Ordinal); return false; });
    prepared.Vm.ValidateGameCommand.Execute(null);
    prepared.Vm.InspectRegistryCommand.Execute(null);
    var snapshot = RegistrySnapshotFactory.Create(new RegistryLanguageInspectionService(FakeRegistry.WithPair("enUS", "enUS")).Inspect(), TestGameIdentity());
    prepared.Store.CreateNew(snapshot, prepared.Vm.Game.InstallationPath);
    prepared.Vm.RefreshRegistrySnapshotsCommand.Execute(null);
    prepared.Vm.RestorePreviousSettingsCommand.Execute(null);
    Assert(prompt && current.WriteNames.Count == 0 && current.DeleteNames.Count == 0, "Restore confirmation was skipped or cancellation mutated values.");
    Assert(prepared.Vm.RestoreStatusText == prepared.Vm.Texts["RestoreCancelled"], "Restore cancellation status is missing.");
    Assert(Directory.EnumerateFiles(prepared.Store.BackupDirectory, "*.json").Count() == 1, "A recovery snapshot was created before restore confirmation.");
    return Task.CompletedTask;
}

static Task RestoreLaunchMessagesBilingual()
{
    var texts = new TextCatalog();
    texts.SetLanguage("ar");
    Assert(!string.IsNullOrWhiteSpace(texts["RestorationSucceeded"]) && !string.IsNullOrWhiteSpace(texts["LaunchSucceeded"]) && !string.IsNullOrWhiteSpace(texts["GameAlreadyRunning"]), "Arabic Phase 16D strings are missing.");
    texts.SetLanguage("en");
    Assert(!string.IsNullOrWhiteSpace(texts["RestorationSucceeded"]) && !string.IsNullOrWhiteSpace(texts["LaunchFailed"]) && !string.IsNullOrWhiteSpace(texts["RestoreRollbackManualRecovery"]), "English Phase 16D strings are missing.");
    return Task.CompletedTask;
}

static Task RejectIncompatibleRestoreSnapshot()
{
    var fake = FakeRegistry.WithPair("arSA", "arSA");
    var source = RegistrySnapshotFactory.Create(new RegistryLanguageInspectionService(FakeRegistry.WithPair("enUS", "enUS")).Inspect(), TestGameIdentity());
    var incompatible = source with { Game = source.Game with { Version = "1.1.2.1279291" } };
    var envelope = new RegistrySnapshotEnvelope(incompatible, RegistrySnapshotStore.ComputeHash(incompatible));
    var rejected = false;
    try { _ = new RegistryTransactionService(fake).Restore(envelope, TestGameIdentity()); } catch (InvalidDataException) { rejected = true; }
    Assert(rejected && fake.WriteNames.Count == 0 && fake.DeleteNames.Count == 0, "Incompatible snapshot was restored or mutated values.");
    return Task.CompletedTask;
}

static Task UnsupportedUiActionsDisabled()
{
    using var fixture = new TempFixture();
    var game = fixture.CreateGame();
    var paths = new AppPaths(Path.Combine(fixture.Root, "app-data"));
    paths.EnsureCreated();
    var detector = new GameDetectionService(new FakeMetadata("Wrong Game"));
    var process = new FakeProcessLauncher();
    var registry = FakeRegistry.WithPair("enUS", "enUS");
    var vm = new MainViewModel(paths, new StructuredLogger(paths), new SettingsStore(paths), new LauncherSettings { GameDirectory = game }, detector,
        new PackageService(), new BackupManager(), new RegistryLanguageInspectionService(registry), new RegistryTransactionService(registry), new RegistrySnapshotStore(paths),
        _ => true, _ => true, new GameLaunchService(detector, process));
    vm.ValidateGameCommand.Execute(null);
    vm.InspectRegistryCommand.Execute(null);
    Assert(!vm.RestorePreviousSettingsCommand.CanExecute(null) && !vm.LaunchGameCommand.CanExecute(null), "Unsupported installation enabled Restore or Launch.");
    Assert(process.StartCalls == 0, "Unsupported installation started the process.");
    return Task.CompletedTask;
}

static (MainViewModel Vm, FakeRegistry Registry, RegistrySnapshotStore Store) ReplaceSnapshotStore(
    (MainViewModel Vm, FakeRegistry Registry, RegistrySnapshotStore Store) old, TempFixture fixture, FakeRegistry registry, IRegistrySnapshotStore store)
{
    var paths = new AppPaths(Path.Combine(fixture.Root, "app-data"));
    var detector = new GameDetectionService(new FakeMetadata(GameDetectionService.ExpectedProduct));
    var vm = new MainViewModel(paths, new StructuredLogger(paths), new SettingsStore(paths), new LauncherSettings { GameDirectory = old.Vm.GameDirectory, UiLanguage = "en" },
        detector, new PackageService(), new BackupManager(), new RegistryLanguageInspectionService(registry), new RegistryTransactionService(registry), store,
        _ => true, _ => true, new GameLaunchService(detector, new FakeProcessLauncher()));
    return (vm, registry, old.Store);
}

static GameDetectionResult ValidatedDetection(string game)
{
    var detector = new GameDetectionService(new FakeMetadata(GameDetectionService.ExpectedProduct));
    return detector.Detect(game);
}

static RegistryGameIdentity TestGameIdentity() => new(GameDetectionService.GameId, GameDetectionService.ExpectedBuildProduct,
    GameDetectionService.ExpectedVersion, GameDetectionService.ExpectedBuildName, TempFixture.TestBuildKey);

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

sealed class FakeRegistry : IRegistryMutationAdapter
{
    private readonly Dictionary<string, RegistryValueInspection> _values = new(StringComparer.Ordinal);
    public RegistryKeyReadStatus KeyStatus { get; }
    public List<string> ReadNames { get; } = [];
    public List<string> WriteNames { get; } = [];
    public List<string> DeleteNames { get; } = [];
    public string? FailWriteName { get; set; }
    public string? FailWriteOnceName { get; set; }
    public string? FailDeleteName { get; set; }
    public string? MismatchReadName { get; set; }
    public Action<string>? BeforeWrite { get; set; }
    private readonly HashSet<string> _pendingMismatches = new(StringComparer.Ordinal);
    private readonly HashSet<string> _failedOnce = new(StringComparer.Ordinal);

    public FakeRegistry(RegistryKeyReadStatus keyStatus, RegistryValueInspection locale, RegistryValueInspection audio)
    {
        KeyStatus = keyStatus;
        _values[locale.Name] = locale;
        _values[audio.Name] = audio;
    }

    public static FakeRegistry WithPair(string locale, string audio) => new(RegistryKeyReadStatus.Success,
        String(RegistryLanguageScope.LocaleName, locale), String(RegistryLanguageScope.LocaleAudioName, audio));

    public static RegistryValueInspection String(string name, string data, string kind = "String")
    {
        var raw = JsonSerializer.SerializeToElement(data);
        return new(name, kind == "String" ? RegistryValueReadStatus.PresentString : RegistryValueReadStatus.UnsupportedType,
            true, data, kind, RawData: raw);
    }

    public static RegistryValueInspection Unsupported(string name, string kind, object data)
    {
        var raw = JsonSerializer.SerializeToElement(data, data.GetType());
        return new(name, RegistryValueReadStatus.UnsupportedType, true, raw.GetRawText(), kind, RawData: raw);
    }

    public static RegistryValueInspection Missing(string name) => new(name, RegistryValueReadStatus.Missing, false, null, null);
    public static RegistryValueInspection Error(string name, RegistryValueReadStatus status) => new(name, status, false, null, null, "Fake read result.");

    public RegistryLanguageReadResult ReadApprovedValues() => new(KeyStatus,
        ReadValue(RegistryLanguageScope.LocaleName), ReadValue(RegistryLanguageScope.LocaleAudioName),
        KeyStatus == RegistryKeyReadStatus.Success ? null : "Fake key status.");

    public RegistryValueInspection ReadValue(string name)
    {
        if (name is not (RegistryLanguageScope.LocaleName or RegistryLanguageScope.LocaleAudioName))
            throw new InvalidOperationException("Attempted out-of-scope Registry read.");
        ReadNames.Add(name);
        if (KeyStatus != RegistryKeyReadStatus.Success)
            return Error(name, KeyStatus switch
            {
                RegistryKeyReadStatus.MissingKey => RegistryValueReadStatus.MissingKey,
                RegistryKeyReadStatus.AccessDenied => RegistryValueReadStatus.AccessDenied,
                _ => RegistryValueReadStatus.UnexpectedError
            });
        if (!_values.TryGetValue(name, out var value)) return Missing(name);
        if (name == MismatchReadName && _pendingMismatches.Remove(name))
        {
            var changed = JsonSerializer.SerializeToElement("wrong-read-back");
            return value with { Data = "wrong-read-back", RawData = changed };
        }
        return value;
    }

    public RegistryMutationAttempt WriteValue(string name, string kind, JsonElement data)
    {
        if (name is not (RegistryLanguageScope.LocaleName or RegistryLanguageScope.LocaleAudioName))
            throw new InvalidOperationException("Attempted out-of-scope Registry write.");
        WriteNames.Add(name);
        BeforeWrite?.Invoke(name);
        if (name == FailWriteName) return new(false, "Injected fake failure.");
        if (name == FailWriteOnceName && _failedOnce.Add(name)) return new(false, "Injected one-time fake failure.");
        var display = data.ValueKind == JsonValueKind.String ? data.GetString() : data.GetRawText();
        _values[name] = new(name, kind == "String" ? RegistryValueReadStatus.PresentString : RegistryValueReadStatus.UnsupportedType,
            true, display, kind, RawData: data.Clone());
        if (name == MismatchReadName) _pendingMismatches.Add(name);
        return new(true, "Fake write succeeded.");
    }

    public RegistryMutationAttempt DeleteValue(string name)
    {
        if (name is not (RegistryLanguageScope.LocaleName or RegistryLanguageScope.LocaleAudioName))
            throw new InvalidOperationException("Attempted out-of-scope Registry delete.");
        DeleteNames.Add(name);
        if (name == FailDeleteName) return new(false, "Injected fake failure.");
        _values[name] = Missing(name);
        return new(true, "Fake delete succeeded.");
    }
}

sealed class FakeMetadata(string productName) : IExecutableMetadataReader
{
    public (string ProductName, string FileVersion) Read(string path) => (productName, "1.0.0.0");
}

sealed class FailSnapshotVerificationStore(IRegistrySnapshotStore inner) : IRegistrySnapshotStore
{
    public string CreateNew(RegistrySnapshot snapshot, string gameDirectory) => inner.CreateNew(snapshot, gameDirectory);
    public RegistrySnapshotEnvelope LoadAndVerify(string path) => throw new InvalidDataException("Injected snapshot verification failure.");
    public IReadOnlyList<ValidatedRegistrySnapshot> ListValidSnapshots(RegistryGameIdentity currentGame) => inner.ListValidSnapshots(currentGame);
}

sealed class FailNthSnapshotLoadStore(IRegistrySnapshotStore inner, int failOnCall) : IRegistrySnapshotStore
{
    private int _loads;
    public string CreateNew(RegistrySnapshot snapshot, string gameDirectory) => inner.CreateNew(snapshot, gameDirectory);
    public RegistrySnapshotEnvelope LoadAndVerify(string path)
    {
        if (Interlocked.Increment(ref _loads) == failOnCall) throw new InvalidDataException("Injected snapshot verification failure.");
        return inner.LoadAndVerify(path);
    }
    public IReadOnlyList<ValidatedRegistrySnapshot> ListValidSnapshots(RegistryGameIdentity currentGame) => inner.ListValidSnapshots(currentGame);
}

sealed class FakeGameDetector(GameDetectionResult result) : IGameInstallationDetector
{
    public GameDetectionResult Detect(string? directory) => result;
}

sealed class FakeProcessLauncher : IGameProcessLauncher
{
    public bool Running { get; set; }
    public bool ThrowOnStart { get; set; }
    public int StartCalls { get; private set; }
    public string? LastExecutable { get; private set; }
    public string? LastWorkingDirectory { get; private set; }
    public bool IsGameRunning() => Running;
    public void Start(string absoluteExecutablePath, string workingDirectory)
    {
        StartCalls++;
        LastExecutable = absoluteExecutablePath;
        LastWorkingDirectory = workingDirectory;
        if (ThrowOnStart) throw new InvalidOperationException("Injected fake process start failure.");
    }
}

sealed class TempFixture : IDisposable
{
    public const string TestBuildKey = "ebd38497ab7dd9895822a094a31c0457";
    public static string ConfigPath(string gamePath) => Path.Combine(gamePath, "Data", "config", TestBuildKey[..2], TestBuildKey.Substring(2, 2), TestBuildKey);
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "ArabizeGamesTests", Guid.NewGuid().ToString("N"));
    public TempFixture() => Directory.CreateDirectory(Root);

    public string CreateGame(string version = "1.1.2.1279292", string product = "lazr", string buildName = "CRC_1279292A_retail_signed_ship")
    {
        var path = Path.Combine(Root, "game");
        Directory.CreateDirectory(Path.Combine(path, "Data", "data"));
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath(path))!);
        Directory.CreateDirectory(Path.Combine(path, "main"));
        File.WriteAllText(Path.Combine(path, "MW2CR.exe"), "test executable marker");
        File.WriteAllText(Path.Combine(path, "MW2 Campaign Remastered Launcher.exe"), "test launcher marker");
        File.WriteAllText(Path.Combine(path, ".build.info"), $"Branch!STRING:0|Active!DEC:1|Build Key!HEX:16|Version!STRING:0\neu|1|{TestBuildKey}|{version}\n");
        File.WriteAllText(ConfigPath(path), $"build-name = {buildName}\nbuild-product = {product}\n");
        File.WriteAllText(Path.Combine(path, "main", "fileSysCheck.cfg"), "// test fixture\n");
        return path;
    }

    public string CreatePackage(string supportedBuild, string destination, bool corruptHash = false)
    {
        var directory = Path.Combine(Root, "packages", "demo");
        Directory.CreateDirectory(Path.Combine(directory, "assets", "ar"));
        var payload = Path.Combine(directory, "assets", "ar", "text.bin");
        File.WriteAllText(payload, "test-only package bytes; not a translation");
        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(payload)));
        if (corruptHash) hash = new string('0', 64);
        var manifest = new PackageManifest
        {
            PackageId = "test-only-package", GameId = GameDetectionService.GameId,
            SupportedEdition = "Campaign Remastered PC", SupportedBuilds = [supportedBuild],
            PackageVersion = "0.0-test", Language = "ar", InstallationMethod = "Unverified test fixture",
            CompatibilityNotes = "Temporary test data only.", InstallationInstructions = "Not installable.",
            Files = [new PackageFileEntry { Source = "assets/ar/text.bin", Destination = destination, Sha256 = hash }]
        };
        var path = Path.Combine(directory, "package.json");
        File.WriteAllText(path, JsonSerializer.Serialize(manifest));
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
    }
}
