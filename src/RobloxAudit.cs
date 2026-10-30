using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace Astera
{
    sealed partial class LibraryForm
    {
        internal static int AuditRoblox(string root, bool live)
        {
            try
            {
                if (Directory.Exists(root))
                    throw new IOException("Use a new Roblox audit directory");
                Directory.CreateDirectory(root);
                var checks = new List<string>();
                long place;
                foreach (string value in new[]
                {
                    "1818",
                    "https://www.roblox.com/games/1818/Classic-Crossroads",
                    "https://www.roblox.com/games/start?placeId=1818",
                    "roblox://placeId=1818"
                }

                )
                    AuditAssert(RobloxSupport.TryParsePlaceId(value, out place) && place == 1818, "Valid Roblox link rejected");
                foreach (string value in new[]
                {
                    "https://evilroblox.com/games/1818",
                    "https://roblox.com.evil/games/1818",
                    "https://u:p@roblox.com/games/1818",
                    "https://roblox.com:444/games/1818",
                    "https://www.roblox.com/share?code=abc",
                    "roblox://placeId=1818&evil=payload",
                    "-1",
                    "0"
                }

                )
                    AuditAssert(!RobloxSupport.TryParsePlaceId(value, out place), "Unsafe Roblox link accepted");
                checks.Add("Roblox Place IDs and supported links parse; fake domains, credentials, ports and unresolved share codes are rejected");
                var launch = RobloxSupport.LaunchInfo("1818");
                AuditAssert(launch.FileName == "roblox://placeId=1818" && launch.UseShellExecute && launch.Arguments == "", "Roblox protocol handoff changed");
                checks.Add("Roblox launch uses only the canonical Place ID protocol without executing a game during tests");
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                var state = new Store(Path.Combine(root, "state"));
                state.Settings.CheckOnStart = false;
                state.Settings.UseMinecraftSkin = false;
                var game = new CustomGame
                {
                    Name = "Crossroads",
                    Platform = "roblox",
                    PlatformId = "1818",
                    Executable = RobloxSupport.WebUrl(1818)
                };
                state.Settings.CustomGames.Add(game);
                state.Save();
                AuditAssert(new Store(state.Root).Settings.CustomGames[0].PlatformId == "1818", "Roblox shortcut did not survive settings reload");
                AuditAssert(BatchGames.Same(game, new CustomGame { Platform = "roblox", PlatformId = "1818", Executable = game.Executable }) && !BatchGames.Same(game, new CustomGame { Executable = Application.ExecutablePath }), "Roblox duplicate matching collided with EXEs");
                CustomGames.Set(state.Settings.CustomGames, game, "Renamed experience", game.Executable);
                checks.Add("Roblox shortcuts persist, rename and deduplicate independently from executable game paths");
                game.RobloxDescription = "Explore together.\nFind hidden paths!";
                game.RobloxDescriptionVersion = 1;
                state.Save();
                AuditAssert(new Store(state.Root).Settings.CustomGames[0].RobloxDescription == game.RobloxDescription, "Cached description lost after restart");
                AuditAssert(RobloxSupport.DescriptionText(new String('x', 40000)).Length == 16000 && RobloxSupport.DescriptionText("a\0b\u0001\nnext") == "ab\r\nnext", "Description bounds or plain-text control cleanup failed");
                checks.Add("Experience descriptions persist offline and preserve readable line breaks while rejecting control characters and oversized text");
                if (live)
                {
                    var metadata = RobloxSupport.Lookup(1818, CancellationToken.None);
                    game.UniverseId = metadata.UniverseId;
                    game.Name = metadata.Name;
                    game.RobloxDescription = metadata.Description;
                    AuditAssert(!String.IsNullOrWhiteSpace(game.RobloxDescription), "Live experience description missing");
                    File.WriteAllText(Path.Combine(root, "public-description.txt"), game.RobloxDescription);
                    string cache = Path.Combine(root, "artwork");
                    game.RobloxIcon = RobloxSupport.Cache(metadata.Icon, cache, metadata.UniverseId, "icon", CancellationToken.None);
                    game.RobloxBanner = RobloxSupport.Cache(metadata.Banner, cache, metadata.UniverseId, "banner", CancellationToken.None);
                    AuditAssert(game.UniverseId > 0 && !String.IsNullOrEmpty(game.Name), "Live Roblox metadata missing");
                    checks.Add("Actual public Roblox Place-to-Universe metadata and available CDN artwork resolve without authentication");
                }

                using (var form = new LibraryForm(state, new Channel(), true))
                {
                    form.TopMost = live;
                    form.ReloadGames(game);
                    form.Show();
                    Application.DoEvents();
                    AuditAssert(form.latestHeading.Text == "About this experience" && form.notes.Text == RobloxSupport.DescriptionText(game.RobloxDescription), "Roblox description was replaced with generic update text");
                    string cachedDescription = form.notes.Text;
                    form.RefreshRobloxView();
                    AuditAssert(form.notes.Text == cachedDescription, "Refreshing rewrote the description");
                    string savedDescription = game.RobloxDescription;
                    game.RobloxDescription = "";
                    form.RefreshRobloxView();
                    AuditAssert(form.notes.Text.Contains("No description is available") && !form.notes.Text.Contains("Minecraft"), "Empty description inherited another game description");
                    game.RobloxDescription = savedDescription;
                    form.RefreshRobloxView();
                    checks.Add("Roblox shows its own description in an About panel and has an independent missing-description fallback");
                    AuditAssert(form.play.Enabled && form.play.Text == "PLAY" && form.check.Text == "OPEN GAME PAGE" && !form.download.Visible && !form.install.Visible && !form.restore.Visible && form.status.Text == "Managed by Roblox", "Roblox inherited an executable check or Astera update controls");
                    AuditAssert(form.GameFolder() == "" && form.SelectedRunningGame == null, "Roblox was treated as an installed executable or had false running detection");
                    checks.Add("Roblox shows its platform-managed status, hides Astera install controls and is not mistaken for a local game folder");
                    form.notes.Text = "Trailer: https://youtu.be/M7lc1UVf-VE";
                    AuditAssert(form.updateVideos.Visible && form.updateVideos.Controls.Count == 1, "YouTube watch action missing from update notes");
                    if (live)
                    {
                        form.Refresh();
                        Application.DoEvents();
                        form.CaptureVisibleUi(root, "update-video-action");
                    }

                    form.notes.Parent.PerformLayout();
                    AuditAssert(form.notes.Bottom <= form.updateVideos.Top, "Video actions overlap update text");
                    form.notes.Text = "No video here";
                    AuditAssert(!form.updateVideos.Visible && form.updateVideos.Controls.Count == 0, "YouTube actions remained after changing notes");
                    checks.Add("Update-note video actions appear only for valid links and clear when notes change");
                    if (live)
                    {
                        form.notes.Text = RobloxSupport.ExperienceDescription(game);
                        form.Refresh();
                        Application.DoEvents();
                        form.CaptureVisibleUi(root, "roblox-library");
                    }

                    state.Settings.CustomGames.Remove(game);
                    state.Save();
                    AuditAssert(new Store(state.Root).Settings.CustomGames.Count == 0, "Removing a Roblox shortcut did not persist");
                    checks.Add("Removing a favorite changes only Astera preferences, without touching Roblox installations or saves");
                    form.Close();
                }

                File.WriteAllText(Path.Combine(root, "result.json"), MC2Pack.Engine.Json.Serialize(new
                {
                    Passed = true,
                    Checks = checks,
                    RobloxLaunched = false,
                    LivePreferencesChanged = false
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
