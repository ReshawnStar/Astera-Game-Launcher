using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using MC2Pack;

namespace Astera
{
    sealed partial class LibraryForm
    {
        internal static int AuditManagedLibrary(string root)
        {
            try
            {
                if (Directory.Exists(root))
                    throw new IOException("Use a new managed-library audit directory");
                Directory.CreateDirectory(root);
                var checks = new List<string>();
                string archive = Path.Combine(root, "release.zip");
                var package = ManagedFixture(archive, "1.0.0", "game", new Dictionary<string, byte[]> { { "game/tweak.cfg", System.Text.Encoding.UTF8.GetBytes("default") } });
                var settings = new Preferences();
                string data = Path.Combine(root, "Astera"), home = Path.Combine(root, "Carl"), documents = Path.Combine(home, "OneDrive", "Documents");
                Directory.CreateDirectory(documents);
                AuditAssert(ManagedLibrary.Root(settings, data) == Path.Combine(data, "Games"), "Default game library does not stay with local Astera data");
                string game = Path.Combine(home, "Games", "Gacha Nebula +");
                ManagedGames.Install(archive, package, game, CancellationToken.None, null);
                var candidates = ManagedLibrary.Candidates(settings, data, home, documents).ToArray();
                AuditAssert(ManagedLibrary.FindExisting(candidates, package) == game, "Existing user-profile Games installation was not detected");
                AuditAssert(ManagedLibrary.FindExisting(new[] { Path.Combine(root, "missing") }, package) == null, "Missing installation did not fall back cleanly");
                File.WriteAllText(Path.Combine(game, "game", "GachaNebula.swf"), "changed");
                AuditAssert(!ManagedLibrary.VerifiedExisting(game, package), "A changed game was adopted without matching the signed package hashes");
                File.WriteAllText(Path.Combine(game, "game", "GachaNebula.swf"), "game");
                checks.Add("Per-user default library, bounded existing-install discovery, missing-folder fallback and tampered-game rejection");
                string roaming = Path.Combine(root, "roaming"), local = Path.Combine(root, "local");
                var saves = ManagedLibrary.Saves(game, roaming, local);
                Directory.CreateDirectory(saves[0]);
                File.WriteAllText(Path.Combine(saves[0], "profile.sol"), "native save");
                Directory.CreateDirectory(Path.Combine(game, "Recovery"));
                File.WriteAllText(Path.Combine(game, "Recovery", "backup.sol"), "backup");
                File.WriteAllText(Path.Combine(game, "my-export.png"), "export");
                File.WriteAllText(Path.Combine(game, "game", "tweak.cfg"), "personal tweak");
                ManagedLibrary.Remember(settings, game, true, roaming, local);
                ManagedLibrary.Remember(settings, game, true, roaming, local);
                var persisted = Engine.Json.Deserialize<Preferences>(Engine.Json.Serialize(settings));
                AuditAssert(persisted.ManagedInstalls.Count == 1 && persisted.ManagedInstalls[0].SaveLocations.SequenceEqual(saves), "Remembered save locations duplicated or failed persistence");
                var legacy = ManagedGames.State(game);
                File.WriteAllText(Path.Combine(game, ManagedGames.Marker), Engine.Json.Serialize(new
                {
                    legacy.ProjectId,
                    legacy.Version,
                    legacy.ArchiveHash,
                    legacy.Files
                }));
                AuditAssert(ManagedGames.State(game).Installed, "Older installation records were treated as uninstalled");
                checks.Add("Native save locations and in-folder Recovery records persist without duplicates; old install records stay compatible");
                string marker = Path.Combine(game, ManagedGames.Marker), before = Engine.Hash(marker);
                bool refused = false;
                try
                {
                    ManagedLibrary.Uninstall(game, CancellationToken.None, null, () => true);
                }
                catch (IOException)
                {
                    refused = true;
                }

                AuditAssert(refused && Engine.Hash(marker) == before, "Running-game guard changed the install");
                using (var stop = new CancellationTokenSource())
                {
                    stop.Cancel();
                    refused = false;
                    try
                    {
                        ManagedLibrary.Uninstall(game, stop.Token, null, () => false);
                    }
                    catch (OperationCanceledException)
                    {
                        refused = true;
                    }

                    AuditAssert(refused && Engine.Hash(marker) == before, "Pre-commit cancellation changed the game");
                }

                string unrelated = Path.Combine(root, "unmanaged");
                Directory.CreateDirectory(unrelated);
                File.WriteAllText(Path.Combine(unrelated, "keep.txt"), "unrelated");
                refused = false;
                try
                {
                    ManagedLibrary.Uninstall(unrelated, CancellationToken.None, null, () => false);
                }
                catch (IOException)
                {
                    refused = true;
                }

                AuditAssert(refused && File.ReadAllText(Path.Combine(unrelated, "keep.txt")) == "unrelated", "Unmanaged folder files were removed");
                checks.Add("Running games, pre-commit cancellation and unmanaged folders are refused without deleting files");
                var removed = ManagedLibrary.Uninstall(game, CancellationToken.None, null, () => false);
                ManagedLibrary.Remember(settings, game, false, roaming, local);
                AuditAssert(removed.Removed == 3 && !ManagedLibrary.Installed(game) && !File.Exists(Path.Combine(game, ManagedGames.Launcher)), "Known game files were not removed");
                AuditAssert(File.ReadAllText(Path.Combine(game, "Recovery", "backup.sol")) == "backup" && File.ReadAllText(Path.Combine(game, "my-export.png")) == "export" && File.ReadAllText(Path.Combine(game, "game", "tweak.cfg")) == "personal tweak" && File.ReadAllText(Path.Combine(saves[0], "profile.sol")) == "native save", "Uninstall changed saved data or a personal file");
                AuditAssert(ManagedGames.State(game).Files.Count == 0 && !ManagedGames.State(game).Installed, "Uninstalled state was not durable");
                checks.Add("Uninstall deletes only unchanged package files; native saves, Recovery, exports and changed runtime files remain");
                string repair = Path.Combine(root, "repair", "Gacha Nebula +");
                ManagedGames.Install(archive, package, repair, CancellationToken.None, null);
                Directory.CreateDirectory(Path.Combine(repair, "Recovery"));
                File.WriteAllText(Path.Combine(repair, "Recovery", "keep.sol"), "keep");
                using (var stop = new CancellationTokenSource())
                {
                    refused = false;
                    try
                    {
                        ManagedLibrary.Uninstall(repair, stop.Token, (phase, done, total) =>
                        {
                            if (done == 1)
                                stop.Cancel();
                        }, () => false);
                    }
                    catch (OperationCanceledException)
                    {
                        refused = true;
                    }

                    AuditAssert(refused && !ManagedGames.State(repair).Installed && File.ReadAllText(Path.Combine(repair, "Recovery", "keep.sol")) == "keep", "Interrupted removal damaged saved data or still looked installed");
                }

                ManagedGames.Install(archive, package, repair, CancellationToken.None, null);
                AuditAssert(ManagedLibrary.Installed(repair) && File.ReadAllText(Path.Combine(repair, "Recovery", "keep.sol")) == "keep", "Reinstall did not repair interrupted removal with saved data intact");
                checks.Add("Interrupted uninstall becomes a repairable Install state; reinstall restores game files while retaining saves");
                string locked = Path.Combine(root, "locked", "Gacha Nebula +");
                ManagedGames.Install(archive, package, locked, CancellationToken.None, null);
                FileStream held = null;
                int calls = 0;
                try
                {
                    var result = ManagedLibrary.Uninstall(locked, CancellationToken.None, null, () =>
                    {
                        if (++calls == 2)
                            held = new FileStream(Path.Combine(locked, "game", "GachaNebula.swf"), FileMode.Open, FileAccess.Read, FileShare.None);
                        return false;
                    });
                    AuditAssert(result.BusyFiles.Count == 1 && ManagedGames.State(locked).Files.Count == 1 && !ManagedLibrary.Installed(locked), "Locked game file was not retained for retry");
                }
                finally
                {
                    if (held != null)
                        held.Dispose();
                }

                ManagedLibrary.Uninstall(locked, CancellationToken.None, null, () => false);
                AuditAssert(ManagedGames.State(locked).Files.Count == 0, "Remaining locked game file did not remove on retry");
                checks.Add("Late file locks leave a clear retryable record and never claim a partial game is playable");
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                var store = new Store(Path.Combine(root, "ui-state"));
                store.Settings = settings;
                store.Settings.AnimatedEffects = false;
                store.Settings.UseMinecraftSkin = false;
                ManagedGames.Install(archive, package, repair, CancellationToken.None, null);
                using (var form = new LibraryForm(store, new Channel(), true))
                {
                    var linked = form.RegisterManagedNebula(Path.Combine(repair, ManagedGames.Launcher));
                    string id = linked.Id;
                    store.Settings.FavoriteGames.Add("custom:" + id);
                    ManagedLibrary.Uninstall(repair, CancellationToken.None, null, () => false);
                    form.RememberManaged(repair, false);
                    form.ReloadGames(linked);
                    form.RefreshView();
                    AuditAssert(form.play.Text == "INSTALL" && form.ManagedRowStatus(linked) == "Install • saves kept", "Uninstalled game has no clear reinstall action");
                    store.Settings.CustomGames.Clear();
                    AuditAssert(form.RetainedManagedRoot() == repair, "Removed Library entry lost its remembered install/save location");
                    store.Settings.CustomGames.Add(linked);
                    ManagedGames.Install(archive, package, repair, CancellationToken.None, null);
                    form.RegisterManagedNebula(Path.Combine(repair, ManagedGames.Launcher));
                    AuditAssert(store.Settings.CustomGames.Count == 1 && store.Settings.CustomGames[0].Id == id && store.Settings.FavoriteGames.Contains("custom:" + id), "Reinstall duplicated Library identity or lost favorites");
                    form.Close();
                }

                using (var review = new ManagedInstallReview(game, dialog =>
                {
                }, dialog =>
                {
                }))
                {
                    review.StartPosition = FormStartPosition.Manual;
                    review.Location = new Point(-30000, -30000);
                    review.Show();
                    Application.DoEvents();
                    using (var image = new Bitmap(review.Width, review.Height))
                    {
                        review.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
                        image.Save(Path.Combine(root, "install-review.png"));
                    }

                    review.Close();
                }

                checks.Add("Library retains its game identity/favorites, offers Install after uninstall, remembers removed links and renders the location review without desktop capture");
                File.WriteAllText(Path.Combine(root, "result.json"), Engine.Json.Serialize(new
                {
                    Passed = true,
                    Checks = checks,
                    GameLaunched = false,
                    LiveGamesRemoved = false,
                    LiveSavesChanged = false,
                    AppSha256 = Engine.Hash(Application.ExecutablePath)
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
