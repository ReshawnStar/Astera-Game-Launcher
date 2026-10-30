using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using MC2Pack;

namespace Astera
{
    public sealed class PlaytimeData
    {
        public Dictionary<string, double> Seconds = new Dictionary<string, double>();
        public Dictionary<string, long> LastPlayed = new Dictionary<string, long>();
    }

    sealed class PlaytimeLedger
    {
        internal sealed class Session
        {
            internal string Key, Platform;
            internal CustomGame Game;
            internal double Requested, Last;
            internal bool Confirmed;
        }

        internal readonly Dictionary<string, Session> Active = new Dictionary<string, Session>();
        internal readonly PlaytimeData Data;
        internal PlaytimeLedger(PlaytimeData data)
        {
            Data = data ?? new PlaytimeData();
            if (Data.LastPlayed == null)
                Data.LastPlayed = new Dictionary<string, long>();
            if (Data.Seconds == null)
                Data.Seconds = new Dictionary<string, double>();
            foreach (var key in Data.Seconds.Keys.ToArray())
                if (Double.IsNaN(Data.Seconds[key]) || Double.IsInfinity(Data.Seconds[key]) || Data.Seconds[key] < 0)
                    Data.Seconds.Remove(key);
        }

        internal void Request(string key, CustomGame game, double now)
        {
            if (Active.ContainsKey(key))
                return;
            if (game.Platform == "roblox")
                foreach (var old in Active.Values.Where(s => s.Platform == "roblox").ToArray())
                    Active.Remove(old.Key);
            Active[key] = new Session
            {
                Key = key,
                Game = game,
                Platform = game.Platform,
                Requested = now,
                Last = now
            };
        }

        internal void Observe(Session session, bool running, double now)
        {
            Session current;
            if (!Active.TryGetValue(session.Key, out current) || !Object.ReferenceEquals(current, session))
                return;
            double elapsed = Math.Max(0, now - session.Last);
            session.Last = now;
            if (running)
            {
                if (session.Confirmed && elapsed <= 15)
                {
                    double total;
                    Data.Seconds.TryGetValue(session.Key, out total);
                    Data.Seconds[session.Key] = total + elapsed;
                }

                session.Confirmed = true;
                Data.LastPlayed[session.Key] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            }
            else if (session.Confirmed || now - session.Requested > (session.Platform == "minecraft" ? 600 : 180))
                Active.Remove(session.Key);
        }

        internal double Total(string key)
        {
            double total;
            return key != null && Data.Seconds.TryGetValue(key, out total) ? total : 0;
        }
    }

    static class PlaytimeProbe
    {
        internal static bool Running(CustomGame game)
        {
            if (game.Platform == "roblox")
                return HasProcess("RobloxPlayerBeta") || HasProcess("RobloxPlayer");
            if (game.Platform == "minecraft")
            {
                if (!HasProcess("javaw") && !HasProcess("java"))
                    return false;
                string log = Path.Combine(game.Executable, "logs", "latest.log");
                if (!File.Exists(log))
                    return false;
                try
                {
                    using (var stream = new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.None))
                        return false;
                }
                catch (IOException error)
                {
                    int code = error.HResult & 65535;
                    return code == 32 || code == 33;
                }
                catch (UnauthorizedAccessException)
                {
                    return false;
                }
            }

            return RunningGames.Check(game, game.PlatformId);
        }

        static bool HasProcess(string name)
        {
            return ProcessSnapshot.Exists(name);
        }
    }

    sealed partial class LibraryForm
    {
        readonly System.Windows.Forms.Timer playtimeClock = new System.Windows.Forms.Timer
        {
            Interval = 5000
        };
        readonly Stopwatch playtimeTime = Stopwatch.StartNew();
        PlaytimeLedger playtimeLedger;
        bool playtimePolling;
        double playtimeSaved;
        readonly Label playtimeLabel = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = Muted,
            AutoEllipsis = true,
            Text = "PLAY TIME (EST.)\r\n0 min",
            AccessibleName = "Estimated playtime",
            Font = UiTheme.Text(12)
        };
        readonly Label lastPlayedLabel = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = Muted,
            AutoEllipsis = true,
            Text = "LAST PLAYED\r\nNot yet",
            AccessibleName = "Last played",
            Font = UiTheme.Text(12)
        };
        TableLayoutPanel playtimeStats;
        void BuildPlaytimeStats(FlowLayoutPanel actions)
        {
            playtimeStats = new TableLayoutPanel
            {
                Width = 250,
                Height = 48,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.Transparent,
                Margin = new Padding(10, 3, 0, 0),
                Padding = new Padding(0, 2, 0, 0)
            };
            playtimeStats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));
            playtimeStats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52));
            playtimeStats.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            playtimeStats.Controls.Add(lastPlayedLabel, 0, 0);
            playtimeStats.Controls.Add(playtimeLabel, 1, 0);
            actions.Controls.Add(playtimeStats);
        }

        void LayoutPlaytimeStats()
        {
            if (playtimeStats == null || heroActions == null)
                return;
            int used = heroActions.Controls.Cast<Control>().Where(c => c != playtimeStats && c.Visible).Sum(c => Math.Max(c.MinimumSize.Width, c.GetPreferredSize(Size.Empty).Width) + c.Margin.Horizontal);
            playtimeStats.Width = Math.Min(270, Math.Max(185, heroActions.ClientSize.Width - used - 16));
        }

        void StartPlaytime()
        {
            PlaytimeData data = null;
            string path = Path.Combine(store.Root, "playtime.json");
            try
            {
                if (File.Exists(path))
                {
                    if (new FileInfo(path).Length > 4 * 1024 * 1024)
                        throw new IOException("Playtime file is too large");
                    data = Engine.Json.Deserialize<PlaytimeData>(File.ReadAllText(path));
                }
            }
            catch (Exception error)
            {
                AsteraDiagnostics.Write("Playtime history unavailable; original file retained", new IOException(error.GetType().Name));
                playtimeHistoryUnreadable = true;
            }

            playtimeLedger = new PlaytimeLedger(data);
            playtimeClock.Tick += delegate
            {
                PollPlaytime();
            };
            if (!previewMode)
                playtimeClock.Start();
            Disposed += delegate
            {
                playtimeClock.Dispose();
                SavePlaytime();
            };
        }

        bool playtimeHistoryUnreadable;
        void SavePlaytime()
        {
            if (playtimeLedger == null || playtimeHistoryUnreadable || previewMode)
                return;
            try
            {
                string path = Path.Combine(store.Root, "playtime.json"), next = path + ".next";
                File.WriteAllText(next, Engine.Json.Serialize(playtimeLedger.Data));
                if (File.Exists(path))
                    File.Replace(next, path, null);
                else
                    File.Move(next, path);
                playtimeSaved = playtimeTime.Elapsed.TotalSeconds;
            }
            catch (Exception error)
            {
                AsteraDiagnostics.Write("Playtime history could not be saved", new IOException(error.GetType().Name));
            }
        }

        void TrackLaunchedGame(string key, CustomGame game)
        {
            BeginGameLaunchWait(key, game);
            if (previewMode || !store.Settings.TrackPlaytime || playtimeLedger == null || game == null)
                return;
            var snapshot = new CustomGame
            {
                Id = game.Id,
                Name = game.Name,
                Executable = game.Executable,
                Platform = game.Platform,
                PlatformId = game.PlatformId
            };
            playtimeLedger.Request(key, snapshot, playtimeTime.Elapsed.TotalSeconds);
            PollPlaytime();
        }

        void TrackCustomLaunch(CustomGame game)
        {
            TrackLaunchedGame("custom:" + game.Id, game);
        }

        async void PollPlaytime()
        {
            if (playtimePolling || IsDisposed || playtimeLedger == null)
                return;
            if (!store.Settings.TrackPlaytime)
            {
                playtimeLedger.Active.Clear();
                UpdatePlaytimeLabel();
                return;
            }

            var sessions = playtimeLedger.Active.Values.ToArray();
            if (sessions.Length == 0)
            {
                UpdatePlaytimeLabel();
                return;
            }

            playtimePolling = true;
            try
            {
                var observations = await Task.Run(() => sessions.Select(s =>
                {
                    try
                    {
                        return PlaytimeProbe.Running(s.Game);
                    }
                    catch (Exception)
                    {
                        return false;
                    }
                }).ToArray());
                if (IsDisposed)
                    return;
                double now = playtimeTime.Elapsed.TotalSeconds;
                for (int i = 0; i < sessions.Length; i++)
                    playtimeLedger.Observe(sessions[i], observations[i], now);
                if (now - playtimeSaved >= 30 || playtimeLedger.Active.Count != sessions.Length)
                    SavePlaytime();
                UpdatePlaytimeLabel();
            }
            finally
            {
                playtimePolling = false;
            }
        }

        void UpdatePlaytimeLabel()
        {
            if (playtimeLabel == null || playtimeLedger == null)
                return;
            string key = LibraryKey(games.SelectedItem as LibraryEntry ?? activeLibraryEntry);
            if (playtimeStats != null)
                playtimeStats.Visible = key != null;
            string text = !store.Settings.TrackPlaytime ? "Tracking off" : FormatPlaytime(playtimeLedger.Total(key));
            long stamp;
            string last = key != null && playtimeLedger.Data.LastPlayed.TryGetValue(key, out stamp) ? FormatLastPlayed(stamp) : "Not yet";
            PlaytimeLedger.Session session;
            if (key != null && playtimeLedger.Active.TryGetValue(key, out session) && session.Confirmed)
                last = "Playing now";
            text = "PLAY TIME (EST.)\r\n" + text;
            last = "LAST PLAYED\r\n" + last;
            if (playtimeLabel.Text != text)
                playtimeLabel.Text = text;
            if (lastPlayedLabel.Text != last)
                lastPlayedLabel.Text = last;
        }

        internal static string FormatLastPlayed(long stamp)
        {
            try
            {
                var day = DateTimeOffset.FromUnixTimeSeconds(stamp).LocalDateTime.Date;
                int days = (DateTime.Now.Date - day).Days;
                return days == 0 ? "Today" : days == 1 ? "Yesterday" : day.ToString("d MMM yyyy");
            }
            catch (ArgumentOutOfRangeException)
            {
                return "Not yet";
            }
        }

        internal static string FormatPlaytime(double seconds)
        {
            long minutes = (long)(Math.Max(0, seconds) / 60);
            return minutes < 60 ? minutes + " min" : (minutes / 60) + " hr " + (minutes % 60) + " min";
        }
    }
}
