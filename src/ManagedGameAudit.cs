using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using MC2Pack;

namespace Astera
{
    sealed partial class LibraryForm
    {
        static ManagedGamePackage ManagedFixture(string archive, string version, string gameText, Dictionary<string, byte[]> extras = null)
        {
            var files = new Dictionary<string, byte[]>();
            var exe = new byte[256];
            exe[0] = 0x4d;
            exe[1] = 0x5a;
            exe[60] = 64;
            exe[64] = 0x50;
            exe[65] = 0x45;
            files.Add(ManagedGames.Launcher, exe);
            files.Add("game/GachaNebula.swf", Encoding.UTF8.GetBytes(gameText));
            files.Add("game/META-INF/AIR/application.xml", Encoding.UTF8.GetBytes("<application><id>com.starsallies.nebula.stability</id></application>"));
            if (extras != null)
                foreach (var pair in extras)
                    files.Add(pair.Key, pair.Value);
            using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
                foreach (var pair in files)
                {
                    var entry = zip.CreateEntry(pair.Key);
                    using (var output = entry.Open())
                        output.Write(pair.Value, 0, pair.Value.Length);
                }

            return new ManagedGamePackage
            {
                Version = version,
                Hash = Engine.Hash(archive),
                Bytes = new FileInfo(archive).Length,
                LauncherHash = Engine.Hash(exe),
                GameHash = Engine.Hash(Encoding.UTF8.GetBytes(gameText))
            };
        }

        internal static int AuditManagedGame(string root, string realArchive = null)
        {
            try
            {
                if (Directory.Exists(root))
                    throw new IOException("Use a new managed-game audit folder");
                Directory.CreateDirectory(root);
                var checks = new List<string>();
                string first = Path.Combine(root, "first.zip"), second = Path.Combine(root, "second.zip"), install = Path.Combine(root, "game");
                var p1 = ManagedFixture(first, "1.0.0", "game one");
                var p2 = ManagedFixture(second, "1.0.1", "game two");
                string executable = ManagedGames.Install(first, p1, install, CancellationToken.None, null);
                AuditAssert(File.Exists(executable) && ManagedGames.State(install).Version == "1.0.0", "First install failed");
                Directory.CreateDirectory(Path.Combine(install, "Recovery", "Snapshots"));
                File.WriteAllText(Path.Combine(install, "Recovery", "Snapshots", "keep.sol"), "private backup");
                File.WriteAllText(Path.Combine(install, "my-export.png"), "private export");
                ManagedGames.Install(second, p2, install, CancellationToken.None, null);
                AuditAssert(File.ReadAllText(Path.Combine(install, "Recovery", "Snapshots", "keep.sol")) == "private backup" && File.ReadAllText(Path.Combine(install, "my-export.png")) == "private export" && Directory.GetDirectories(root, "game.astera-backup-*").Length == 1, "Update lost user data or rollback copy");
                checks.Add("Fresh install and update preserve Recovery, personal exports and a complete prior-install backup");
                string before = Engine.Hash(Path.Combine(install, ManagedGames.Marker));
                bool refused = false;
                try
                {
                    ManagedGames.Install(first, p1, install, CancellationToken.None, null);
                }
                catch (IOException)
                {
                    refused = true;
                }

                AuditAssert(refused && Engine.Hash(Path.Combine(install, ManagedGames.Marker)) == before, "Downgrade was applied");
                checks.Add("Older releases are refused without changing the installed game");
                refused = false;
                try
                {
                    ManagedGames.Install(second, p2, install, CancellationToken.None, null, () => true);
                }
                catch (IOException)
                {
                    refused = true;
                }

                AuditAssert(refused && Engine.Hash(Path.Combine(install, ManagedGames.Marker)) == before, "Running game was overwritten");
                using (var stop = new CancellationTokenSource())
                {
                    stop.Cancel();
                    refused = false;
                    try
                    {
                        ManagedGames.Install(second, p2, install, stop.Token, null);
                    }
                    catch (OperationCanceledException)
                    {
                        refused = true;
                    }

                    AuditAssert(refused && Engine.Hash(Path.Combine(install, ManagedGames.Marker)) == before, "Cancellation changed the installation");
                }

                checks.Add("Running-game and cancellation guards preserve the existing installation");
                string evil = Path.Combine(root, "traversal.zip");
                var bad = ManagedFixture(evil, "1.0.2", "evil", new Dictionary<string, byte[]> { { "../escaped.dll", new byte[] { 1 } } });
                refused = false;
                try
                {
                    ManagedGames.Install(evil, bad, install, CancellationToken.None, null);
                }
                catch (IOException)
                {
                    refused = true;
                }

                AuditAssert(refused && !File.Exists(Path.Combine(root, "escaped.dll")), "Archive traversal escaped staging");
                string saveArchive = Path.Combine(root, "save-in-package.zip");
                bad = ManagedFixture(saveArchive, "1.0.2", "evil", new Dictionary<string, byte[]> { { "Recovery/save.sol", new byte[] { 1 } } });
                refused = false;
                try
                {
                    ManagedGames.Install(saveArchive, bad, install, CancellationToken.None, null);
                }
                catch (IOException)
                {
                    refused = true;
                }

                AuditAssert(refused && Engine.Hash(Path.Combine(install, ManagedGames.Marker)) == before, "A package with player saves was accepted");
                AuditAssert(Directory.GetDirectories(root, ".an-*").Length == 0, "Failed staging was left behind");
                checks.Add("Archive traversal and packaged player saves are rejected; failed staging is cleaned safely");
                refused = false;
                try
                {
                    ManagedGames.Install(second, p2, install, CancellationToken.None, null, null, null, () =>
                    {
                        throw new IOException("Injected commit failure");
                    });
                }
                catch (IOException)
                {
                    refused = true;
                }

                AuditAssert(refused && Engine.Hash(Path.Combine(install, ManagedGames.Marker)) == before && File.ReadAllText(Path.Combine(install, "Recovery", "Snapshots", "keep.sol")) == "private backup", "Commit failure did not restore the old installation");
                checks.Add("A failure after moving the old installation restores it with its Recovery files intact");
                FileStream held = null;
                string failedStage = null;
                refused = false;
                try
                {
                    try
                    {
                        ManagedGames.Install(second, p2, install, CancellationToken.None, null, null, delegate
                        {
                            failedStage = Directory.GetDirectories(root, ".an-*").Single();
                            string license = Path.Combine(failedStage, "game", "Adobe AIR", "Versions", "1.0", "Resources", "Licenses", "cairo", "COPYING");
                            Directory.CreateDirectory(Path.GetDirectoryName(license));
                            File.WriteAllText(license, "fixture");
                            held = new FileStream(license, FileMode.Open, FileAccess.Read, FileShare.None);
                            throw new IOException("Original install failure retained");
                        });
                    }
                    catch (IOException error)
                    {
                        refused = error.Message == "Original install failure retained";
                    }
                }
                finally
                {
                    if (held != null)
                        held.Dispose();
                    if (failedStage != null)
                        ManagedGames.CleanStage(failedStage, root);
                }

                AuditAssert(refused && Engine.Hash(Path.Combine(install, ManagedGames.Marker)) == before && !Directory.Exists(failedStage), "Locked cleanup hid the original error or changed installation");
                checks.Add("A locked nested AIR license cannot mask the original failure; old game stays intact and deferred cleanup succeeds after unlock");
                string deep = Path.Combine(root, new string('d', 90), "Documents", new string('s', 60), "Gacha Nebula +");
                string deepExe = ManagedGames.Install(first, p1, deep, CancellationToken.None, null);
                AuditAssert(!deepExe.StartsWith(@"\\?\") && File.Exists(ManagedGames.DiskPath(deepExe)), "Internal long-path prefix leaked into the Library or install failed");
                Directory.CreateDirectory(ManagedGames.DiskPath(Path.Combine(deep, "Recovery")));
                File.WriteAllText(ManagedGames.DiskPath(Path.Combine(deep, "Recovery", "keep.sol")), "personal save");
                ManagedGames.Install(second, p2, deep, CancellationToken.None, null);
                AuditAssert(File.ReadAllText(ManagedGames.DiskPath(Path.Combine(deep, "Recovery", "keep.sol"))) == "personal save" && ManagedGames.State(deep).Version == "1.0.1", "Long-path update lost state or recovery data");
                refused = false;
                try
                {
                    ManagedGames.Install(second, p2, deep, CancellationToken.None, null, null, null, () =>
                    {
                        throw new IOException("Deep commit fault");
                    });
                }
                catch (IOException error)
                {
                    refused = error.Message == "Deep commit fault";
                }

                AuditAssert(refused && ManagedGames.State(deep).Version == "1.0.1" && File.ReadAllText(ManagedGames.DiskPath(Path.Combine(deep, "Recovery", "keep.sol"))) == "personal save", "Long-path rollback lost the installed game");
                checks.Add("Deep Documents-style folder install/update/rollback uses extended filesystem paths; saves stay intact and Library paths remain normal");
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                var state = new Store(Path.Combine(root, "state"));
                state.Settings.AnimatedEffects = false;
                state.Settings.UseMinecraftSkin = false;
                using (var form = new LibraryForm(state, new Channel(), true))
                {
                    var linked = form.RegisterManagedNebula(executable);
                    string id = linked.Id;
                    form.RegisterManagedNebula(executable);
                    AuditAssert(state.Settings.CustomGames.Count == 1 && state.Settings.CustomGames[0].Id == id, "Repeated install duplicated Library entry");
                    form.ReloadGames(linked);
                    form.RefreshView();
                    AuditAssert(form.play.Text == "UPDATE" && form.check.Enabled, "Managed game did not expose the newer bundled release");
                    AuditAssert(CustomGames.StartInfo(linked).Arguments == "--play", "Play bypassed the protected launcher");
                    form.Close();
                }

                checks.Add("Library registration is idempotent; Play uses the helper with --play and newer releases expose Update");
                using (var progress = new AppUpdateDialog())
                {
                    progress.Location = new System.Drawing.Point(-30000, -30000);
                    progress.StartPosition = FormStartPosition.Manual;
                    progress.Show();
                    PresentManagedInstall(progress, "Downloading Gacha Nebula +", 50, 100);
                    AuditAssert(progress.Stage.Text == "50%" && progress.Heading.Text.Contains("Gacha Nebula +") && progress.Meter.Fraction == .5, "Discover install progress was not visible in its own dialog");
                    progress.Finish();
                }

                checks.Add("Discover installation has its own named progress dialog and real byte percentage");
                if (realArchive != null)
                {
                    using (var zip = ZipFile.OpenRead(realArchive))
                        foreach (var entry in zip.Entries)
                        {
                            string relative = ManagedGames.Relative(entry.FullName).Replace('/', Path.DirectorySeparatorChar);
                            foreach (string destination in new[]
                            {
                                Path.Combine("C:\\Games", ".an-0123456789abcdef", relative),
                                Path.Combine("C:\\Games", "Gacha Nebula +", relative)
                            }

                            )
                                AuditAssert(destination.Length < 260 && Path.GetDirectoryName(destination).Length < 248, "Published archive exceeds legacy Windows path budget for C:\\Games");
                        }

                    checks.Add("Every actual release path fits legacy Windows file/directory limits in both shortened staging and final C:\\Games installation");
                    var release = DiscoverCatalog.Bundled().Projects.First(p => p.Id == ManagedGames.Id).Package;
                    string actual = Path.Combine(root, new string('a', 90), "Documents", new string('b', 50), "actual-release");
                    string launcher = ManagedGames.Install(realArchive, release, actual, CancellationToken.None, null);
                    AuditAssert(Engine.Hash(launcher).Equals(release.LauncherHash, StringComparison.OrdinalIgnoreCase) && Engine.Hash(Path.Combine(actual, "game", "GachaNebula.swf")).Equals(release.GameHash, StringComparison.OrdinalIgnoreCase), "Real package components differ from receipt");
                    Directory.CreateDirectory(ManagedGames.DiskPath(Path.Combine(actual, "Recovery")));
                    File.WriteAllText(ManagedGames.DiskPath(Path.Combine(actual, "Recovery", "keep.sol")), "real-package save");
                    ManagedLibrary.Uninstall(actual, CancellationToken.None, null, () => false);
                    AuditAssert(!ManagedLibrary.Installed(actual) && File.ReadAllText(ManagedGames.DiskPath(Path.Combine(actual, "Recovery", "keep.sol"))) == "real-package save", "Real release uninstall lost saves or still appeared installed");
                    ManagedGames.Install(realArchive, release, actual, CancellationToken.None, null);
                    AuditAssert(ManagedLibrary.Installed(actual) && File.ReadAllText(ManagedGames.DiskPath(Path.Combine(actual, "Recovery", "keep.sol"))) == "real-package save", "Real release reinstall failed to preserve saved data");
                    checks.Add("Actual release uninstall and reinstall work in deep paths with Recovery saves preserved");
                    checks.Add("The supplied 536 MiB release installs completely into an isolated folder with the pinned helper/game hashes and save-profile identity");
                }

                File.WriteAllText(Path.Combine(root, "result.json"), Engine.Json.Serialize(new
                {
                    Passed = true,
                    Checks = checks,
                    GameLaunched = false,
                    LiveSaveProfilesChanged = false,
                    RealArchiveTested = realArchive != null
                }));
                return 0;
            }
            catch (Exception error)
            {
                Directory.CreateDirectory(root);
                File.WriteAllText(Path.Combine(root, "failure.txt"), error.ToString());
                return 1;
            }
        }
    }
}
