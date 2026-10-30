using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using MC2Pack;

namespace Astera
{
    sealed partial class LibraryForm
    {
        sealed class TrackedProcess : Process
        {
            internal int Releases;
            protected override void Dispose(bool disposing)
            {
                if (disposing)
                    Releases++;
                base.Dispose(disposing);
            }
        }

        static bool ReturnBeforeEndOfSnapshot(ProcessSnapshot snapshot)
        {
            using (snapshot)
            {
                return snapshot.Items.Length > 0;
            }
        }

        internal static int AuditRuntimeHealth(string root)
        {
            bool previousEffects = UiEffects.Enabled;
            try
            {
                root = Path.GetFullPath(root);
                if (Directory.Exists(root))
                    throw new IOException("Use a fresh runtime health directory");
                Directory.CreateDirectory(root);
                var checks = new List<string>();
                var early = new[]
                {
                    new TrackedProcess(),
                    new TrackedProcess(),
                    new TrackedProcess()
                };
                AuditAssert(ReturnBeforeEndOfSnapshot(new ProcessSnapshot(early)) && early.All(p => p.Releases == 1), "Early process search did not release the whole snapshot");
                var failed = new[]
                {
                    new TrackedProcess(),
                    new TrackedProcess()
                };
                try
                {
                    using (var snapshot = new ProcessSnapshot(failed))
                    {
                        throw new IOException("Expected fixture failure");
                    }
                }
                catch (IOException)
                {
                }

                AuditAssert(failed.All(p => p.Releases == 1), "Failed process search retained wrappers");
                var twice = new TrackedProcess();
                var owned = new ProcessSnapshot(new[] { twice });
                owned.Dispose();
                owned.Dispose();
                AuditAssert(twice.Releases == 1, "Snapshot disposed wrappers twice");
                checks.Add("Process snapshots release every wrapper on early return, failure and repeated disposal");
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                UiEffects.Enabled = true;
                string gif = Path.Combine(root, "animated.gif");
                File.WriteAllBytes(gif, Convert.FromBase64String("R0lGODlhEAAQAIEAAP8AAAAAAAAAAAAAACH/C05FVFNDQVBFMi4wAwEAAAAh+QQACgAAACwAAAAAEAAQAAAIHQABCBxIsKDBgwgTKlzIsKHDhxAjSpxIsaLFgQEBACH5BAEKAAEALAAAAAAQABAAgQAA/wAAAAAAAAAAAAgdAAEIHEiwoMGDCBMqXMiwocOHECNKnEixosWBAQEAOw=="));
                var store = new Store(Path.Combine(root, "state"));
                store.Settings.CheckOnStart = store.Settings.CheckAppOnStart = store.Settings.UseMinecraftSkin = store.Settings.KeepInTray = false;
                var game = new CustomGame
                {
                    Name = "Animation fixture",
                    Executable = Path.Combine(root, "fixture.exe"),
                    Background = gif,
                    Icon = gif
                };
                File.WriteAllText(game.Executable, "Not executed");
                store.Settings.CustomGames.Add(game);
                long visibleRenders, coveredRenders, resumedRenders;
                using (var form = new LibraryForm(store, new Channel(), true))
                {
                    form.Location = new Point(-30000, -30000);
                    form.ReloadGames(game);
                    form.Show();
                    MotionAudit.Pump(1600);
                    form.RegisterIconAnimation(gif);
                    form.banner.BeginPerformanceMeasurement();
                    MotionAudit.Pump(350);
                    visibleRenders = form.banner.MeasuredRenders;
                    AuditAssert(visibleRenders > 0 && form.iconAnimationClock.Enabled, "Visible fixture animations did not start: renders=" + visibleRenders + ", icon=" + form.iconAnimationClock.Enabled + ", banner=" + form.banner.ArtworkPlayback);
                    form.BeginGameLaunchWait("custom:" + game.Id, game, true);
                    long before = form.banner.MeasuredRenders;
                    MotionAudit.Pump(350);
                    coveredRenders = form.banner.MeasuredRenders - before;
                    AuditAssert(form.launchScreen != null && form.banner.LaunchAnimationPaused && !form.iconAnimationClock.Enabled && coveredRenders == 0, "Hidden artwork still renders behind the launch cover");
                    checks.Add("Opaque launch cover pauses banner rendering and sidebar GIF timers without removing the loading screen");
                    form.LeaveLaunchScreen();
                    before = form.banner.MeasuredRenders;
                    MotionAudit.Pump(350);
                    resumedRenders = form.banner.MeasuredRenders - before;
                    AuditAssert(!form.banner.LaunchAnimationPaused && form.iconAnimationClock.Enabled && resumedRenders > 0, "Returning to library did not resume artwork");
                    checks.Add("Returning to Library resumes visible artwork and icon playback");
                    form.banner.EndPerformanceMeasurement();
                    form.Close();
                }

                File.WriteAllText(Path.Combine(root, "result.json"), Engine.Json.Serialize(new
                {
                    Passed = true,
                    Checks = checks,
                    VisibleRenders = visibleRenders,
                    CoveredRenders = coveredRenders,
                    ResumedRenders = resumedRenders,
                    SampleMilliseconds = 350,
                    GamesLaunched = false,
                    NetworkRequests = false,
                    LivePreferencesChanged = false,
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
            finally
            {
                UiEffects.Enabled = previousEffects;
            }
        }
    }
}
