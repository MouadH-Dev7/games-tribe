using System.Security.Cryptography;
using System.Text.Json;
using SilksongArabicLauncher.Core;

var tests = new (string Name, Action Run)[]
{
    ("manifest parses and validates source hashes", TestManifestAndHashes),
    ("pinned manifest digest detects manifest tampering", TestPinnedManifest),
    ("rejects path traversal", TestPathTraversal),
    ("rejects unexpected existing destinations", TestDestinationConflict),
    ("rejects unlisted package files", TestUnlistedFile),
    ("refuses install without unsigned-runtime confirmation", TestRuntimeConfirmation),
    ("refuses installation while the game process is running", TestRunningGameGuard),
    ("installs, verifies, uninstalls and restores exact original files", TestInstallAndUninstall),
    ("rolls back completed operations after a transaction failure", TestRollback),
    ("recovers an interrupted transaction from its journal", TestInterruptedRecovery),
    ("recovery clears a stale installed record after rollback", TestRecoveryClearsStaleInstalledRecord),
    ("installer refuses to start over a pending transaction", TestInstallBlockedByPendingTransaction),
    ("preserves user-modified files during uninstall", TestModifiedFiles),
    ("rejects a changed game executable", TestExecutableValidation),
    ("launch requires warning confirmation and starts the verified path", TestLaunch),
    ("detects the actual local game and package read-only", TestRealLocalDetection)
};

var passed = 0;
foreach (var (name, run) in tests)
{
    try { run(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception ex) { Console.Error.WriteLine("FAIL " + name + ": " + ex); Environment.ExitCode = 1; }
}
Console.WriteLine($"RESULT {passed}/{tests.Length} passed");

static void TestManifestAndHashes()
{
    using var f = Fixture.Create();
    var result = f.Installer.Inspect(f.ManifestPath, f.Package, f.Game);
    Assert(result.Success, string.Join("\n", result.Issues.Select(x => x.Message)));
    Assert(result.InstallFiles.Count == 2, "Expected two installable fixture files.");
    Write(Path.Combine(f.Package, "readme.txt"), "changed");
    result = f.Installer.Inspect(f.ManifestPath, f.Package, f.Game);
    Assert(result.Issues.Any(x => x.Code == "source-size-mismatch" || x.Code == "source-hash-mismatch"), "Excluded instructions file hash was not checked.");
}

static void TestPinnedManifest()
{
    using var f = Fixture.Create();
    var expected = HashFile(f.ManifestPath);
    Assert(new ManifestValidator(expected).Read(f.ManifestPath).SchemaVersion == 1, "Correct pinned manifest was rejected.");
    Write(f.ManifestPath, File.ReadAllText(f.ManifestPath) + " ");
    try { _ = new ManifestValidator(expected).Read(f.ManifestPath); throw new InvalidOperationException("Tampered manifest was accepted."); }
    catch (InvalidDataException) { }
}

static void TestPathTraversal()
{
    using var f = Fixture.Create();
    var manifest = ReadManifest(f.ManifestPath);
    manifest.Files[0].DestinationRelativePath = "../outside.bin";
    SaveManifest(f.ManifestPath, manifest);
    var result = f.Installer.Inspect(f.ManifestPath, f.Package, f.Game);
    Assert(!result.Success && result.Issues.Any(x => x.Code == "unsafe-path"), "Traversal was not rejected.");
}

static void TestDestinationConflict()
{
    using var f = Fixture.Create();
    var added = manifestAdd(f.ManifestPath);
    Write(Path.Combine(f.Game, added.DestinationRelativePath!.Replace('/', Path.DirectorySeparatorChar)), "unrelated mod data");
    var result = f.Installer.Inspect(f.ManifestPath, f.Package, f.Game);
    Assert(result.Issues.Any(x => x.Code == "unexpected-conflict"), "Unexpected file was not treated as a conflict.");
}

static void TestUnlistedFile()
{
    using var f = Fixture.Create();
    Write(Path.Combine(f.Package, "rogue.dll"), "unlisted");
    var result = f.Installer.Inspect(f.ManifestPath, f.Package, f.Game);
    Assert(result.Issues.Any(x => x.Code == "unlisted-package-file"), "Unlisted payload was not rejected.");
}

static void TestRuntimeConfirmation()
{
    using var f = Fixture.Create();
    var result = f.Installer.Install(f.ManifestPath, f.Package, f.Game, confirmUnsignedRuntime: false);
    Assert(!result.Success && result.Message.Contains("Explicit confirmation", StringComparison.Ordinal), "Runtime confirmation was not required.");
    Assert(Read(Path.Combine(f.Game, Fixture.FontRelative)) == Fixture.OriginalFont, "Game file changed before confirmation.");
}

static void TestRunningGameGuard()
{
    using var f = Fixture.Create();
    var installer = new TransactionalInstaller(f.DataRoot, new FakeProcessGuard(running: true));
    var result = installer.Install(f.ManifestPath, f.Package, f.Game, confirmUnsignedRuntime: true);
    Assert(!result.Success && result.Message.Contains("Close Hollow Knight", StringComparison.Ordinal), "Running process was not blocked.");
    Assert(Read(Path.Combine(f.Game, Fixture.FontRelative)) == Fixture.OriginalFont, "Game was modified while its process was running.");
}

static void TestInstallAndUninstall()
{
    using var f = Fixture.Create();
    var result = f.Installer.Install(f.ManifestPath, f.Package, f.Game, confirmUnsignedRuntime: true);
    Assert(result.Success, result.Message);
    Assert(Read(Path.Combine(f.Game, Fixture.FontRelative)) == Fixture.InstalledFont, "Replacement was not installed.");
    Assert(Read(Path.Combine(f.Game, Fixture.PluginRelative)) == Fixture.PluginContents, "New file was not installed.");
    Assert(f.Installer.VerifyInstalledTransaction(result.TransactionId!, f.Game).Success, "Installed transaction verification failed.");
    var uninstall = f.Installer.Uninstall(result.TransactionId!, f.Game);
    Assert(uninstall.Success, uninstall.Message);
    Assert(Read(Path.Combine(f.Game, Fixture.FontRelative)) == Fixture.OriginalFont, "Original bundle was not restored.");
    Assert(!File.Exists(Path.Combine(f.Game, Fixture.PluginRelative)), "Transaction-created file was not removed.");
    Assert(File.Exists(Path.Combine(f.DataRoot, "backups", result.TransactionId!, Fixture.FontRelative.Replace('/', Path.DirectorySeparatorChar))), "Verified backup was not preserved.");
}

static void TestRollback()
{
    using var f = Fixture.Create();
    var result = f.Installer.Install(f.ManifestPath, f.Package, f.Game, true, (count, _) =>
    {
        if (count == 1) throw new IOException("simulated commit interruption");
    });
    Assert(!result.Success, "Injected failure should fail the transaction.");
    Assert(result.ManualFiles.Count == 0, "Rollback required manual intervention.");
    Assert(Read(Path.Combine(f.Game, Fixture.FontRelative)) == Fixture.OriginalFont, "Rollback did not restore the replaced file.");
    Assert(!File.Exists(Path.Combine(f.Game, Fixture.PluginRelative)), "Rollback left a new file behind.");
}

static void TestInterruptedRecovery()
{
    using var f = Fixture.Create();
    var id = Guid.NewGuid().ToString("N");
    var backupRoot = Path.Combine(f.DataRoot, "backups", id);
    Directory.CreateDirectory(Path.Combine(backupRoot, Path.GetDirectoryName(Fixture.FontRelative)!));
    Write(Path.Combine(backupRoot, Fixture.FontRelative.Replace('/', Path.DirectorySeparatorChar)), Fixture.OriginalFont);
    Write(Path.Combine(f.Game, Fixture.FontRelative), Fixture.InstalledFont);
    Write(Path.Combine(f.Game, Fixture.PluginRelative), Fixture.PluginContents);
    var tx = f.CreateTransaction(id, "Applying");
    var journalFolder = Path.Combine(f.DataRoot, "journals");
    Directory.CreateDirectory(journalFolder);
    WriteSealed(Path.Combine(journalFolder, id + ".pending.json"), tx);
    var result = f.Installer.RecoverInterrupted(f.Game);
    Assert(result.Success, result.Message);
    Assert(Read(Path.Combine(f.Game, Fixture.FontRelative)) == Fixture.OriginalFont, "Recovery did not restore replacement.");
    Assert(!File.Exists(Path.Combine(f.Game, Fixture.PluginRelative)), "Recovery did not remove transaction-created file.");
    Assert(f.Installer.FindRecoverable(f.Game).Count == 0, "Recovered transaction is still pending.");
}

static void TestRecoveryClearsStaleInstalledRecord()
{
    using var f = Fixture.Create();
    var id = Guid.NewGuid().ToString("N");
    var backupRoot = Path.Combine(f.DataRoot, "backups", id);
    Directory.CreateDirectory(Path.Combine(backupRoot, Path.GetDirectoryName(Fixture.FontRelative)!));
    Write(Path.Combine(backupRoot, Fixture.FontRelative.Replace('/', Path.DirectorySeparatorChar)), Fixture.OriginalFont);
    Write(Path.Combine(f.Game, Fixture.FontRelative), Fixture.InstalledFont);
    Write(Path.Combine(f.Game, Fixture.PluginRelative), Fixture.PluginContents);
    var tx = f.CreateTransaction(id, "Applying");
    var journalFolder = Path.Combine(f.DataRoot, "journals");
    var transactionFolder = Path.Combine(f.DataRoot, "transactions");
    Directory.CreateDirectory(journalFolder);
    Directory.CreateDirectory(transactionFolder);
    WriteSealed(Path.Combine(journalFolder, id + ".pending.json"), tx);
    var staleFinal = f.CreateTransaction(id, "Installed");
    WriteSealed(Path.Combine(transactionFolder, id + ".json"), staleFinal);

    var result = f.Installer.RecoverInterrupted(f.Game);
    Assert(result.Success, result.Message);
    Assert(f.Installer.GetInstalledTransactions(f.Game).Count == 0, "Rollback left an Installed transaction record behind.");
}

static void TestInstallBlockedByPendingTransaction()
{
    using var f = Fixture.Create();
    var id = Guid.NewGuid().ToString("N");
    var backup = Path.Combine(f.DataRoot, "backups", id, Fixture.FontRelative.Replace('/', Path.DirectorySeparatorChar));
    Write(backup, Fixture.OriginalFont);
    var journalFolder = Path.Combine(f.DataRoot, "journals");
    Directory.CreateDirectory(journalFolder);
    WriteSealed(Path.Combine(journalFolder, id + ".pending.json"), f.CreateTransaction(id, "Staging"));
    var result = f.Installer.Install(f.ManifestPath, f.Package, f.Game, true);
    Assert(!result.Success && result.Message.Contains("interrupted transaction", StringComparison.OrdinalIgnoreCase), "Install proceeded despite a pending transaction.");
    Assert(Read(Path.Combine(f.Game, Fixture.FontRelative)) == Fixture.OriginalFont, "Pending-transaction guard modified the game fixture.");
}

static void TestModifiedFiles()
{
    using var f = Fixture.Create();
    var installed = f.Installer.Install(f.ManifestPath, f.Package, f.Game, true);
    Assert(installed.Success, installed.Message);
    Write(Path.Combine(f.Game, Fixture.PluginRelative), "modified by user after install");
    var result = f.Installer.Uninstall(installed.TransactionId!, f.Game);
    Assert(!result.Success && result.ManualFiles.Contains(Fixture.PluginRelative), "Modified file was not reported.");
    Assert(Read(Path.Combine(f.Game, Fixture.PluginRelative)) == "modified by user after install", "Modified file was deleted.");
    Assert(Read(Path.Combine(f.Game, Fixture.FontRelative)) == Fixture.OriginalFont, "Unmodified replacement was not restored.");
}

static void TestExecutableValidation()
{
    using var f = Fixture.Create();
    Write(Path.Combine(f.Game, "Hollow Knight Silksong.exe"), "changed executable");
    var result = f.Installer.Inspect(f.ManifestPath, f.Package, f.Game);
    Assert(result.Issues.Any(x => x.Code == "game-exe-changed"), "Changed executable was accepted.");
}

static void TestLaunch()
{
    using var f = Fixture.Create();
    var install = f.Installer.Install(f.ManifestPath, f.Package, f.Game, true);
    Assert(install.Success, install.Message);
    var starter = new FakeStarter();
    var launcher = new GameLaunchService(f.Installer, new FakeProcessGuard(), starter);
    var denied = launcher.Launch(f.ManifestPath, f.Game, install.TransactionId!, false);
    Assert(denied.Status == GameLaunchStatus.ConfirmationRequired && starter.Path is null, "Launch bypassed warning confirmation.");
    var result = launcher.Launch(f.ManifestPath, f.Game, install.TransactionId!, true);
    Assert(result.Status == GameLaunchStatus.Started, result.Message);
    Assert(starter.Path == Path.Combine(f.Game, "Hollow Knight Silksong.exe"), "Launcher used an unexpected executable path.");
    Assert(starter.WorkingDirectory == f.Game, "Launcher used an unexpected working directory.");
}

static void TestRealLocalDetection()
{
    var root = FindProjectRoot();
    var manifest = Path.Combine(root, "diagnostics", "phase-2-installation-manifest-proposal.json");
    var parsed = new ManifestValidator().Read(manifest);
    var package = parsed.PackageIdentity.SourceDirectory;
    var game = parsed.TargetGame.CandidateInstallationPath;
    Assert(new GameDiscoveryService().Discover(parsed, game).Any(c => c.Valid), "The real local installation was not discovered.");
    var result = new ManifestValidator().Validate(manifest, package, game);
    Assert(result.Success, "Read-only real-path detection failed: " + string.Join("; ", result.Issues.Select(x => x.Code + ":" + x.Message)));
}

static ManifestFile manifestAdd(string path) => ReadManifest(path).Files.Single(f => f.ProposedAction == "ADD");
static Phase2Manifest ReadManifest(string path) => JsonSerializer.Deserialize<Phase2Manifest>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
static void SaveManifest(string path, Phase2Manifest manifest) => File.WriteAllText(path, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
static void Write(string path, string value) { Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, value); }
static string Read(string path) => File.ReadAllText(path);
static string HashFile(string path) { using var s = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(s)); }
static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
static string FindProjectRoot()
{
    var current = new DirectoryInfo(AppContext.BaseDirectory);
    while (current is not null && !File.Exists(Path.Combine(current.FullName, "diagnostics", "phase-2-installation-manifest-proposal.json"))) current = current.Parent;
    return current?.FullName ?? throw new DirectoryNotFoundException("Could not locate project diagnostics folder.");
}
static void WriteSealed(string path, TransactionManifest tx)
{
    var options = new JsonSerializerOptions { WriteIndented = true };
    tx.IntegritySha256 = "";
    tx.IntegritySha256 = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(tx, options)));
    Write(path, JsonSerializer.Serialize(tx, options));
}

sealed class Fixture : IDisposable
{
    public const string FontRelative = "Hollow Knight Silksong_Data/StreamingAssets/aa/StandaloneWindows64/fonts_assets_english.bundle";
    public const string PluginRelative = "BepInEx/plugins/HeshamRTL/HeshamRTL.Runtime.dll";
    public const string OriginalFont = "original-font-bundle";
    public const string InstalledFont = "replacement-font-bundle";
    public const string PluginContents = "synthetic-runtime-plugin";
    public string Root { get; }
    public string Package { get; }
    public string Game { get; }
    public string DataRoot { get; }
    public string ManifestPath { get; }
    public TransactionalInstaller Installer { get; }

    private Fixture(string root)
    {
        Root = root;
        Package = Path.Combine(root, "package");
        Game = Path.Combine(root, "game");
        DataRoot = Path.Combine(root, "launcher-data");
        ManifestPath = Path.Combine(root, "manifest.json");
        Directory.CreateDirectory(Package);
        Directory.CreateDirectory(Game);
        Write(Path.Combine(Game, "Hollow Knight Silksong.exe"), "synthetic-silksong-executable");
        Write(Path.Combine(Game, "UnityPlayer.dll"), "synthetic-unity-player");
        Write(Path.Combine(Game, "Hollow Knight Silksong_Data/globalgamemanagers"), "synthetic-global-data");
        Write(Path.Combine(Game, "Hollow Knight Silksong_Data/Managed/Assembly-CSharp.dll"), "synthetic-managed-game");
        Write(Path.Combine(Game, FontRelative), OriginalFont);
        Write(Path.Combine(Package, FontRelative), InstalledFont);
        Write(Path.Combine(Package, PluginRelative), PluginContents);
        Write(Path.Combine(Package, "readme.txt"), "instructions not installed");

        var manifest = new Phase2Manifest
        {
            SchemaVersion = 1,
            PackageIdentity = new PackageIdentity { ClaimedName = "Test package", SourceDirectory = Package, InstructionsRelativePath = "readme.txt" },
            TargetGame = new TargetGame
            {
                Title = "Hollow Knight: Silksong (PC)", SteamAppId = 1030300, CandidateInstallationPath = Game,
                Executable = new ExecutableIdentity
                {
                    RelativePath = "Hollow Knight Silksong.exe",
                    SizeBytes = new FileInfo(Path.Combine(Game, "Hollow Knight Silksong.exe")).Length,
                    Sha256 = Hash(Path.Combine(Game, "Hollow Knight Silksong.exe"))
                }
            },
            Files =
            [
                CreateFileEntry(FontRelative, FontRelative, "REPLACE", true, Hash(Path.Combine(Game, FontRelative))),
                CreateFileEntry(PluginRelative, PluginRelative, "ADD", false, null),
                new ManifestFile { SourceRelativePath = "readme.txt", IncludedPerReadme = false, SizeBytes = new FileInfo(Path.Combine(Package, "readme.txt")).Length, Sha256 = Hash(Path.Combine(Package, "readme.txt")), ProposedAction = "EXCLUDE_INSTRUCTIONS" }
            ]
        };
        SaveManifest(ManifestPath, manifest);
        Installer = new TransactionalInstaller(DataRoot, new FakeProcessGuard());
    }

    public static Fixture Create() => new(Path.Combine(Path.GetTempPath(), "SilksongArabicLauncher.Tests", Guid.NewGuid().ToString("N")));

    public TransactionManifest CreateTransaction(string id, string state)
    {
        var old = File.ReadAllText(Path.Combine(Game, FontRelative));
        var installed = File.ReadAllText(Path.Combine(Package, FontRelative));
        var plugin = File.ReadAllText(Path.Combine(Package, PluginRelative));
        return new TransactionManifest
        {
            TransactionId = id, GameRoot = Game,
            GameExecutableSha256 = Hash(Path.Combine(Game, "Hollow Knight Silksong.exe")),
            State = state, CreatedAtUtc = DateTimeOffset.UtcNow,
            CreatedDirectories = ["BepInEx", "BepInEx/plugins", "BepInEx/plugins/HeshamRTL"],
            Operations =
            [
                new FileOperation { SourceRelativePath = FontRelative, DestinationRelativePath = FontRelative, SizeBytes = new FileInfo(Path.Combine(Package, FontRelative)).Length, InstalledSha256 = Hash(Path.Combine(Package, FontRelative)), OriginalExisted = true, OriginalSha256 = Hash(Path.Combine(DataRoot, "backups", id, FontRelative)), OriginalSizeBytes = new FileInfo(Path.Combine(DataRoot, "backups", id, FontRelative)).Length, BackupRelativePath = FontRelative, TemporaryRelativePath = FontRelative + "." + id + ".slar-tmp" },
                new FileOperation { SourceRelativePath = PluginRelative, DestinationRelativePath = PluginRelative, SizeBytes = new FileInfo(Path.Combine(Package, PluginRelative)).Length, InstalledSha256 = Hash(Path.Combine(Package, PluginRelative)), OriginalExisted = false, TemporaryRelativePath = PluginRelative + "." + id + ".slar-tmp" }
            ]
        };
    }

    private ManifestFile CreateFileEntry(string src, string dest, string action, bool exists, string? oldHash) => new()
    {
        SourceRelativePath = src, DestinationRelativePath = dest, IncludedPerReadme = true,
        SizeBytes = new FileInfo(Path.Combine(Package, src.Replace('/', Path.DirectorySeparatorChar))).Length,
        Sha256 = Hash(Path.Combine(Package, src.Replace('/', Path.DirectorySeparatorChar))),
        ProposedAction = action, DestinationCurrentlyExists = exists, ExistingDestinationSha256 = oldHash,
        ExistingDestinationSizeBytes = exists ? new FileInfo(Path.Combine(Game, dest.Replace('/', Path.DirectorySeparatorChar))).Length : null
    };
    private static string Hash(string path) { using var s = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(s)); }
    private static void Write(string path, string value) { Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, value); }
    private static void SaveManifest(string path, Phase2Manifest manifest) => File.WriteAllText(path, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
    public void Dispose() { try { Directory.Delete(Root, recursive: true); } catch { } }
}

sealed class FakeProcessGuard(bool running = false) : IGameProcessGuard { public bool IsRunning(string executablePath) => running; }
sealed class FakeStarter : IGameStarter
{
    public string? Path { get; private set; }
    public string? WorkingDirectory { get; private set; }
    public void Start(string executablePath, string workingDirectory) { Path = executablePath; WorkingDirectory = workingDirectory; }
}
