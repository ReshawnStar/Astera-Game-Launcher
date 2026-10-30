using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MC2Pack;

namespace Astera
{
    sealed partial class LibraryForm : Form
    {
        // These numeric IDs are persisted in preferences and update activity.
        // Keep their values stable when adding another built-in game.
        const int MinecraftGame = 0;
        const int StarDashGame = 1;
        const int MultiverseGame = 2;
        readonly Store store;
        readonly Channel channel;
        readonly GameListBox games = new GameListBox();
        readonly Banner banner = new Banner();
        readonly Label status = new Label(), profileLabel = new Label(), releaseLabel = new Label();
        readonly Button play = new AsteraButton(), check = new AsteraButton(), download = new AsteraButton(), install = new AsteraButton(), cancel = new AsteraButton(), settings = new AsteraButton(), restore = new AsteraButton();
        readonly ScrollText notes = new ScrollText(), log = new ScrollText();
        readonly NotifyIcon tray = new NotifyIcon();
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        UpdateFeed feed;
        Plan plan;
        string prepared;
        bool busy, quitting, updateReady, transaction;
        CancellationTokenSource cancellation;
        static readonly Color Base = Color.FromArgb(24, 28, 36), Panel = Color.FromArgb(33, 39, 49), Muted = Color.FromArgb(167, 179, 197);
        readonly bool previewMode;
        public LibraryForm(Store state, Channel source, bool preview = false)
        {
            previewMode = preview;
            store = state;
            instanceShowMessage = SingleInstance.MessageFor(store.Root);
            channel = source;
            Text = "Astera Game Launcher";
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
            Size = new Size(1400, 900);
            MinimumSize = new Size(1100, 760);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Base;
            ForeColor = Color.White;
            Font = UiTheme.Text(14);
            AutoScaleMode = AutoScaleMode.Dpi;
            if (String.IsNullOrEmpty(store.Settings.FeedUrl) || store.Settings.FeedUrl == "https://raw.githubusercontent.com/ReshawnStar/Astera-Game-Launcher/main/channels/minecraft-squared.json")
                store.Settings.FeedUrl = channel.FeedUrl ?? "";
            if (!previewMode && String.IsNullOrEmpty(store.Settings.StarDashExecutable))
            {
                store.Settings.StarDashExecutable = LocalStarDash.DefaultAt(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
                if (!String.IsNullOrEmpty(store.Settings.StarDashExecutable))
                    store.Save();
            }

            if (!previewMode && String.IsNullOrEmpty(store.Settings.Profile))
            {
                var matches = Profiles.Discover(Profiles.DefaultRoot);
                if (matches.Count == 0)
                    try
                    {
                        string data = String.IsNullOrWhiteSpace(store.Settings.PrismDataRoot) ? PrismProfiles.DefaultData : store.Settings.PrismDataRoot;
                        matches = Profiles.Discover(PrismProfiles.InstanceDirectory(data));
                    }
                    catch (Exception error)
                    {
                        AsteraDiagnostics.Write("Prism discovery unavailable", error);
                    }

                if (matches.Count == 1)
                {
                    store.Settings.Profile = matches[0].Path;
                    store.Settings.MinecraftLauncher = matches[0].IsPrism ? "prism" : "curseforge";
                    store.Save();
                }
            }

            UiEffects.Enabled = store.Settings.AnimatedEffects;
            ButtonAtlas.SetAccent(store.Settings.ButtonAccent);
            BuildLibraryLayout();
            StartSteamArtwork();
            StartRunningChecks();
            StartPlaytime();
            PageRedraw.BufferLayout(this);
            GameKind = MinecraftGame;
            if (previewMode)
            {
                RefreshView();
                return;
            }

            StartDiagnostics();
            BuildStartupPresentation();
            tray.Icon = Icon;
            tray.Text = "Astera Game Launcher";
            var menu = AsteraMenuRenderer.Create(menuComponents);
            menu.Items.Add("Open Astera", null, delegate
            {
                ShowWindow();
            });
            menu.Items.Add("Exit", null, delegate
            {
                quitting = true;
                Close();
            });
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += delegate
            {
                ShowWindow();
            };
            tray.BalloonTipClicked += delegate
            {
                ShowWindow();
            };
            tray.Visible = true;
            timer.Interval = 30 * 60 * 1000;
            timer.Tick += async delegate
            {
                if (store.Settings.CheckOnStart && !busy)
                    await CheckScheduled();
            };
            timer.Start();
            Shown += async delegate
            {
                await PrepareStartup();
            };
            FormClosing += delegate (object sender, FormClosingEventArgs e)
            {
                if (busy && startupScreen != null && !transaction)
                {
                    if (cancellation != null)
                        cancellation.Cancel();
                    if (!store.Settings.KeepInTray)
                        quitting = true;
                    e.Cancel = true;
                    Hide();
                    return;
                }

                if (busy)
                {
                    e.Cancel = true;
                    MessageBox.Show(this, "Let the current operation finish, or cancel the download first.", "Astera");
                    return;
                }

                if (!quitting && store.Settings.KeepInTray)
                {
                    e.Cancel = true;
                    Hide();
                    tray.ShowBalloonTip(2500, "Astera is still here", "Update checks continue while Astera is in the tray.", ToolTipIcon.Info);
                }
                else
                {
                    timer.Stop();
                    ClearPrepared();
                }
            };
            RefreshView();
        }

        static Button Button(string text, EventHandler handler)
        {
            var button = new AsteraButton
            {
                Text = text
            };
            Style(button);
            button.Click += handler;
            return button;
        }

        internal static void Style(Button button)
        {
            button.AutoSize = true;
            button.MinimumSize = new Size(80, 40);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.UseVisualStyleBackColor = false;
            button.BackColor = button is AsteraButton ? Color.Transparent : Color.FromArgb(38, 48, 80);
            button.ForeColor = Color.FromArgb(239, 242, 255);
            button.Padding = new Padding(14, 8, 14, 8);
            button.Margin = new Padding(0, 0, 10, 0);
        }

        void DrawGame(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0)
                return;
            var entry = (LibraryEntry)games.Items[e.Index];
            using (var b = new SolidBrush((e.State & DrawItemState.Selected) != 0 ? Color.FromArgb(46, 58, 77) : games.BackColor))
                e.Graphics.FillRectangle(b, e.Bounds);
            int textX = e.Bounds.X + 64;
            Image icon = entry.Kind == 0 ? MinecraftSelectedIcon() : entry.Kind == 1 ? GameArtwork.StarDashIcon : entry.Kind == 2 ? GameArtwork.FtlIcon : CustomLibraryIcon(entry.Custom);
            if (icon != null)
            {
                GameArtwork.Contain(e.Graphics, icon, new Rectangle(e.Bounds.X + 8, e.Bounds.Y + 13, 46, 46));
                textX = e.Bounds.X + 64;
            }

            var title = new Rectangle(textX, e.Bounds.Y + 9, Math.Max(1, LibraryRowRight - textX - 10), 24);
            using (var f = UiTheme.Text(15, true))
                TextRenderer.DrawText(e.Graphics, entry.ToString(), f, title, Color.White, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.PreserveGraphicsClipping | TextFormatFlags.PreserveGraphicsTranslateTransform);
            string detail = entry.Kind == 0 ? (updateReady ? "Update ready" : store.Settings.MinecraftLauncher == "prism" ? "Prism instance" : "CurseForge profile") : entry.Kind == 2 ? (multiverseUpdate ? "Update ready" : "Install / update") : entry.Kind == 1 ? (LocalStarDash.IsValid(store.Settings.StarDashExecutable) ? "Ready to play" : "Link your app") : entry.Custom.ManagedProjectId == ManagedGames.Id ? ManagedRowStatus(entry.Custom) : entry.Custom.Platform == "roblox" ? "Managed by Roblox" : PlatformLibraries.Available(entry.Custom) ? "Ready to play" : "EXE not found";
            using (var f = UiTheme.Text(12))
                TextRenderer.DrawText(e.Graphics, detail, f, new Rectangle(textX, e.Bounds.Y + 35, Math.Max(1, LibraryRowRight - textX - 10), 24), Color.LightSlateGray, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.PreserveGraphicsClipping | TextFormatFlags.PreserveGraphicsTranslateTransform);
        }

        void RefreshView()
        {
            EnsureMinecraftSelection();
            check.Visible = true;
            if ((!IsCustomGame || IsRoblox) && modProfileStrip != null)
                modProfileStrip.Visible = false;
            RefreshActivityLog();
            UpdatePlaytimeLabel();
            if (GameKind < 0)
            {
                RefreshEmptyLibrary();
                return;
            }

            UpdateRobloxSlidePlayback();
            UpdateWorkProgress();
            UpdateTransferSlot();
            latestHeading.Text = "Latest update";
            check.Text = "Check for updates";
            if (IsCustomGame)
            {
                RefreshCustomView();
                return;
            }

            appUpdateIcon.Enabled = !busy && !checkingApp;
            if (IsMultiverse)
            {
                RefreshMultiverseView();
                ApplyBuiltinRunning();
                return;
            }

            ((AsteraButton)play).Symbol = GameKind == MinecraftGame && updateReady ? ButtonSymbol.Download : ButtonSymbol.Play;
            bool mc = GameKind == MinecraftGame;
            banner.Game = mc ? "Minecraft²" : "Star Dash";
            banner.Invalidate();
            play.Text = mc ? (updateReady ? "UPDATE" : String.IsNullOrWhiteSpace(store.Settings.Profile) ? "SET UP" : "PLAY") : (LocalStarDash.IsValid(store.Settings.StarDashExecutable) ? "PLAY" : "LINK APP");
            play.Enabled = !busy;
            check.Enabled = mc && !busy;
            download.Enabled = mc && !busy && feed != null && updateReady;
            install.Enabled = mc && !busy && plan != null;
            restore.Enabled = mc && !busy && !String.IsNullOrWhiteSpace(store.Settings.Profile);
            cancel.Enabled = busy && cancellation != null && !transaction;
            settings.Enabled = !busy;
            games.Enabled = !busy;
            if (!mc)
            {
                bool ready = LocalStarDash.IsValid(store.Settings.StarDashExecutable);
                status.Text = ready ? "StarDash • ready to play" : "Choose your StarDash installation";
                status.ForeColor = Color.White;
                profileLabel.Text = ready ? store.Settings.StarDashExecutable : "Choose your StarDash installation to get started.";
                notes.Text = UiText.WindowsLines("StarDash — What’s New\n\nNo patch notes available yet.\n\nUpdates, improvements, bug fixes, and new features will be listed here as they’re released.");
                releaseLabel.Text = ready ? "Ready to play" : "Choose your installation to get started.";
                games.Invalidate();
                UpdateLayoutView();
                ApplyBuiltinRunning();
                return;
            }

            string profile = store.Settings.Profile;
            if (String.IsNullOrWhiteSpace(profile))
                profileLabel.Text = "Link a Minecraft² instance in Settings.";
            else
            {
                try
                {
                    Profiles.Read(profile);
                    profileLabel.Text = "Your installation is linked.";
                }
                catch
                {
                    profileLabel.Text = "Profile unavailable — choose it again in Settings.";
                }
            }

            status.Text = minecraftChannelBehind ? "Up to date" : feed == null ? (String.IsNullOrWhiteSpace(store.Settings.FeedUrl) ? "Connect your update channel in Settings" : "Ready to check for updates") : updateReady ? "Update ready • " + feed.Release.Name : "Up to date • " + feed.Release.Name;
            status.ForeColor = updateReady ? Color.LightGreen : Color.White;
            notes.Text = PatchNotes.Minecraft(feed);
            releaseLabel.Text = updateReady ? "An update is ready to install." : feed == null ? "Check for updates when you are ready." : "You are up to date.";
            games.Invalidate();
            UpdateLayoutView();
        }

        void ShowWindow()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }

        async void Configure()
        {
            if (busy)
                return;
            if (IsCustomGame)
            {
                if (CurrentCustom.ManagedProjectId == ManagedGames.Id)
                    ConfigureManagedGame();
                else
                    EditCustomGame();
                return;
            }

            if (IsMultiverse)
            {
                ConfigureMultiverse();
                return;
            }

            if (GameKind == MinecraftGame)
            {
                ConfigureMinecraft();
                return;
            }

            var priorSettings = store.Settings;
            using (var dialog = new SettingsForm(store.Settings, GameKind))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                ClearPrepared();
                minecraftChannelBehind = false;
                feed = null;
                plan = null;
                updateReady = false;
                store.Settings = dialog.Result;
                store.Save();
                RefreshView();
                if (GameKind == MinecraftGame && NeedsProfileCheck(priorSettings, store.Settings))
                    await CheckMinecraft(false);
            }
        }

        async Task Work(Action operation, bool background, string activityKey = null)
        {
            if (busy)
                return;
            string priorActivity = activityContext.Value;
            workActivityKey = activityKey ?? SelectedActivityKey ?? "app";
            activityContext.Value = workActivityKey;
            workPhase = "Working";
            workStartedUtc = DateTime.UtcNow;
            workDone = workTotal = lastProgressTicks = 0;
            busy = true;
            cancellation = new CancellationTokenSource();
            RefreshView();
            try
            {
                await Task.Run(operation);
            }
            catch (Exception error)
            {
                plan = null;
                AsteraDiagnostics.Write("Update operation", error);
                Log(cancellation.IsCancellationRequested ? (workPhase.StartsWith("Removing", StringComparison.Ordinal) ? "Removal stopped. Saved data is kept; use Install to repair or retry removal." : "Download/check cancelled. No installation was started.") : "Stopped: " + error.Message);
                if (!background && !cancellation.IsCancellationRequested)
                    MessageBox.Show(this, error.Message, "Astera", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            finally
            {
                cancellation.Dispose();
                cancellation = null;
                busy = false;
                workActivityKey = null;
                activityContext.Value = priorActivity;
                RefreshView();
            }
        }

        async Task Check(bool background)
        {
            if (IsRoblox)
            {
                OpenRobloxPage();
                return;
            }

            if (IsCustomGame)
            {
                if (CurrentCustom.ManagedProjectId == ManagedGames.Id)
                    await CheckManagedProject();
                else
                    OpenCustomSteamLibrary();
                return;
            }

            if (IsMultiverse)
                await CheckMultiverse(background);
            else
                await CheckMinecraft(background);
        }

        async Task CheckScheduled(Action<string> report = null)
        {
            if (!preparingStartup && store.Settings.CheckAppOnStart)
            {
                if (report != null)
                    report("Checking Astera updates…");
                await CheckAsteraUpdates(true);
            }

            if (MinecraftSquaredSelected && !String.IsNullOrWhiteSpace(store.Settings.Profile) && !String.IsNullOrWhiteSpace(store.Settings.FeedUrl))
            {
                if (report != null)
                    report("Checking Minecraft² updates…");
                await CheckMinecraft(true);
            }

            if (!multiversePrepared && !String.IsNullOrWhiteSpace(store.Settings.FtlDirectory))
            {
                if (report != null)
                    report("Checking FTL updates…");
                await CheckMultiverse(true);
            }

            if (store.Settings.CustomGames.Any(g => g.ManagedProjectId == ManagedGames.Id))
            {
                if (report != null)
                    report("Checking project updates…");
                await RefreshDiscoverCatalog(true);
                if (!IsDisposed && IsCustomGame && CurrentCustom.ManagedProjectId == ManagedGames.Id)
                    RefreshView();
            }
        }

        async Task CheckMinecraft(bool background)
        {
            EnsureMinecraftSelection();
            if (!MinecraftSquaredSelected)
            {
                if (!background)
                    ConfigureMinecraft();
                return;
            }

            if (String.IsNullOrWhiteSpace(store.Settings.FeedUrl))
            {
                Configure();
                return;
            }

            bool previous = updateReady;
            await Work(delegate
            {
                var client = new UpdateClient(channel.PublicKeyXml);
                var current = client.Check(store.Settings.FeedUrl, cancellation.Token);
                if (current.Release.Sequence < store.Settings.HighestSequence)
                    throw new Exception("The channel served an older release than one Astera already verified.");
                if (NewerLocalComponents(current, store.Settings.Profile))
                {
                    ClearPrepared();
                    plan = null;
                    feed = current;
                    updateReady = false;
                    minecraftChannelBehind = true;
                    store.Settings.HighestSequence = Math.Max(store.Settings.HighestSequence, current.Release.Sequence);
                    store.Save();
                    Log("The installed Stars components are newer than this channel; no downgrade offered.");
                    return;
                }

                minecraftChannelBehind = false;
                var inspection = client.Prepare(current, store.Settings.Profile, store.Cache, false);
                ClearPrepared();
                plan = null;
                feed = current;
                updateReady = current.Release.Files.Any(f => !File.Exists(Engine.Inside(Engine.Root(store.Settings.Profile), f.Path)) || f.Policy == "replace" && !Engine.Hash(Engine.Inside(Engine.Root(store.Settings.Profile), f.Path)).Equals(f.Hash, StringComparison.OrdinalIgnoreCase));
                store.Settings.HighestSequence = Math.Max(store.Settings.HighestSequence, current.Release.Sequence);
                store.Save();
                Log(updateReady ? "Update ready: " + current.Release.Name + ". " + inspection.DownloadFiles + " files to download (" + (inspection.DownloadBytes / 1048576D).ToString("0.0") + " MiB)." : "Your managed files match " + current.Release.Name + ".");
            }, background, "builtin:0");
            if (updateReady && !previous && feed != null)
                tray.ShowBalloonTip(6000, "Minecraft² • Update ready", feed.Release.Name + " is ready in Astera.", ToolTipIcon.Info);
        }

        async Task Download()
        {
            if (GameKind == MinecraftGame && !MinecraftSquaredSelected)
                return;
            if (IsMultiverse)
            {
                await DownloadMultiverse();
                return;
            }

            if (feed == null)
                return;
            var chosen = feed;
            await Work(delegate
            {
                var client = new UpdateClient(channel.PublicKeyXml);
                var inspection = client.Prepare(chosen, store.Settings.Profile, store.Cache);
                ClearPrepared();
                prepared = client.Materialize(inspection, store.Cache, cancellation.Token, Log, ReportWorkProgress);
                ReportWorkProgress("Checking install", 0, 0);
                plan = Engine.Scan(store.Settings.Profile, prepared, store.Settings.ConfigurePacks, false, false, Log);
                plan.Preconditions = inspection.Guards;
                foreach (var warning in plan.Warnings.Where(w => !w.StartsWith("Extra mod retained;")))
                    Log(warning);
                foreach (var edit in plan.Edits)
                    Log(edit.Reason + ": " + edit.Path);
                Log("Ready to install: " + plan.Edits.Count + " changes. Worlds and personal settings are protected.");
            }, false, "builtin:0");
        }

        async Task Install()
        {
            if (GameKind == MinecraftGame && !MinecraftSquaredSelected)
                return;
            if (IsMultiverse)
            {
                await InstallMultiverse();
                return;
            }

            if (plan == null)
                return;
            var chosen = plan;
            if (MessageBox.Show(this, "Install " + chosen.Release.Name + "?\n\nClose Minecraft first. " + chosen.Edits.Count + " previewed changes will be applied to:\n" + chosen.Profile + "\n\nExisting files are backed up before replacement.", "Install Minecraft² update", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            transaction = true;
            try
            {
                await Work(delegate
                {
                    cancellation.Token.ThrowIfCancellationRequested();
                    ReportWorkProgress("Preparing install", 0, 0);
                    Log(Engine.Apply(chosen, Log, false, -1, delegate (int done, int total)
                    {
                        ReportWorkProgress("Installing", done, total);
                    }));
                    plan = null;
                    updateReady = false;
                    ClearPrepared();
                }, false, "builtin:0");
            }
            finally
            {
                transaction = false;
                RefreshView();
            }
        }

        async Task Restore()
        {
            if (GameKind == MinecraftGame && !MinecraftSquaredSelected)
                return;
            if (IsMultiverse)
            {
                await RestoreMultiverse();
                return;
            }

            using (var dialog = new ExplorerFolderDialog
            {
                Description = "Choose a dated folder inside this profile's Astera-backups",
                SelectedPath = Path.Combine(store.Settings.Profile ?? "", "Astera-backups")
            }

            )
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                string path = dialog.SelectedPath;
                if (MessageBox.Show(this, "Restore this backup? Files changed since installation will be preserved for review.", "Restore backup", MessageBoxButtons.YesNo) != DialogResult.Yes)
                    return;
                transaction = true;
                try
                {
                    await Work(delegate
                    {
                        Engine.Restore(store.Settings.Profile, path, Log);
                        ClearPrepared();
                        plan = null;
                        feed = null;
                        Log("Backup restored. Check for updates when you are ready.");
                    }, false, "builtin:0");
                }
                finally
                {
                    transaction = false;
                    RefreshView();
                }
            }
        }

        void ClearPrepared()
        {
            if (prepared != null && File.Exists(prepared))
                File.Delete(prepared);
            prepared = null;
        }

        void OpenStarDash()
        {
            if (!LocalStarDash.IsValid(store.Settings.StarDashExecutable))
            {
                Configure();
                return;
            }

            try
            {
                LocalStarDash.Validate(store.Settings.StarDashExecutable);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(store.Settings.StarDashExecutable) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(store.Settings.StarDashExecutable) });
                TrackLaunchedGame("builtin:1", new CustomGame { Executable = store.Settings.StarDashExecutable });
            }
            catch (Exception error)
            {
                MessageBox.Show(this, error.Message, "Play StarDash");
            }
        }

        void OpenCurseForge()
        {
            try
            {
                string path = FindCurseForge();
                if (path == null)
                {
                    MessageBox.Show(this, "Open CurseForge and launch Minecraft Squared from My Modpacks. If it is not installed yet, create/import that profile in CurseForge, then link it in Astera Settings.", "Play Minecraft²");
                    return;
                }

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
                if (!String.IsNullOrWhiteSpace(store.Settings.Profile))
                    TrackLaunchedGame("builtin:0", new CustomGame { Executable = store.Settings.Profile, Platform = "minecraft" });
            }
            catch (Exception error)
            {
                MessageBox.Show(this, error.Message, "Open CurseForge");
            }
        }

        static string FindCurseForge()
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var paths = new[]
            {
                Path.Combine(local, "Programs", "CurseForge Windows", "CurseForge.exe"),
                Path.Combine(local, "Programs", "CurseForge", "CurseForge.exe"),
                Convert.ToString(Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\App Paths\CurseForge.exe", "", null)),
                Convert.ToString(Microsoft.Win32.Registry.GetValue(@"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\App Paths\CurseForge.exe", "", null))
            };
            string direct = paths.FirstOrDefault(p => !String.IsNullOrWhiteSpace(p) && File.Exists(p));
            if (direct != null)
                return direct;
            foreach (var folder in new[]
            {
                Environment.SpecialFolder.StartMenu,
                Environment.SpecialFolder.CommonStartMenu,
                Environment.SpecialFolder.Desktop
            }

            )
            {
                string root = Environment.GetFolderPath(folder);
                if (!Directory.Exists(root))
                    continue;
                try
                {
                    var shortcut = Directory.GetFiles(root, "CurseForge.lnk", SearchOption.AllDirectories).FirstOrDefault();
                    if (shortcut != null)
                        return shortcut;
                }
                catch (UnauthorizedAccessException)
                {
                }
                catch (IOException)
                {
                }
            }

            return null;
        }
    }
}
