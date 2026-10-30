using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using MC2Pack;

namespace Astera
{
    sealed partial class LibraryForm
    {
        static void AuditAssert(bool value, string message)
        {
            if (!value)
                throw new Exception(message);
        }

        static void AuditButtons(Control parent)
        {
            foreach (Control child in parent.Controls)
            {
                if (child is AsteraButton && child.Visible)
                    AuditAssert(parent.ClientRectangle.Contains(child.Bounds), "Button clipped in " + parent.GetType().Name + ": " + child.Text + " " + child.Bounds + " / " + parent.ClientRectangle);
                AuditButtons(child);
            }
        }

        static void Snapshot(Form form, string path)
        {
            form.PerformLayout();
            Application.DoEvents();
            using (var bitmap = new Bitmap(form.Width, form.Height))
            {
                form.DrawToBitmap(bitmap, new Rectangle(0, 0, form.Width, form.Height));
                bitmap.Save(path, ImageFormat.Png);
            }
        }

        public static int AuditLayout(string directory)
        {
            var checks = new List<string>();
            try
            {
                directory = Path.GetFullPath(directory);
                if (Directory.Exists(directory))
                    throw new Exception("Use a new isolated layout audit directory.");
                Directory.CreateDirectory(directory);
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                var store = new Store(Path.Combine(directory, "preferences"));
                store.Settings.CheckOnStart = false;
                store.Settings.KeepInTray = false;
                store.Settings.AnimatedEffects = false;
                string minecraftFixture = Path.Combine(directory, "Minecraft Squared");
                Directory.CreateDirectory(minecraftFixture);
                File.WriteAllText(Path.Combine(minecraftFixture, "minecraftinstance.json"), "{\"name\":\"Minecraft Squared\",\"baseModLoader\":{\"minecraftVersion\":\"26.3\",\"name\":\"fabric-0.19.5-26.3\",\"forgeVersion\":\"0.19.5\"}}");
                store.Settings.Profile = minecraftFixture;
                using (var form = new LibraryForm(store, new Channel(), true))
                {
                    form.ShowInTaskbar = false;
                    form.StartPosition = FormStartPosition.Manual;
                    form.Location = new Point(-30000, -30000);
                    form.Show();
                    var sorted = form.games.Items.Cast<LibraryEntry>().Select(r => r.ToString()).ToArray();
                    AuditAssert(sorted.SequenceEqual(sorted.OrderBy(n => n, StringComparer.OrdinalIgnoreCase)), "Built-in library is not A–Z");
                    AuditAssert(form.GameKind == 0 && form.games.SelectedIndex == 1, "Sorted index changed startup game identity");
                    string customExe = Path.Combine(directory, "A test & game.exe");
                    File.Copy(Application.ExecutablePath, customExe);
                    var custom = new CustomGame();
                    CustomGames.Set(store.Settings.CustomGames, custom, "A test game", customExe);
                    store.Settings.CustomGames.Add(custom);
                    store.Save();
                    form.ReloadGames(custom);
                    Application.DoEvents();
                    AuditAssert(form.heroTitle.Text == custom.Name && form.play.Text == "PLAY" && !form.check.Enabled && !form.install.Enabled, "Custom game inherited managed updater actions");
                    AuditAssert(form.games.SelectedIndex == 0 && form.IsCustomGame, "Custom game did not join A–Z list");
                    AuditButtons(form);
                    var launch = CustomGames.StartInfo(custom);
                    AuditAssert(launch.FileName == customExe && launch.WorkingDirectory == directory && launch.Arguments == "" && launch.UseShellExecute, "Custom launch path with spaces/metacharacters or working directory changed");
                    Snapshot(form, Path.Combine(directory, "custom-game.png"));
                    bool duplicateRejected = false;
                    try
                    {
                        CustomGames.Set(store.Settings.CustomGames, new CustomGame(), "Duplicate", customExe);
                    }
                    catch (IOException)
                    {
                        duplicateRejected = true;
                    }

                    AuditAssert(duplicateRejected, "Duplicate EXE was accepted");
                    var reloadedCustom = new Store(store.Root);
                    AuditAssert(reloadedCustom.Settings.CustomGames.Count == 1 && reloadedCustom.Settings.CustomGames[0].Executable == customExe, "Custom game did not persist");
                    CustomGames.Set(store.Settings.CustomGames, custom, "Zulu game", customExe);
                    form.ReloadGames(custom);
                    AuditAssert(form.games.SelectedIndex == form.games.Items.Count - 1 && form.CurrentCustom == custom, "Rename did not sort while preserving identity");
                    store.Settings.CustomGames.Remove(custom);
                    store.Save();
                    form.ReloadGames(null);
                    form.GameKind = 0;
                    AuditAssert(File.Exists(customExe) && new Store(store.Root).Settings.CustomGames.Count == 0, "Library removal affected game files or failed to persist");
                    File.Delete(customExe);
                    checks.Add("Built-in and custom library entries sort A–Z without changing game identity");
                    checks.Add("Custom EXEs persist, reject duplicates, use their own working directory and never inherit game updaters");
                    checks.Add("Renaming re-sorts the library; removing an entry preserves its game files");
                    string library = Path.Combine(directory, "Friend's separate game drive"), steam = Path.Combine(directory, "Another Steam install"), gameRoot = Path.Combine(library, "steamapps", "common", "Example Game"), nested = Path.Combine(gameRoot, "bin", "Win64", "Example.exe"), cache = Path.Combine(steam, "appcache", "librarycache", "42");
                    Directory.CreateDirectory(Path.GetDirectoryName(nested));
                    Directory.CreateDirectory(cache);
                    File.WriteAllText(nested, "Path fixture only");
                    File.WriteAllText(Path.Combine(library, "steamapps", "appmanifest_42.acf"), "\"AppState\" { \"appid\" \"42\" \"name\" \"Example Game\" \"installdir\" \"Example Game\" }");
                    string steamHero = Path.Combine(cache, "library_hero.png"), steamIcon = Path.Combine(cache, new string('a', 40) + ".png"), ownHero = Path.Combine(directory, "my-art.png");
                    using (var art = new Bitmap(400, 100))
                    {
                        using (var g = Graphics.FromImage(art))
                            g.Clear(Color.Red);
                        art.Save(steamHero);
                        art.Save(Path.Combine(cache, "library_600x900.png"));
                        using (var square = new Bitmap(32, 32))
                        {
                            using (var g = Graphics.FromImage(square))
                                g.Clear(Color.Gold);
                            square.Save(steamIcon);
                        }

                        using (var g = Graphics.FromImage(art))
                            g.Clear(Color.Blue);
                        art.Save(ownHero);
                    }

                    var detected = SteamArtwork.Find(nested, new[] { steam });
                    AuditAssert(detected != null && detected.AppId == "42" && detected.Hero == steamHero && detected.Icon == steamIcon, "Steam art did not follow a nested EXE from a separate library to the primary cache");
                    AuditAssert(SteamArtwork.Find(Path.Combine(gameRoot + " Clone", "Example.exe"), new[] { steam }) == null, "Unrelated game matched another Steam manifest");
                    var steamCustom = new CustomGame
                    {
                        Name = "Example Game",
                        Executable = nested
                    };
                    store.Settings.CustomGames.Add(steamCustom);
                    form.ApplySteamArtwork(nested, detected);
                    form.ReloadGames(steamCustom);
                    AuditAssert(form.artLabel.Text == "Steam library artwork" && form.AutomaticSteamIcon(steamCustom) != null, "Existing custom Steam game did not show automatic artwork");
                    steamCustom.Background = ownHero;
                    form.UpdateCustomLayout();
                    AuditAssert(form.artLabel.Text == "my-art.png" && ((Bitmap)form.artPreview.Image).GetPixel(0, 0).B > 200, "Steam art replaced custom artwork");
                    steamCustom.Background = "";
                    form.UpdateCustomLayout();
                    AuditAssert(form.artLabel.Text == "Steam library artwork", "Reset did not restore automatic Steam art");
                    var absent = SteamArtwork.Find(nested, new[] { Path.Combine(directory, "Steam without cache") });
                    AuditAssert(absent != null && absent.Hero == null && absent.Icon == null, "Missing Steam cache did not fall back safely");
                    File.Move(steamHero, Path.Combine(steam, "appcache", "librarycache", "42_library_hero.png"));
                    AuditAssert(SteamArtwork.Find(nested, new[] { steam }).Hero.EndsWith("42_library_hero.png"), "Legacy flat Steam cache was not supported");
                    detected.UpdatePending = true;
                    form.ApplySteamArtwork(nested, detected);
                    AuditAssert(form.play.Text == "UPDATE" && form.check.Text == "OPEN IN STEAM" && form.check.Enabled && !form.install.Enabled, "Steam update controls inherited another updater or hid the handoff");
                    AuditButtons(form);
                    store.Settings.CustomGames.Remove(steamCustom);
                    form.ReloadGames(null);
                    form.GameKind = 0;
                    AuditAssert(SteamArtwork.Pending("\"StateFlags\" \"6\"") && !SteamArtwork.Pending("\"StateFlags\" \"4\"") && SteamArtwork.Pending("\"TargetBuildID\" \"123\" \"buildid\" \"122\""), "Steam pending-update state was misread");
                    var launchSteam = SteamArtwork.Launch("42");
                    AuditAssert(launchSteam.FileName == "steam://run/42" && launchSteam.UseShellExecute && launchSteam.Arguments == "", "Steam launch handoff changed");
                    bool badSteam = false;
                    try
                    {
                        SteamArtwork.Launch("42/extra");
                    }
                    catch (IOException)
                    {
                        badSteam = true;
                    }

                    AuditAssert(badSteam, "Steam app ID allowed command injection");
                    AuditAssert(GameArtwork.MinecraftIcon != null, "Supplied Minecraft icon missing");
                    checks.Add("Steam update state and numeric-only launch handoff are correct; Minecraft icon is embedded");
                    checks.Add("Steam artwork follows actual nested EXEs in secondary libraries to separate Steam caches without hardcoded user paths");
                    checks.Add("Custom artwork overrides Steam art; reset restores it and missing/legacy caches fall back safely");
                    string alphaIcon = Path.Combine(directory, "transparent-icon.png");
                    using (var image = new Bitmap(40, 80))
                    {
                        using (var graphics = Graphics.FromImage(image))
                        {
                            graphics.Clear(Color.Transparent);
                            graphics.FillRectangle(Brushes.Magenta, 10, 10, 20, 60);
                        }

                        image.Save(alphaIcon, ImageFormat.Png);
                    }

                    using (var prepared = ReadCustomIcon(alphaIcon))
                    {
                        AuditAssert(prepared.GetPixel(0, 0).A == 0 && prepared.GetPixel(64, 64).A > 200, "Custom icon alpha or aspect-fit padding was lost");
                    }

                    AuditAssert(form.heroSubtitle.Text.StartsWith("Explore a reimagined Minecraft world") && form.heroTagline.Text == "You're not alone this time." && form.heroTagline.Font.Bold, "Minecraft description or bold closing line changed");
                    var footerLabel = form.appUpdateIcon.Parent.Controls.OfType<Label>().Single();
                    AuditAssert(footerLabel.Text.StartsWith("by Star") && !footerLabel.Text.Contains("ASTERA") && footerLabel.Top >= 24, "Footer repeated the brand or remained against Add game");
                    checks.Add("Transparent custom icons retain alpha and padding; Minecraft copy and lowered single-line footer match user direction");
                    var installedSteam = PlatformLibraries.Steam(new[] { library });
                    AuditAssert(installedSteam.Count == 1 && installedSteam[0].Id == "42" && installedSteam[0].Folder == gameRoot, "Steam installed picker missed the fixture library or chose an EXE");
                    var folderGame = new CustomGame
                    {
                        Name = "Example Game",
                        Platform = "steam",
                        PlatformId = "42",
                        Executable = gameRoot
                    };
                    AuditAssert(PlatformLibraries.Available(folderGame) && PlatformLibraries.Start(folderGame).FileName == "steam://run/42" && PlatformLibraries.Folder(folderGame) == gameRoot, "Steam folder import did not preserve platform launch identity");
                    string itchRoot = Path.Combine(directory, "Friend itch apps"), itchGame = Path.Combine(itchRoot, "game-with-no-guessed-exe"), receipt = Path.Combine(itchGame, ".itch", "receipt.json.gz");
                    Directory.CreateDirectory(Path.GetDirectoryName(receipt));
                    using (var output = File.Create(receipt))
                    using (var gzip = new System.IO.Compression.GZipStream(output, System.IO.Compression.CompressionMode.Compress))
                    {
                        byte[] bytes = System.Text.Encoding.UTF8.GetBytes("{\"game\":{\"id\":123,\"title\":\"Itch example\"}}");
                        gzip.Write(bytes, 0, bytes.Length);
                    }

                    var installedItch = PlatformLibraries.Itch(itchRoot);
                    AuditAssert(installedItch.Count == 1 && installedItch[0].Name == "Itch example" && PlatformLibraries.Start(new CustomGame { Platform = "itch", PlatformId = "123", Executable = itchGame }).FileName == "itch://games/123", "Itch receipt import guessed an EXE or lost its existing game-page fallback");
                    byte[] newsBytes = System.Text.Encoding.UTF8.GetBytes("{\"appnews\":{\"appid\":42,\"newsitems\":[{\"title\":\"Patch one\",\"date\":1234567890,\"feedname\":\"steam_community_announcements\",\"contents\":\"[b]Fixed[/b] &amp; improved [img]hidden-image[/img]\"},{\"title\":\"External article\",\"date\":1234567891,\"feedname\":\"external\",\"contents\":\"Not developer notes\"}]}}");
                    string news = SteamNews.Text("42", newsBytes);
                    AuditAssert(news.Contains("Patch one") && news.Contains("Fixed & improved") && !news.Contains("hidden-image") && !news.Contains("External article") && !news.Contains("[b]"), "Steam posts leaked BBCode, image URLs or external articles");
                    bool wrongNews = false;
                    try
                    {
                        SteamNews.Text("43", newsBytes);
                    }
                    catch (IOException)
                    {
                        wrongNews = true;
                    }

                    AuditAssert(wrongNews, "News for a different Steam game was accepted");
                    checks.Add("Steam and itch installed-library imports preserve platform IDs and launch folders without guessing EXEs");
                    checks.Add("Official Steam posts strip markup, retain dates and text, and reject another game's feed");
                    AuditAssert(RunningGames.Check(new CustomGame { Executable = Application.ExecutablePath }, null) && !RunningGames.Check(new CustomGame { Executable = Path.Combine(directory, "not-running", "Astera.exe") }, null), "Running-state check confused another EXE with the same filename");
                    var runningFixture = new CustomGame
                    {
                        Name = "Running fixture",
                        Executable = Application.ExecutablePath
                    };
                    store.Settings.CustomGames.Add(runningFixture);
                    form.ReloadGames(runningFixture);
                    form.runningGames[runningFixture.Id] = true;
                    form.RefreshCustomView();
                    AuditAssert(form.play.Text == "RUNNING" && !form.play.Enabled, "Running game still allowed a duplicate launch");
                    form.runningGames[runningFixture.Id] = false;
                    form.startingGames[runningFixture.Id] = DateTime.UtcNow.AddSeconds(10);
                    form.RefreshCustomView();
                    AuditAssert(form.play.Text == "STARTING…" && !form.play.Enabled, "Double-click during startup was not guarded");
                    store.Settings.CustomGames.Remove(runningFixture);
                    form.ReloadGames(null);
                    form.GameKind = 0;
                    checks.Add("Running games match their real EXE paths; running and starting states prevent duplicate launches");
                    string armPose = Path.Combine(directory, "arm-colors.png");
                    using (var skin = new Bitmap(64, 64))
                    {
                        using (var graphics = Graphics.FromImage(skin))
                        {
                            graphics.Clear(Color.Transparent);
                            graphics.FillRectangle(Brushes.Red, 40, 16, 16, 16);
                            graphics.FillRectangle(Brushes.Blue, 32, 48, 16, 16);
                        }

                        skin.Save(armPose + ".skin.png");
                    }

                    File.WriteAllText(armPose + ".model", "classic");
                    using (var rig = new SkinRig(armPose))
                    using (var output = new Bitmap(300, 350))
                    using (var graphics = Graphics.FromImage(output))
                    {
                        graphics.Clear(Color.Transparent);
                        rig.Draw(graphics, new Rectangle(0, 0, 300, 350), new Point(150, 150), 4);
                        int leftRed = 0, rightRed = 0, leftBlue = 0, rightBlue = 0;
                        for (int y = 0; y < 350; y++)
                            for (int x = 0; x < 300; x++)
                            {
                                Color pixel = output.GetPixel(x, y);
                                if (pixel.A == 0)
                                    continue;
                                if (pixel.R > 120 && pixel.B < 80)
                                {
                                    if (x < 138)
                                        leftRed++;
                                    else
                                        rightRed++;
                                }

                                if (pixel.B > 120 && pixel.R < 80)
                                {
                                    if (x < 138)
                                        leftBlue++;
                                    else
                                        rightBlue++;
                                }
                            }

                        AuditAssert(leftRed > 100 && rightBlue > 100 && leftRed > leftBlue * 4 && rightBlue > rightRed * 4, "Minecraft anatomical arm textures were assigned to the opposite screen sides");
                        output.Save(Path.Combine(directory, "arm-orientation.png"));
                    }

                    checks.Add("Minecraft right and left arm textures render on their anatomical sides; existing head and depth checks remain intact");
                    AuditAssert(ButtonAtlas.Accents.Length == 16 && ButtonAtlas.Accents.Take(4).SequenceEqual(new[] { "Violet", "Teal", "Rose", "Gold" }), "Legacy colors were removed or palette options missing");
                    ButtonAtlas.SetAccent("Ice White");
                    AuditAssert(ButtonAtlas.AccentColor.ToArgb() == ColorTranslator.FromHtml("#DCE9FF").ToArgb() && ButtonAtlas.AccentTextColor.R < 40, "Light theme did not preserve its exact color and readable text");
                    ButtonAtlas.SetAccent("Deep Indigo");
                    AuditAssert(ButtonAtlas.AccentColor.ToArgb() == ColorTranslator.FromHtml("#6265CB").ToArgb(), "Requested indigo color changed");
                    ButtonAtlas.SetAccent("Violet");
                    AuditAssert(AppChangelog.Text().Contains("Astera " + SelfUpdate.CurrentVersion) && AppChangelog.Text().Length > 80, "Embedded in-app changelog is missing");
                    checks.Add("Sixteen palette choices retain the original four; light accents use readable captions and the app changelog is embedded");
                    form.Log("Repeated fixture status");
                    form.Log("Repeated fixture status");
                    AuditAssert(form.log.Text.Split(new[] { "Repeated fixture status" }, StringSplitOptions.None).Length == 2, "Activity repeats were not collapsed");
                    form.busy = true;
                    form.cancellation = new System.Threading.CancellationTokenSource();
                    form.RefreshView();
                    AuditAssert(form.cancel.Visible && !form.download.Visible, "Cancel did not occupy the download slot");
                    form.transaction = true;
                    form.RefreshView();
                    AuditAssert(!form.cancel.Visible, "Non-cancellable installation offered cancellation");
                    form.transaction = false;
                    form.busy = false;
                    form.cancellation.Dispose();
                    form.cancellation = null;
                    form.RefreshView();
                    UiEffects.Enabled = false;
                    form.GameKind = 1;
                    AuditAssert(form.gameTransition == null, "Animations-off still created a transition");
                    UiEffects.Enabled = true;
                    form.GameKind = 0;
                    checks.Add("Repeated activity collapses, Cancel replaces Download safely and the animation switch stops game transitions");
                    UiEffects.Enabled = false;
                    form.CancelGameTransition();
                    form.RefreshView();
                    form.busy = true;
                    form.SetWorkProgress("Downloading", 50L * 1048576, 100L * 1048576);
                    AuditAssert(form.workPanel.Visible && form.workMeter.Fraction == 0.5 && form.workLabel.Text.Contains("50%") && form.workLabel.Text.Contains("50.0 / 100.0 MiB"), "Download progress did not expose its percentage and bytes");
                    form.SetWorkProgress("Installing", 1, 2);
                    AuditAssert(form.workLabel.Text.Contains("1 / 2 files"), "Install progress mislabeled its units");
                    form.busy = false;
                    form.UpdateWorkProgress();
                    AuditAssert(!form.workPanel.Visible, "Progress did not hide after operation");
                    checks.Add("Download percentage/size and install file progress are visible only during work");
                    var oldProfile = new Preferences
                    {
                        Profile = "C:/old",
                        FeedUrl = "https://example.com/feed"
                    };
                    var newProfile = new Preferences
                    {
                        Profile = "C:/new",
                        FeedUrl = oldProfile.FeedUrl
                    };
                    AuditAssert(NeedsProfileCheck(oldProfile, newProfile) && !NeedsProfileCheck(oldProfile, oldProfile) && !NeedsProfileCheck(oldProfile, new Preferences()), "Profile changes did not request a new check or an unchanged/empty profile did");
                    checks.Add("Saving a different linked profile requests an automatic update check; unchanged and empty profiles do not");
                    string ftlLogs = Path.Combine(directory, "ftl-diagnostic-fixture"), diagnosticRoot = Path.Combine(directory, "ftl-diagnostics");
                    Directory.CreateDirectory(ftlLogs);
                    string ftlLog = Path.Combine(ftlLogs, "FTL_HS.log"), stderr = Path.Combine(ftlLogs, "stderr.txt");
                    File.WriteAllText(ftlLog, "Hyperspace fixture log");
                    File.WriteAllText(stderr, "Launch failure fixture");
                    string ftlOriginalHash = Engine.Hash(ftlLog);
                    AuditAssert(GameDiagnostics.Collect(diagnosticRoot, "ftl", ftlLogs, "", false) == 1 && GameDiagnostics.Collect(diagnosticRoot, "ftl", ftlLogs, "", false) == 0 && GameDiagnostics.Collect(diagnosticRoot, "ftl", ftlLogs, "", true) == 1 && Engine.Hash(ftlLog) == ftlOriginalHash, "FTL diagnostic collection missed runtime details, duplicated reports or altered originals");
                    checks.Add("FTL reports retain Hyperspace and failure output, deduplicate copies and preserve source logs");
                    var edgePoint = form.PointToScreen(new Point(1, 1));
                    long hit = (long)(ushort)edgePoint.X | ((long)(ushort)edgePoint.Y << 16);
                    AuditAssert(form.FormBorderStyle == FormBorderStyle.None && SendMessage(form.Handle, 0x84, IntPtr.Zero, new IntPtr(hit)).ToInt32() == 13, "Borderless corner resizing did not expose its native hit target");
                    checks.Add("Borderless frame exposes native corner-resize hit testing");
                    foreach (var size in new[]
                    {
                        new Size(1400, 900),
                        new Size(1100, 760)
                    }

                    )
                    {
                        form.Size = size;
                        form.PerformLayout();
                        Application.DoEvents();
                        AuditButtons(form);
                        AuditAssert(!form.banner.Controls.OfType<TableLayoutPanel>().First().Bounds.IntersectsWith(form.banner.Controls.OfType<FlowLayoutPanel>().First().Bounds), "Hero text panel overlaps and natively clips the action buttons");
                        Snapshot(form, Path.Combine(directory, "overview-" + size.Width + ".png"));
                    }

                    checks.Add("Overview buttons fit default and minimum window widths");
                    checks.Add("Hero text never overlaps the native action-button windows");
                    form.Size = new Size(1400, 900);
                    form.SelectLibraryTab(1);
                    Application.DoEvents();
                    AuditAssert(form.activityCard.Visible && !form.overviewRight.Visible, "Updates tab did not show activity");
                    AuditButtons(form);
                    Snapshot(form, Path.Combine(directory, "updates.png"));
                    checks.Add("Updates tab shows live update activity");
                    for (int cycle = 0; cycle < 12; cycle++)
                    {
                        form.moreMenu.Show(form, new Point(10, 10));
                        form.moreMenu.Close();
                        AuditAssert(!form.moreMenu.IsDisposed, "Overflow menu was disposed on close");
                    }

                    form.moreMenu.Items[2].PerformClick();
                    AuditAssert(form.selectedTab == 1, "Overflow action stopped working after reopen");
                    checks.Add("Overflow menu survives twelve close/reopen cycles and action selection");
                    form.SelectLibraryTab(2);
                    Application.DoEvents();
                    AuditAssert(form.backgroundPage.Visible && !form.overviewPage.Visible, "Artwork tab did not switch");
                    AuditButtons(form);
                    Snapshot(form, Path.Combine(directory, "backgrounds.png"));
                    checks.Add("Background management tab is reachable");
                    form.SelectLibraryTab(3);
                    Application.DoEvents();
                    AuditAssert(form.preferencesPage.Visible, "Settings tab did not switch");
                    AuditButtons(form);
                    checks.Add("Settings page exposes the real settings dialog");
                    form.GameKind = 1;
                    AuditAssert(!form.check.Enabled && !form.install.Enabled && form.heroTitle.Text == "Star Dash", "StarDash inherited Minecraft actions");
                    checks.Add("StarDash keeps unconfigured update actions disabled");
                    form.GameKind = 0;
                    form.updateReady = true;
                    form.RefreshView();
                    AuditAssert(form.play.Text == "UPDATE", "Primary game action did not advertise an available update");
                    form.updateReady = false;
                    form.RefreshView();
                    form.GameKind = 1;
                    AuditAssert(!form.notes.Text.Contains("Godot") && !form.notes.Text.Contains(".pck") && form.notes.Text.Contains("No patch notes available yet"), "Technical setup text leaked into StarDash patch notes");
                    checks.Add("Primary update label and player-facing patch-note separation are correct");
                    form.GameKind = 2;
                    AuditAssert(form.heroTitle.Text == "FTL: Multiverse" && form.banner.Game == "FTL: Multiverse" && !form.banner.LiveSkinActive && form.check.Enabled && !form.install.Enabled, "FTL manager inherited another game state");
                    using (var art = form.banner.CopyDefaultArtwork())
                        AuditAssert(art != null && art.Width > art.Height * 2, "FTL default artwork is missing or not its wide banner");
                    checks.Add("FTL manager keeps setup/update state, artwork and skin separate from Minecraft and StarDash");
                    form.GameKind = 0;
                    form.SelectLibraryTab(0);
                    string background = Path.Combine(directory, "test-background.png"), character = Path.Combine(directory, "test-character.png");
                    using (var image = new Bitmap(640, 360))
                    {
                        using (var g = Graphics.FromImage(image))
                            g.Clear(Color.DarkBlue);
                        image.Save(background, ImageFormat.Png);
                    }

                    using (var image = new Bitmap(180, 480))
                    {
                        using (var g = Graphics.FromImage(image))
                        {
                            g.Clear(Color.Transparent);
                            g.FillRectangle(Brushes.Red, 0, 0, 180, 100);
                            g.FillRectangle(Brushes.Cyan, 30, 100, 120, 380);
                        }

                        image.Save(character, ImageFormat.Png);
                    }

                    store.Settings.BackgroundMinecraft = background;
                    store.Settings.CharacterMinecraft = character;
                    store.Save();
                    form.UpdateLayoutView();
                    using (var image = new Bitmap(form.banner.Width, form.banner.Height))
                    {
                        form.banner.DrawToBitmap(image, form.banner.ClientRectangle);
                        AuditAssert(image.GetPixel(form.banner.Width - 65, 10).R > 220, "Character did not extend above the background");
                    }

                    checks.Add("Independent transparent character layer extends above scene");
                    Snapshot(form, Path.Combine(directory, "layer-fixture.png"));
                    var reloaded = new Store(store.Root);
                    AuditAssert(reloaded.Settings.CharacterMinecraft == character && reloaded.Settings.BackgroundMinecraft == background && String.IsNullOrEmpty(reloaded.Settings.CharacterStarDash), "Per-game artwork preferences did not persist");
                    checks.Add("Per-game artwork choices survive reload independently");
                    form.GameKind = 1;
                    AuditAssert(form.artLabel.Text.Contains("StarDash — original artwork"), "Minecraft artwork leaked into StarDash");
                    form.GameKind = 0;
                    form.notes.Text = new string('X', 5000);
                    AuditAssert(form.notes.Multiline && form.notes.ScrollBars == ScrollBars.Vertical, "Long update notes are not scrollable");
                    checks.Add("Long update notes retain a scrolling text area");
                    form.notes.Text = String.Join(Environment.NewLine, Enumerable.Range(0, 80).Select(i => "Scrollable line " + i));
                    Application.DoEvents();
                    AuditAssert(form.notes.Maximum > 0, "Textured scrollbar has no scroll range");
                    form.notes.MoveTo(form.notes.Maximum);
                    AuditAssert(form.notes.FirstLine > 0, "Textured scrollbar failed to move text");
                    form.notes.MoveTo(0);
                    AuditAssert(form.notes.FirstLine == 0, "Textured scrollbar failed to return to top");
                    checks.Add("Textured scrollbar moves the native text viewport and returns to the top");
                    form.Close();
                }

                AsteraDiagnostics.Initialize(Path.Combine(directory, "diagnostics"));
                AsteraDiagnostics.Write("Diagnostic fixture", new InvalidOperationException("outer fixture", new ArgumentException("inner fixture")));
                string diagnostic = File.ReadAllText(AsteraDiagnostics.SessionLog);
                AuditAssert(diagnostic.Contains("outer fixture") && diagnostic.Contains("inner fixture") && diagnostic.Contains("Astera "), "Exception details or build version were lost");
                checks.Add("Astera session log retains exception details, inner cause and build version");
                string fixture = Path.Combine(directory, "game-fixture"), crashes = Path.Combine(fixture, "crash-reports"), runtime = Path.Combine(fixture, "logs"), godot = Path.Combine(directory, "godot-fixture");
                Directory.CreateDirectory(crashes);
                Directory.CreateDirectory(runtime);
                Directory.CreateDirectory(godot);
                string crash = Path.Combine(crashes, "crash-fixture.txt");
                File.WriteAllText(crash, "unique game crash evidence");
                File.WriteAllText(Path.Combine(runtime, "latest.log"), "runtime fixture");
                File.WriteAllText(Path.Combine(godot, "godot.log"), "Godot fixture");
                string originalHash = Engine.Hash(crash);
                AuditAssert(GameDiagnostics.Collect(AsteraDiagnostics.Root, "minecraft", fixture, godot, false) == 1, "Automatic crash collection missed report");
                AuditAssert(GameDiagnostics.Collect(AsteraDiagnostics.Root, "minecraft", fixture, godot, false) == 0, "Unchanged game report was duplicated");
                AuditAssert(Engine.Hash(crash) == originalHash, "Original crash report was modified");
                checks.Add("Automatic Minecraft crash collection preserves originals and deduplicates");
                AuditAssert(GameDiagnostics.Collect(AsteraDiagnostics.Root, "minecraft", fixture, godot, true) == 1 && GameDiagnostics.Collect(AsteraDiagnostics.Root, "stardash", fixture, godot, true) == 1, "Explicit runtime log collection failed");
                checks.Add("Explicit collection includes Minecraft and StarDash runtime logs separately");
                string selected = "11111111222233334444555555555555";
                string accountFixture = "{\"activeAccountLocalId\":\"selected\",\"accounts\":{\"other\":{\"minecraftProfile\":{\"id\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"}},\"selected\":{\"accessToken\":\"synthetic-not-a-credential\",\"minecraftProfile\":{\"id\":\"" + selected + "\",\"name\":\"Fixture\"}}}}";
                AuditAssert(MinecraftSkin.SelectedProfile(accountFixture) == selected, "Skin lookup selected the wrong launcher profile");
                AuditAssert(MinecraftSkin.SelectedProfile("{\"accounts\":{}} ") == null, "Missing active profile was guessed");
                checks.Add("Skin lookup selects only the active public profile and skips ambiguous/missing selections");
                using (var skinFixture = new Bitmap(64, 64))
                {
                    using (var g = Graphics.FromImage(skinFixture))
                        g.Clear(Color.CornflowerBlue);
                    foreach (bool slim in new[]
                    {
                        false,
                        true
                    }

                    )
                        using (var pose = MinecraftSkin.Render(skinFixture, slim))
                        {
                            AuditAssert(pose.Width == 512 && pose.Height == 350 && pose.GetPixel(0, 0).A == 0 && pose.GetPixel(256, 300).A > 0, "Minecraft pose canvas or silhouette is invalid");
                            pose.Save(Path.Combine(directory, slim ? "slim-pose.png" : "classic-pose.png"), ImageFormat.Png);
                        }
                }

                checks.Add("Classic and slim skins render into a transparent posed-character canvas");
                string rigFixture = Path.Combine(directory, "rig-fixture.png");
                using (var texture = new Bitmap(64, 64))
                {
                    using (var g = Graphics.FromImage(texture))
                    {
                        g.Clear(Color.SteelBlue);
                        g.FillRectangle(Brushes.White, 8, 8, 8, 8);
                        g.FillRectangle(Brushes.Red, 16, 20, 8, 12);
                    }

                    texture.Save(rigFixture + ".skin.png", ImageFormat.Png);
                }

                File.WriteAllText(rigFixture + ".model", "classic");
                using (var rig = new SkinRig(rigFixture))
                {
                    foreach (int frame in new[]
                    {
                        0,
                        1,
                        2
                    }

                    )
                        using (var image = new Bitmap(512, 350))
                        {
                            using (var g = Graphics.FromImage(image))
                            {
                                g.Clear(Color.Transparent);
                                rig.Draw(g, new Rectangle(0, 0, 512, 350), new Point(frame == 1 ? 0 : 512, frame == 2 ? 0 : 175), frame == 0 ? 0 : frame == 1 ? 1 : 4);
                            }

                            image.Save(Path.Combine(directory, "rig-" + frame + ".png"), ImageFormat.Png);
                        }
                }

                AuditAssert(Engine.Hash(Path.Combine(directory, "rig-0.png")) != Engine.Hash(Path.Combine(directory, "rig-1.png")) && Engine.Hash(Path.Combine(directory, "rig-1.png")) != Engine.Hash(Path.Combine(directory, "rig-2.png")), "Live skin wave, pointer aim and idle did not change the rendered rig");
                checks.Add("Layered cuboid skin rig renders distinct wave, pointer-follow and idle frames without a Minecraft runtime");
                using (var rig = new SkinRig(rigFixture))
                using (var image = new Bitmap(512, 350))
                using (var g = Graphics.FromImage(image))
                {
                    var directions = new System.Collections.Generic.List<PointF>();
                    foreach (var pointer in new[]
                    {
                        new Point(-100, 140),
                        new Point(700, 140),
                        new Point(235, -100),
                        new Point(235, 600)
                    }

                    )
                    {
                        for (int tick = 0; tick < 70; tick++)
                            rig.Draw(g, new Rectangle(0, 0, 512, 350), pointer, 4);
                        directions.Add(rig.LookDirection);
                    }

                    AuditAssert(directions[0].X < -.2 && directions[1].X > .2 && directions[2].Y < -.1 && directions[3].Y > .1, "Head tracking reversed a screen-space axis");
                    AuditAssert(Math.Abs(directions[0].Y - directions[1].Y) < .12 && Math.Abs(directions[2].X - directions[3].X) < .12, "Mouse axes leaked into each other");
                }

                checks.Add("Head looks left/right/up/down in screen space without swapped or reversed axes");
                using (var rig = new SkinRig(rigFixture))
                    rig.VerifyCrossingFaces();
                checks.Add("Intersecting textured faces choose nearest pixels independently of face order");
                using (var legacy = new Bitmap(64, 32))
                using (var pose = MinecraftSkin.Render(legacy, false))
                {
                    AuditAssert(pose.Width == 512, "Legacy skin rendering failed");
                }

                bool rejected = false;
                using (var invalid = new Bitmap(32, 32))
                {
                    try
                    {
                        using (var pose = MinecraftSkin.Render(invalid, false))
                        {
                        }
                    }
                    catch (IOException)
                    {
                        rejected = true;
                    }
                }

                AuditAssert(rejected, "Unsupported skin dimensions were accepted");
                checks.Add("Legacy skins are supported and invalid skin dimensions rejected");
                File.WriteAllText(Path.Combine(directory, "result.json"), Engine.Json.Serialize(new
                {
                    Passed = true,
                    Checks = checks,
                    LiveProfilesChanged = false,
                    NetworkUsed = false
                }));
                return 0;
            }
            catch (Exception error)
            {
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, "failure.txt"), error.ToString());
                return 1;
            }
        }
    }
}
