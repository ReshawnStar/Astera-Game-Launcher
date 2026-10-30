using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using MC2Pack;

namespace Astera
{
    sealed partial class LibraryForm
    {
        Panel modProfileStrip;
        ProfileLaunchButton modProfileChoose;
        ContextMenuStrip modProfileMenu;
        readonly HashSet<string> modProfileScanned = new HashSet<string>();
        ThunderGame SelectedThunderGame
        {
            get
            {
                var game = CurrentCustom;
                if (game == null)
                    return null;
                var art = CustomSteam(game);
                return ThunderstoreProfiles.Game(game.Platform == "steam" ? game.PlatformId : art == null ? null : art.AppId);
            }
        }

        ModProfileLink SelectedModProfile(CustomGame game)
        {
            return game == null || game.ModProfiles == null ? null : game.ModProfiles.FirstOrDefault(p => p.Id == game.SelectedModProfile);
        }

        void BuildModProfileStrip()
        {
            modProfileStrip = new Panel
            {
                Width = 158,
                Height = 48,
                MinimumSize = new Size(158, 48),
                Margin = new Padding(0, 0, 10, 0),
                BackColor = Color.Transparent,
                Visible = false
            };
            modProfileChoose = new ProfileLaunchButton
            {
                Dock = DockStyle.Fill
            };
            modProfileChoose.Click += delegate
            {
                if (GameKind == MinecraftGame)
                    ShowMinecraftProfileMenu();
                else
                    ShowModProfileMenu();
            };
            modProfileStrip.Controls.Add(modProfileChoose);
            heroActions.Controls.Add(modProfileStrip);
            modProfileMenu = AsteraMenuRenderer.Create(menuComponents);
        }

        void RefreshModProfileStrip()
        {
            if (modProfileStrip == null)
                return;
            var game = CurrentCustom;
            var definition = SelectedThunderGame;
            modProfileStrip.Visible = game != null && definition != null;
            if (!modProfileStrip.Visible)
                return;
            check.Visible = false;
            if (!previewMode && modProfileScanned.Add(game.Id))
                ScanModProfiles(game, definition);
            var selected = SelectedModProfile(game);
            modProfileChoose.ProfileName = selected == null ? "Vanilla" : selected.Name;
            modProfileChoose.AccessibleName = "Play profile: " + modProfileChoose.ProfileName;
            modProfileChoose.Invalidate();
            modProfileChoose.Enabled = !busy && !GameRunning(game) && !GameStarting(game);
        }

        void ShowModProfileMenu()
        {
            var game = CurrentCustom;
            var definition = SelectedThunderGame;
            if (game == null || definition == null || busy || GameRunning(game) || GameStarting(game))
                return;
            ClearLibraryMenu(modProfileMenu);
            Action<string, string> add = delegate (string title, string id)
            {
                var item = new ToolStripMenuItem(title)
                {
                    Checked = (game.SelectedModProfile ?? "") == id
                };
                item.Click += delegate
                {
                    SelectLaunchProfile(game, id);
                };
                modProfileMenu.Items.Add(item);
            };
            add("Vanilla", "");
            foreach (var profile in game.ModProfiles ?? new List<ModProfileLink>())
                add(profile.Name, profile.Id);
            modProfileMenu.Items.Add(new ToolStripSeparator());
            modProfileMenu.Items.Add("Manage profiles…", null, delegate
            {
                ChooseModProfile();
            });
            var import = modProfileMenu.Items.Add("Import by code…", null, async delegate
            {
                await UpdateModProfile(true);
            });
            import.Enabled = definition.Loader == "bepinex";
            if (SelectedModProfile(game) != null)
            {
                var update = modProfileMenu.Items.Add("Update selected by code…", null, async delegate
                {
                    await UpdateModProfile();
                });
                update.Enabled = definition.Loader == "bepinex";
            }

            modProfileMenu.Items.Add(new ToolStripSeparator());
            modProfileMenu.Items.Add("Open in Steam", null, delegate
            {
                OpenCustomSteamLibrary();
            });
            modProfileMenu.Show(modProfileChoose, new Point(0, modProfileChoose.Height));
        }

        void SelectLaunchProfile(CustomGame game, string id)
        {
            if (game == null || CurrentCustom != game || busy || GameRunning(game) || GameStarting(game))
                return;
            if (!String.IsNullOrEmpty(id) && (game.ModProfiles == null || !game.ModProfiles.Any(p => p.Id == id)))
                return;
            game.SelectedModProfile = id ?? "";
            store.Save();
            RefreshModProfileStrip();
            LayoutHeroContent();
        }

        async void ScanModProfiles(CustomGame game, ThunderGame definition)
        {
            try
            {
                var roots = ThunderstoreProfiles.Roots(store.Settings.ThunderstoreRoots).ToArray();
                var found = await Task.Run(() => ThunderstoreProfiles.Discover(definition, roots));
                if (IsDisposed)
                    return;
                ThunderstoreProfiles.Merge(game, found);
                store.Save();
                if (CurrentCustom == game)
                {
                    RefreshModProfileStrip();
                    LayoutHeroContent();
                }
            }
            catch (Exception error)
            {
                AsteraDiagnostics.Write("Thunderstore profile scan", error);
            }
        }

        void ChooseModProfile()
        {
            var game = CurrentCustom;
            var definition = SelectedThunderGame;
            if (game == null || definition == null || busy || GameRunning(game))
                return;
            using (var dialog = new AsteraDialog
            {
                Text = game.Name + " profiles",
                Size = new Size(650, 480),
                StartPosition = FormStartPosition.CenterParent
            }

            )
            {
                var list = new ListBox
                {
                    Dock = DockStyle.Fill,
                    BackColor = Base,
                    ForeColor = Color.White,
                    BorderStyle = BorderStyle.None
                };
                var rows = new List<ModProfileLink>();
                Action fill = delegate
                {
                    rows = new List<ModProfileLink>
                    {
                        null
                    };
                    rows.AddRange(game.ModProfiles ?? new List<ModProfileLink>());
                    list.Items.Clear();
                    foreach (var row in rows)
                        list.Items.Add(row == null ? "Vanilla" : row.Name + "  ·  " + (row.UseManaged && !String.IsNullOrEmpty(row.Managed) ? "Astera copy" : row.Manager));
                    list.SelectedIndex = Math.Max(0, rows.FindIndex(p => p != null && p.Id == game.SelectedModProfile));
                };
                var message = new Label
                {
                    Dock = DockStyle.Top,
                    Height = 54,
                    Padding = new Padding(12),
                    Text = "Choose which profile Play will use. Updates stay in Astera; your manager's original profile is preserved."
                };
                var actions = new FlowLayoutPanel
                {
                    Dock = DockStyle.Bottom,
                    Height = 60,
                    Padding = new Padding(10)
                };
                var use = Button("Use profile", delegate
                {
                    int index = list.SelectedIndex;
                    if (index < 0)
                        return;
                    if (GameRunning(game))
                    {
                        MessageBox.Show(dialog, "Close the game before switching profiles.", "Game is running");
                        return;
                    }

                    if (rows[index] != null)
                        rows[index].UseManaged = true;
                    game.SelectedModProfile = rows[index] == null ? "" : rows[index].Id;
                    store.Save();
                    RefreshModProfileStrip();
                    dialog.DialogResult = DialogResult.OK;
                });
                var scan = Button("Refresh", delegate
                {
                    ThunderstoreProfiles.Merge(game, ThunderstoreProfiles.Discover(definition, ThunderstoreProfiles.Roots(store.Settings.ThunderstoreRoots)));
                    store.Save();
                    fill();
                });
                var browse = Button("Find data folder…", delegate
                {
                    using (var picker = new ExplorerFolderDialog
                    {
                        Description = "Choose the manager's data folder containing the game folders"
                    }

                    )
                        if (picker.ShowDialog(dialog) == DialogResult.OK)
                        {
                            ThunderstoreProfiles.Merge(game, ThunderstoreProfiles.Discover(definition, new[] { picker.SelectedPath }));
                            if (!store.Settings.ThunderstoreRoots.Contains(picker.SelectedPath, StringComparer.OrdinalIgnoreCase))
                                store.Settings.ThunderstoreRoots.Add(picker.SelectedPath);
                            store.Save();
                            fill();
                        }
                });
                var original = Button("Use original", delegate
                {
                    int index = list.SelectedIndex;
                    if (index <= 0 || GameRunning(game))
                        return;
                    var link = rows[index];
                    if (String.IsNullOrEmpty(link.Source) || !Directory.Exists(link.Source))
                        return;
                    link.UseManaged = false;
                    game.SelectedModProfile = link.Id;
                    store.Save();
                    RefreshModProfileStrip();
                    dialog.DialogResult = DialogResult.OK;
                });
                actions.Height = 100;
                actions.WrapContents = true;
                actions.Controls.AddRange(new Control[] { use, original, scan, browse });
                dialog.Controls.Add(list);
                dialog.Controls.Add(message);
                dialog.Controls.Add(actions);
                fill();
                list.DoubleClick += delegate
                {
                    use.PerformClick();
                };
                dialog.ShowDialog(this);
            }
        }

        async Task UpdateModProfile(bool importNew = false)
        {
            var game = CurrentCustom;
            var definition = SelectedThunderGame;
            if (game == null || definition == null || busy || GameRunning(game) || GameStarting(game))
                return;
            var selected = importNew ? null : SelectedModProfile(game);
            string code;
            TextBox input;
            using (var dialog = BuildProfileCodeDialog(selected, out input))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                code = input.Text;
            }

            string key = "custom:" + game.Id;
            ThunderImportPlan plan = null;
            string work = SelfUpdateFiles.Inside(SelfUpdateFiles.Root(store.Root), "thunderstore-work/" + Guid.NewGuid().ToString("N"));
            await Work(delegate
            {
                plan = ThunderstoreImport.Download(code, work, cancellation.Token, ReportWorkProgress, definition, SelfUpdateFiles.Inside(SelfUpdateFiles.Root(store.Root), "thunderstore-api-cache"));
            }, false, key);
            if (plan == null || IsDisposed)
                return;
            if (MessageBox.Show(this, "Install " + plan.Export.Name + " for " + game.Name + "?\n\n" + plan.Packages.Count + " enabled packages. Astera will keep its own copy; the original manager profile and game saves stay intact.", "Review mod profile", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            if (await Task.Run(() => RunningGames.Check(game, definition.SteamId)))
            {
                MessageBox.Show(this, "Close the game before updating its profile.", "Game is running");
                return;
            }

            var link = selected ?? new ModProfileLink
            {
                Name = plan.Export.Name,
                Manager = "Astera"
            };
            string ownedId = String.IsNullOrEmpty(link.ManagedId) ? Guid.NewGuid().ToString("N") : link.ManagedId;
            string target = null;
            await Work(delegate
            {
                target = ThunderstoreImport.Install(plan, definition, SelfUpdateFiles.Inside(SelfUpdateFiles.Root(store.Root), "mod-profiles/" + definition.SteamId), ownedId, selected == null ? null : ThunderstoreProfiles.Active(selected), cancellation.Token, ReportWorkProgress, Log, () => RunningGames.Check(game, definition.SteamId), null, SelfUpdateFiles.Inside(SelfUpdateFiles.Root(store.Root), "thunderstore-cache"));
            }, false, key);
            if (target != null)
            {
                if (game.ModProfiles == null)
                    game.ModProfiles = new List<ModProfileLink>();
                if (selected == null)
                    game.ModProfiles.Add(link);
                link.UseManaged = true;
                link.Managed = target;
                link.ManagedId = ownedId;
                link.Code = code;
                game.SelectedModProfile = link.Id;
                store.Save();
                LogActivity(key, "Mod profile ready: " + link.Name);
                if (CurrentCustom == game)
                {
                    RefreshView();
                    LayoutHeroContent();
                }
            }
        }

        bool LaunchThunderProfile(CustomGame game)
        {
            var definition = SelectedThunderGame;
            if (definition == null)
                return false;
            var profile = SelectedModProfile(game);
            if (definition.Loader != "bepinex")
            {
                if (profile != null)
                    throw new IOException("This profile uses a loader that still needs its original mod manager. Astera did not start the vanilla game.");
                return false;
            }

            if (profile == null && !String.IsNullOrEmpty(game.SelectedModProfile))
                throw new IOException("The selected profile is missing. Choose a profile again.");
            ProcessThunderLaunch(game, definition, profile == null ? null : ThunderstoreProfiles.Active(profile));
            return true;
        }

        void ProcessThunderLaunch(CustomGame game, ThunderGame definition, string profile)
        {
            var start = ThunderstoreLaunch.Start(definition, profile, PlatformLibraries.Folder(game), SelfUpdateFiles.Inside(SelfUpdateFiles.Root(store.Root), "thunderstore-launch-backups/" + definition.SteamId));
            System.Diagnostics.Process.Start(start);
            TrackCustomLaunch(game);
        }
    }
}
