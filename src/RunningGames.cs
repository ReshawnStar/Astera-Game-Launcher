using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Astera
{
    static class RunningGames
    {
        internal static bool Check(CustomGame game, string steamId)
        {
            if (game.Platform == "minecraft")
                return PlaytimeProbe.Running(game);
            if (PlatformLibraries.Id(steamId))
                try
                {
                    object flag = Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam\Apps\" + steamId, "Running", null);
                    if (flag != null && Convert.ToInt32(flag) != 0)
                        return true;
                }
                catch (System.Security.SecurityException)
                {
                }
                catch (FormatException)
                {
                }

            bool managed = game.ManagedProjectId == ManagedGames.Id;
            bool folder = managed || !String.IsNullOrEmpty(game.Platform);
            string target = Path.GetFullPath(managed ? Path.GetDirectoryName(game.Executable) : game.Executable);
            string folderPrefix = target.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            using (var snapshot = folder ? ProcessSnapshot.All() : ProcessSnapshot.Named(Path.GetFileNameWithoutExtension(target)))
            {
                foreach (var process in snapshot.Items)
                {
                    try
                    {
                        string processPath = Path.GetFullPath(process.MainModule.FileName);
                        bool matches = folder ? processPath.StartsWith(folderPrefix, StringComparison.OrdinalIgnoreCase) : processPath.Equals(target, StringComparison.OrdinalIgnoreCase);
                        if (matches)
                        {
                            return true;
                        }
                    }
                    catch (System.ComponentModel.Win32Exception)
                    {
                        // Protected processes are unrelated to this game's folder.
                    }
                    catch (InvalidOperationException)
                    {
                        // The process may have exited during the snapshot.
                    }
                    catch (NotSupportedException)
                    {
                    }
                }
            }

            return false;
        }
    }

    sealed partial class LibraryForm
    {
        readonly Timer runningClock = new Timer
        {
            Interval = 2500
        };
        readonly Dictionary<string, bool> runningGames = new Dictionary<string, bool>();
        readonly Dictionary<string, DateTime> startingGames = new Dictionary<string, DateTime>();
        bool pollingRunning;
        CustomGame SelectedRunningGame
        {
            get
            {
                if (GameKind == MinecraftGame && !String.IsNullOrWhiteSpace(store.Settings.Profile))
                    return new CustomGame
                    {
                        Id = "builtin-minecraft:" + MC2Pack.Engine.Hash(System.Text.Encoding.UTF8.GetBytes(store.Settings.Profile.ToLowerInvariant())).Substring(0, 16),
                        Executable = store.Settings.Profile,
                        Platform = "minecraft"
                    };
                if (IsRoblox)
                    return null;
                if (IsCustomGame)
                    return CurrentCustom;
                if (GameKind == StarDashGame && !String.IsNullOrWhiteSpace(store.Settings.StarDashExecutable))
                    return new CustomGame
                    {
                        Id = "builtin-stardash",
                        Executable = store.Settings.StarDashExecutable
                    };
                if (IsMultiverse && !String.IsNullOrWhiteSpace(store.Settings.FtlDirectory))
                    return new CustomGame
                    {
                        Id = "builtin-ftl",
                        Executable = store.Settings.FtlDirectory,
                        Platform = "folder",
                        PlatformId = FtlDiscovery.Steam(store.Settings.FtlDirectory) ? "212680" : ""
                    };
                return null;
            }
        }

        void ApplyBuiltinRunning()
        {
            var game = SelectedRunningGame;
            if (game == null)
                return;
            if (GameRunning(game))
            {
                play.Text = "RUNNING";
                status.Text = "Running";
                play.Enabled = false;
            }
            else if (GameStarting(game))
            {
                play.Text = "STARTING…";
                status.Text = "Starting…";
                play.Enabled = false;
            }
        }

        async Task<bool> GuardBuiltinLaunch()
        {
            var game = SelectedRunningGame;
            if (game == null)
                return true;
            if (GameRunning(game) || GameStarting(game))
            {
                RefreshView();
                return false;
            }

            startingGames[game.Id] = DateTime.UtcNow.AddSeconds(10);
            RefreshView();
            try
            {
                bool running = await Task.Run(() => RunningGames.Check(game, game.PlatformId));
                runningGames[game.Id] = running;
                if (running)
                    startingGames.Remove(game.Id);
                if (!IsDisposed)
                    RefreshView();
                return !running;
            }
            catch (Exception error)
            {
                startingGames.Remove(game.Id);
                AsteraDiagnostics.Write("Pre-launch running-state check unavailable", new IOException(error.GetType().Name));
                return true;
            }
        }

        bool GameRunning(CustomGame game)
        {
            bool value;
            return game != null && runningGames.TryGetValue(game.Id, out value) && value;
        }

        bool GameStarting(CustomGame game)
        {
            DateTime until;
            return game != null && startingGames.TryGetValue(game.Id, out until) && DateTime.UtcNow < until;
        }

        internal static bool RunningNeedsRefresh(bool known, bool before, bool after, bool startingExpired)
        {
            return !known || before != after || startingExpired;
        }

        async void PollRunning()
        {
            if (previewMode || pollingRunning || SelectedRunningGame == null)
                return;
            pollingRunning = true;
            var game = SelectedRunningGame;
            var art = CustomSteam(game);
            try
            {
                bool value = await Task.Run(() => RunningGames.Check(game, art == null ? game.PlatformId : art.AppId));
                if (!IsDisposed)
                {
                    bool before;
                    bool known = runningGames.TryGetValue(game.Id, out before), expired = startingGames.ContainsKey(game.Id) && !GameStarting(game);
                    runningGames[game.Id] = value;
                    if (value || expired)
                        startingGames.Remove(game.Id);
                    if (game.Id == "builtin-ftl" && (before && !value || expired && !value))
                        CaptureFtlReports();
                    if (RunningNeedsRefresh(known, before, value, expired) && SelectedRunningGame != null && SelectedRunningGame.Id == game.Id)
                        RefreshView();
                }
            }
            catch (Exception error)
            {
                AsteraDiagnostics.Write("Game running-state check unavailable", new IOException(error.GetType().Name));
            }
            finally
            {
                pollingRunning = false;
            }
        }

        void StartRunningChecks()
        {
            Shown += delegate
            {
                if (!previewMode)
                {
                    runningClock.Start();
                    PollRunning();
                }
            };
            runningClock.Tick += delegate
            {
                PollRunning();
            };
            Disposed += delegate
            {
                runningClock.Dispose();
            };
        }
    }
}
