using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Astera
{
    sealed class ManagedInstallReview : AsteraDialog
    {
        internal string Target;
        internal bool Changed;
        internal ManagedInstallReview(string target, Action<ManagedInstallReview> choose, Action<ManagedInstallReview> locate)
        {
            Target = target;
            Text = "Install Gacha Nebula +";
            Size = new Size(680, 400);
            MinimumSize = new Size(600, 400);
            StartPosition = FormStartPosition.CenterParent;
            var content = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20),
                ColumnCount = 1,
                RowCount = 4
            };
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
            content.Controls.Add(new Label { Text = "Installation folder", AutoSize = true }, 0, 0);
            var path = new Label
            {
                Dock = DockStyle.Fill,
                AutoEllipsis = true,
                Text = Target
            };
            content.Controls.Add(path, 0, 1);
            content.Controls.Add(new Label { Text = "Your saves and Recovery backups are kept when uninstalling.", Dock = DockStyle.Fill }, 0, 2);
            var actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                WrapContents = true
            };
            var change = new AsteraButton
            {
                Text = "Change location…"
            };
            change.Click += delegate
            {
                choose(this);
                path.Text = Target;
            };
            var existing = new AsteraButton
            {
                Text = "Locate existing…"
            };
            existing.Click += delegate
            {
                locate(this);
                path.Text = Target;
            };
            var install = new AsteraButton
            {
                Text = "Continue"
            };
            install.Click += delegate
            {
                DialogResult = DialogResult.OK;
                Close();
            };
            actions.Controls.AddRange(new Control[] { change, existing, install });
            content.Controls.Add(actions, 0, 3);
            Controls.Add(content);
            AcceptButton = install;
        }
    }

    sealed partial class LibraryForm
    {
        ToolStripMenuItem managedUninstallItem, managedSavesItem;
        string ManagedRowStatus(CustomGame game)
        {
            var record = (store.Settings.ManagedInstalls ?? new System.Collections.Generic.List<ManagedInstallRecord>()).FirstOrDefault(r => r != null && r.ProjectId == ManagedGames.Id && String.Equals(r.Root, Path.GetDirectoryName(game.Executable), StringComparison.OrdinalIgnoreCase));
            return record != null && !record.Installed ? "Install • saves kept" : File.Exists(game.Executable) ? "Ready to play" : "Ready to install";
        }

        bool ManagedInstalled(CustomGame game)
        {
            return game != null && game.ManagedProjectId == ManagedGames.Id && ManagedLibrary.Installed(Path.GetDirectoryName(game.Executable));
        }

        void RememberManaged(string root, bool installed)
        {
            ManagedLibrary.Remember(store.Settings, root, installed, Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
            store.Save();
        }

        async Task<string> DiscoverExistingManaged(CatalogProject project)
        {
            var candidates = ManagedLibrary.Candidates(store.Settings, store.Root, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)).ToArray();
            return await Task.Run(() => ManagedLibrary.FindExisting(candidates, project.Package));
        }

        string RetainedManagedRoot()
        {
            var game = ManagedNebula;
            var roots = (game == null ? new string[0] : new[]
            {
                Path.GetDirectoryName(game.Executable)
            }

            ).Concat((store.Settings.ManagedInstalls ?? new System.Collections.Generic.List<ManagedInstallRecord>()).Where(x => x != null && x.ProjectId == ManagedGames.Id).Select(x => x.Root).Reverse());
            foreach (string root in roots.Where(x => !String.IsNullOrWhiteSpace(x)))
                try
                {
                    if (ManagedGames.State(root) != null)
                        return root;
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
                catch (ArgumentException)
                {
                }
                catch (NotSupportedException)
                {
                }

            return Path.Combine(ManagedLibrary.Root(store.Settings, store.Root), "Gacha Nebula +");
        }

        bool WarnSyncedRoot(string root)
        {
            bool synced = new[]
            {
                "OneDrive",
                "OneDriveConsumer",
                "OneDriveCommercial"
            }.Select(Environment.GetEnvironmentVariable).Where(x => !String.IsNullOrWhiteSpace(x)).Any(x => root.Equals(x, StringComparison.OrdinalIgnoreCase) || root.StartsWith(x.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase));
            return !synced || MessageBox.Show(this, "This location is synced by OneDrive, which may lock game files during installation or updates. Use it anyway?", "Synced game folder", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes;
        }

        string ChooseManagedLibraryRoot(IWin32Window owner, string current)
        {
            using (var picker = new ExplorerFolderDialog
            {
                Description = "Choose an Astera game library folder",
                SelectedPath = current
            }

            )
            {
                if (picker.ShowDialog(owner) != DialogResult.OK)
                    return null;
                string root = SelfUpdateFiles.Root(picker.SelectedPath);
                ManagedGames.NoLinks(root);
                if (!WarnSyncedRoot(root))
                    return null;
                string probe = Path.Combine(root, ".astera-write-check-" + Guid.NewGuid().ToString("N"));
                using (var file = new FileStream(probe, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.DeleteOnClose))
                    file.WriteByte(1);
                return root;
            }
        }

        string ReviewManagedTarget(string target, CatalogProject project)
        {
            using (var review = new ManagedInstallReview(target, delegate (ManagedInstallReview dialog)
            {
                try
                {
                    string chosen = ChooseManagedLibraryRoot(dialog, Path.GetDirectoryName(dialog.Target));
                    if (chosen != null)
                    {
                        dialog.Target = Path.Combine(chosen, "Gacha Nebula +");
                        dialog.Changed = true;
                    }
                }
                catch (Exception error)
                {
                    MessageBox.Show(dialog, error.Message, "Game library");
                }
            }, delegate (ManagedInstallReview dialog)
            {
                using (var picker = new ExplorerFolderDialog
                {
                    Description = "Locate an existing Astera-installed Gacha Nebula + folder",
                    SelectedPath = dialog.Target
                }

                )
                {
                    if (picker.ShowDialog(dialog) != DialogResult.OK)
                        return;
                    if (!ManagedLibrary.VerifiedExisting(picker.SelectedPath, project.Package))
                    {
                        MessageBox.Show(dialog, "A matching Astera installation was not found there. Choose its Gacha Nebula + folder, or continue with a fresh installation.", "Locate existing game");
                        return;
                    }

                    dialog.Target = picker.SelectedPath;
                }
            }))
            {
                if (review.ShowDialog(this) != DialogResult.OK)
                    return null;
                if (review.Changed)
                {
                    store.Settings.ManagedLibraryRoot = Path.GetDirectoryName(review.Target);
                    store.Save();
                }

                return review.Target;
            }
        }

        void UseExistingManaged(string root)
        {
            var game = RegisterManagedNebula(Path.Combine(root, ManagedGames.Launcher));
            ReloadGames(game);
            SelectPrimaryPage(0);
            RefreshView();
            LogActivity("custom:" + game.Id, "Found and linked your existing Gacha Nebula + installation. No download was needed.");
        }

        void BuildManagedLibraryMenu()
        {
            managedSavesItem = new ToolStripMenuItem("Saved data", null, delegate
            {
                OpenManagedSaves();
            });
            managedUninstallItem = new ToolStripMenuItem("Uninstall — keep saves", null, async delegate
            {
                await UninstallManagedGame();
            });
            moreMenu.Items.Add(managedSavesItem);
            moreMenu.Items.Add(managedUninstallItem);
            moreMenu.Opening += delegate
            {
                bool managed = IsCustomGame && CurrentCustom.ManagedProjectId == ManagedGames.Id;
                managedSavesItem.Visible = managedUninstallItem.Visible = managed;
                managedUninstallItem.Enabled = managed && !busy;
            };
        }

        void ManageLibraryLocations()
        {
            using (var dialog = new AsteraDialog
            {
                Text = "Library locations",
                Size = new Size(720, 520),
                StartPosition = FormStartPosition.CenterParent
            }

            )
            {
                var content = new Panel
                {
                    Dock = DockStyle.Fill,
                    Padding = new Padding(18)
                };
                var text = new ScrollText
                {
                    Dock = DockStyle.Fill,
                    ReadOnly = true,
                    Multiline = true,
                    ScrollBars = ScrollBars.Vertical,
                    BackColor = Color.FromArgb(20, 26, 43),
                    ForeColor = Color.White,
                    BorderStyle = BorderStyle.None
                };
                string[] steamRoots = new string[0];
                Action refresh = delegate
                {
                    text.Text = "Astera-managed games\r\n" + ManagedLibrary.Root(store.Settings, store.Root) + "\r\nExisting games stay in their current locations.\r\n\r\nSteam libraries\r\n" + String.Join("\r\n", steamRoots) + "\r\n\r\nitch.io apps\r\n" + (String.IsNullOrEmpty(store.Settings.ItchAppsRoot) ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "itch", "apps") : store.Settings.ItchAppsRoot);
                };
                var actions = new FlowLayoutPanel
                {
                    Dock = DockStyle.Bottom,
                    Height = 90,
                    WrapContents = true
                };
                var astera = Button("Choose Astera library…", delegate
                {
                    try
                    {
                        string root = ChooseManagedLibraryRoot(dialog, ManagedLibrary.Root(store.Settings, store.Root));
                        if (root != null)
                        {
                            store.Settings.ManagedLibraryRoot = root;
                            store.Save();
                            refresh();
                        }
                    }
                    catch (Exception error)
                    {
                        MessageBox.Show(dialog, error.Message, "Game library");
                    }
                });
                var itch = Button("Choose itch.io apps folder…", delegate
                {
                    using (var picker = new ExplorerFolderDialog
                    {
                        Description = "Choose the itch.io apps folder",
                        SelectedPath = store.Settings.ItchAppsRoot
                    }

                    )
                        if (picker.ShowDialog(dialog) == DialogResult.OK)
                        {
                            store.Settings.ItchAppsRoot = picker.SelectedPath;
                            store.Save();
                            refresh();
                        }
                });
                actions.Controls.AddRange(new Control[] { astera, itch });
                content.Controls.Add(text);
                content.Controls.Add(actions);
                dialog.Controls.Add(content);
                refresh();
                dialog.Shown += async delegate
                {
                    steamRoots = await Task.Run(() => PlatformLibraries.Steam(SteamArtwork.Roots()).Select(g => Directory.GetParent(g.Folder).Parent.Parent.FullName).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
                    if (!dialog.IsDisposed)
                        refresh();
                };
                dialog.ShowDialog(this);
            }
        }

        void ConfigureManagedGame()
        {
            var game = CurrentCustom;
            if (game == null)
                return;
            using (var dialog = new AsteraDialog
            {
                Text = "Gacha Nebula + settings",
                Size = new Size(720, 400),
                StartPosition = FormStartPosition.CenterParent
            }

            )
            {
                var content = new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    Padding = new Padding(18),
                    FlowDirection = FlowDirection.TopDown,
                    WrapContents = false
                };
                content.Controls.Add(new Label { Text = "Installation: " + Path.GetDirectoryName(game.Executable), AutoSize = true, MaximumSize = new Size(650, 0), Margin = new Padding(0, 0, 0, 16) });
                content.Controls.Add(new Label { Text = "Saved data is kept when uninstalling. Library location changes apply to future installations.", AutoSize = true, MaximumSize = new Size(650, 0) });
                var row = new FlowLayoutPanel
                {
                    Width = 650,
                    Height = 90,
                    WrapContents = true
                };
                var locations = Button("Library locations", delegate
                {
                    ManageLibraryLocations();
                });
                var saves = Button("Saved data", delegate
                {
                    OpenManagedSaves();
                });
                var remove = Button("Uninstall — keep saves", async delegate
                {
                    dialog.Close();
                    await UninstallManagedGame();
                });
                row.Controls.AddRange(new Control[] { locations, saves, remove });
                content.Controls.Add(row);
                dialog.Controls.Add(content);
                dialog.ShowDialog(this);
            }
        }

        void OpenManagedSaves()
        {
            var game = CurrentCustom;
            if (game == null || game.ManagedProjectId != ManagedGames.Id)
                return;
            RememberManaged(Path.GetDirectoryName(game.Executable), ManagedInstalled(game));
            var paths = store.Settings.ManagedInstalls.Where(r => r != null && r.ProjectId == ManagedGames.Id).SelectMany(r => r.SaveLocations).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            using (var dialog = new AsteraDialog
            {
                Text = "Gacha Nebula + saved data",
                Size = new Size(720, 430),
                StartPosition = FormStartPosition.CenterParent
            }

            )
            {
                var content = new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    Padding = new Padding(18),
                    FlowDirection = FlowDirection.TopDown,
                    WrapContents = false,
                    AutoScroll = true
                };
                content.Controls.Add(new Label { Text = "These locations are preserved when uninstalling. Saves may not exist until you play.", AutoSize = true, MaximumSize = new Size(650, 0) });
                foreach (string path in paths)
                {
                    string selected = path;
                    var row = new FlowLayoutPanel
                    {
                        Width = 650,
                        Height = 64,
                        WrapContents = false
                    };
                    row.Controls.Add(new Label { Text = selected, Width = 500, Height = 60 });
                    var open = new AsteraButton
                    {
                        Text = "Open",
                        Enabled = Directory.Exists(ManagedGames.DiskPath(selected))
                    };
                    open.Click += delegate
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(selected) { UseShellExecute = true });
                    };
                    row.Controls.Add(open);
                    content.Controls.Add(row);
                }

                dialog.Controls.Add(content);
                dialog.ShowDialog(this);
            }
        }

        async Task UninstallManagedGame()
        {
            var game = CurrentCustom;
            if (busy || game == null || game.ManagedProjectId != ManagedGames.Id)
                return;
            string root = Path.GetDirectoryName(game.Executable);
            if (MessageBox.Show(this, "Uninstall Gacha Nebula +?\r\n\r\nGame files installed by Astera will be removed. Saves, Recovery backups, personal exports and modified files will be kept. You can reinstall from this Library entry.", "Uninstall — keep saves", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            ManagedUninstallResult result = null;
            await Work(delegate
            {
                ReportWorkProgress("Removing game files; keeping saves", 0, 0);
                result = ManagedLibrary.Uninstall(root, cancellation.Token, ReportWorkProgress, () => RunningGames.Check(game, null));
            }, false, "custom:" + game.Id);
            bool installed = ManagedInstalled(game);
            RememberManaged(root, installed);
            ReloadGames(game);
            RefreshView();
            if (result != null)
                LogActivity("custom:" + game.Id, result.BusyFiles.Count == 0 ? "Gacha Nebula + uninstalled. Your saved data is kept for reinstalling." : "Saved data kept. Some game files were in use and remain; close the game and retry removal.");
        }
    }
}
