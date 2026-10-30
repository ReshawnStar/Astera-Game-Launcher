using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Astera
{
    sealed partial class LibraryForm
    {
        sealed class MinecraftLaunchNotice
        {
            internal string Profile, Name, GameId;
            internal ProcessStartInfo Command;
            internal DateTime Started;
            internal bool TimedOut, OverlayDismissed, WindowReady;
            internal System.Collections.Generic.HashSet<IntPtr> ExistingWindows;
            internal CurseForgeBackground Background;
            internal LaunchFlavor Flavor;
        }

        MinecraftLaunchNotice minecraftLaunch;
        bool minecraftWindowPolling;
        DateTime nextMinecraftWindowProbe;
        readonly Timer minecraftLaunchClock = new Timer
        {
            Interval = 100
        };
        bool minecraftLaunchClockReady;
        bool MinecraftLaunchVisible
        {
            get
            {
                return minecraftLaunch != null && GameKind == MinecraftGame && String.Equals(store.Settings.Profile, minecraftLaunch.Profile, StringComparison.OrdinalIgnoreCase);
            }
        }

        void BeginMinecraftLaunch(GameProfile profile, ProcessStartInfo command, Action<ProcessStartInfo> dispatch = null)
        {
            StopMinecraftLaunch();
            var game = SelectedRunningGame;
            minecraftLaunch = new MinecraftLaunchNotice
            {
                Profile = profile.Path,
                Name = profile.Name,
                GameId = game == null ? null : game.Id,
                Command = command,
                ExistingWindows = MinecraftWindows.Visible(),
                Started = DateTime.UtcNow,
                Flavor = new LaunchFlavor(store.Root, true)
            };
            minecraftLaunch.Background = new CurseForgeBackground(this, command);
            if (game != null)
                startingGames[game.Id] = DateTime.UtcNow.Add(LaunchPolicy.Timeout);
            if (!minecraftLaunchClockReady)
            {
                minecraftLaunchClockReady = true;
                minecraftLaunchClock.Tick += delegate
                {
                    TickMinecraftLaunch();
                };
                Disposed += delegate
                {
                    minecraftLaunchClock.Dispose();
                };
            }

            nextMinecraftWindowProbe = DateTime.MinValue;
            RefreshView();
            minecraftLaunchClock.Start();
            try
            {
                if (dispatch != null)
                    dispatch(command);
                else
                    using (var process = Process.Start(command))
                    {
                    }
                ;
                TrackLaunchedGame("builtin:0", new CustomGame { Executable = profile.Path, Platform = "minecraft" });
                LogActivity("builtin:0", "Asked CurseForge to start " + profile.Name + ". Waiting for Minecraft.");
            }
            catch
            {
                if (game != null)
                    startingGames.Remove(game.Id);
                StopMinecraftLaunch();
                RefreshView();
                throw;
            }
        }

        async void ProbeMinecraftWindow()
        {
            if (minecraftWindowPolling || minecraftLaunch == null || DateTime.UtcNow < nextMinecraftWindowProbe)
                return;
            var notice = minecraftLaunch;
            var game = new CustomGame
            {
                Executable = notice.Profile,
                Platform = "minecraft"
            };
            minecraftWindowPolling = true;
            nextMinecraftWindowProbe = DateTime.UtcNow.Add(LaunchPolicy.WindowProbeInterval);
            try
            {
                bool ready = await System.Threading.Tasks.Task.Run(() => MinecraftWindows.Ready(game, notice.ExistingWindows));
                if (!IsDisposed && Object.ReferenceEquals(minecraftLaunch, notice))
                    notice.WindowReady = ready;
            }
            catch (Exception error)
            {
                nextMinecraftWindowProbe = DateTime.UtcNow.AddSeconds(5);
                AsteraDiagnostics.Write("Minecraft window check unavailable; launch tracking continues", new IOException(error.GetType().Name));
            }
            finally
            {
                minecraftWindowPolling = false;
            }
        }

        void TickMinecraftLaunch()
        {
            if (minecraftLaunch == null)
            {
                minecraftLaunchClock.Stop();
                return;
            }

            minecraftLaunch.Background.Pulse(this);
            if (!previewMode)
                ProbeMinecraftWindow();
            var game = SelectedRunningGame;
            if (game != null && game.Id == minecraftLaunch.GameId && GameRunning(game) && minecraftLaunch.WindowReady)
            {
                LogActivity("builtin:0", "Minecraft window opened for " + minecraftLaunch.Name + ".");
                StopMinecraftLaunch();
                RefreshView();
                return;
            }

            if (DateTime.UtcNow - minecraftLaunch.Started >= LaunchPolicy.Timeout)
            {
                minecraftLaunch.TimedOut = true;
                minecraftLaunch.Background.Stop();
                if (minecraftLaunch.GameId != null)
                    startingGames.Remove(minecraftLaunch.GameId);
                minecraftLaunchClock.Stop();
                LogActivity("builtin:0", "Still waiting for Minecraft. CurseForge may need attention.");
                RefreshView();
                return;
            }

            UpdateWorkProgress();
        }

        void StopMinecraftLaunch()
        {
            minecraftLaunchClock.Stop();
            if (minecraftLaunch != null)
                minecraftLaunch.Background.Stop();
            minecraftLaunch = null;
        }

        void ShowMinecraftLaunchManager()
        {
            if (minecraftLaunch == null)
                return;
            minecraftLaunch.Background.Stop();
            var command = minecraftLaunch.Command;
            if (!command.UseShellExecute && Path.GetFileName(command.FileName).Equals("OverwolfLauncher.exe", StringComparison.OrdinalIgnoreCase))
            {
                using (var process = Process.Start(new ProcessStartInfo(command.FileName) { UseShellExecute = false, WorkingDirectory = command.WorkingDirectory, Arguments = "-launchapp " + CurseForgeLaunch.AppId + " -from-startmenu" }))
                {
                }
                ;
            }
            else
                using (var process = Process.Start(new ProcessStartInfo(minecraftLaunch.Profile) { UseShellExecute = true }))
                {
                }
            ;
        }

        void PresentMinecraftLaunch()
        {
            bool timedOut = minecraftLaunch.TimedOut;
            workPanel.Visible = true;
            releaseLabel.Visible = false;
            workHeartbeat.Stop();
            if (gameBody != null)
                gameBody.RowStyles[3].Height = 80;
            workMeter.Fraction = timedOut || launchScreen != null ? 0 : -1;
            workLabel.AutoEllipsis = true;
            workLabel.Text = timedOut ? "Minecraft hasn't started yet. Open CurseForge to check." : (DateTime.UtcNow - minecraftLaunch.Started < TimeSpan.FromSeconds(2) ? "Opening CurseForge in the background…" : "Preparing Minecraft…") + " · " + minecraftLaunch.Name + " · " + (DateTime.UtcNow - minecraftLaunch.Started).ToString(@"mm\:ss");
            workFlavor.Visible = true;
            workFlavor.Text = timedOut ? "CurseForge may need attention. Open its window to check." : minecraftLaunch.Flavor.At(minecraftLaunch.Started, store.Settings.StartupFlavor);
            workCancel.Text = workCancel.AccessibleName = "Open launcher";
            workCancel.Visible = workCancel.Enabled = true;
            workCancel.Parent.Width = 145;
            workCancel.Width = 145;
        }
    }
}
