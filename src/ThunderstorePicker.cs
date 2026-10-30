using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Astera
{
    sealed partial class LibraryForm
    {
        void PickThunderstoreGame()
        {
            if (busy)
                return;
            using (var dialog = new AsteraDialog
            {
                Text = "Add a Thunderstore game",
                Size = new Size(650, 490),
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
                var status = new Label
                {
                    Text = "Finding games and profiles…",
                    Dock = DockStyle.Top,
                    Height = 40,
                    Padding = new Padding(12)
                };
                var controls = new FlowLayoutPanel
                {
                    Dock = DockStyle.Bottom,
                    Height = 58,
                    Padding = new Padding(10)
                };
                var found = new List<InstalledGame>();
                var add = Button("Add game", delegate
                {
                    int index = list.SelectedIndex;
                    if (index < 0 || index >= found.Count)
                        return;
                    var item = found[index];
                    var game = store.Settings.CustomGames.FirstOrDefault(g => g.Platform == "steam" && g.PlatformId == item.Id);
                    if (game == null)
                    {
                        game = new CustomGame
                        {
                            Name = item.Name,
                            Platform = "steam",
                            PlatformId = item.Id,
                            Executable = item.Folder
                        };
                        store.Settings.CustomGames.Add(game);
                    }

                    ThunderstoreProfiles.Merge(game, ThunderstoreProfiles.Discover(ThunderstoreProfiles.Game(item.Id), ThunderstoreProfiles.Roots(store.Settings.ThunderstoreRoots)));
                    store.Save();
                    ReloadGames(game);
                    RefreshView();
                    dialog.DialogResult = DialogResult.OK;
                });
                controls.Controls.Add(add);
                dialog.Controls.Add(list);
                dialog.Controls.Add(status);
                dialog.Controls.Add(controls);
                dialog.Shown += async delegate
                {
                    try
                    {
                        found = await Task.Run(() => PlatformLibraries.Steam(SteamArtwork.Roots()).Where(g => ThunderstoreProfiles.Game(g.Id) != null).ToList());
                        if (dialog.IsDisposed)
                            return;
                        foreach (var game in found)
                            list.Items.Add(game.Name);
                        status.Text = found.Count == 0 ? "No supported Steam games found. Add the game through Steam library first." : "Choose a game. Its Thunderstore and r2modman profiles appear inside it.";
                    }
                    catch (Exception error)
                    {
                        if (!dialog.IsDisposed)
                            status.Text = error.Message;
                    }
                };
                list.DoubleClick += delegate
                {
                    add.PerformClick();
                };
                dialog.ShowDialog(this);
            }
        }
    }
}
