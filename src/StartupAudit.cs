using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using MC2Pack;

namespace Astera
{
    sealed partial class LibraryForm
    {
        internal static int AuditStartup(string root)
        {
            bool effects = UiEffects.Enabled;
            try
            {
                if (Directory.Exists(root))
                    throw new IOException("Use a fresh startup audit");
                Directory.CreateDirectory(root);
                AsteraDiagnostics.Initialize(root);
                var checks = new List<string>();
                var pool = StartupFlavorPool.Load(root);
                AuditAssert(pool.Count == 230, "Bundled flavor library lost lines");
                var seen = new HashSet<string>();
                string previous = null;
                for (int i = 0; i < 230; i++)
                {
                    string line = pool.Next();
                    AuditAssert(line != previous && seen.Add(line), "Flavor repeated before exhausting the library");
                    previous = line;
                }

                AuditAssert(pool.Next() != previous, "Flavor repeated at the shuffle boundary");
                checks.Add("All 230 supplied lines load and shuffle without immediate or within-launch repeats");
                File.WriteAllText(Path.Combine(root, "startup-lines.json"), "invalid override");
                AuditAssert(StartupFlavorPool.Load(root).Count == 230, "Invalid override did not fall back to bundled lines");
                checks.Add("Invalid optional flavor overrides fall back to the bounded embedded library");
                AuditAssert(new Preferences().StartupFlavor && Engine.Json.Deserialize<Preferences>("{}").StartupFlavor && !Engine.Json.Deserialize<Preferences>("{\"StartupFlavor\":false}").StartupFlavor, "Flavor setting default/opt-out changed");
                checks.Add("Playful startup messages default on; saved opt-out remains off");
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                var store = new Store(Path.Combine(root, "state"));
                store.Settings.CheckOnStart = false;
                store.Settings.CheckAppOnStart = false;
                store.Settings.KeepInTray = false;
                store.Settings.UseMinecraftSkin = false;
                store.Settings.StartupFlavor = true;
                store.Settings.AnimatedEffects = true;
                using (var form = new LibraryForm(store, new Channel(), true))
                using (var timer = new System.Windows.Forms.Timer
                {
                    Interval = 80
                }

                )
                {
                    form.BuildStartupPresentation();
                    form.TopMost = true;
                    form.StartPosition = FormStartPosition.Manual;
                    form.Location = Screen.PrimaryScreen.WorkingArea.Location;
                    var gate = new TaskCompletionSource<bool>();
                    Task preparation = null;
                    var overall = Stopwatch.StartNew();
                    var phase = Stopwatch.StartNew();
                    int stage = 0, code = 1;
                    string firstFlavor = null, pausedFlavor = null;
                    StartupScreen cover = null;
                    form.Shown += delegate
                    {
                        preparation = form.PrepareStartup(async delegate (Action<string> report)
                        {
                            report("Checking updates…");
                            await gate.Task;
                            report("Checks finished");
                        });
                        timer.Start();
                    };
                    timer.Tick += delegate
                    {
                        try
                        {
                            if (overall.ElapsedMilliseconds > 14000)
                                throw new IOException("Startup test timed out");
                            if (stage == 0 && phase.ElapsedMilliseconds > 750)
                            {
                                cover = form.startupScreen;
                                AuditAssert(cover != null && cover.PaintCount > 0 && cover.Bounds == form.ClientRectangle, "Startup cover did not paint over the entire window");
                                AuditAssert(cover.StatusText == "Checking updates…" && !preparation.IsCompleted, "Startup exposed the library before checks finished");
                                firstFlavor = cover.FlavorText;
                                using (var sample = CaptureLibrary(cover, new Rectangle(18, 110, 100, 24)))
                                    for (int y = 0; y < sample.Height; y += 4)
                                        for (int x = 0; x < sample.Width; x += 4)
                                            AuditAssert(sample.GetPixel(x, y).ToArgb() == cover.BackColor.ToArgb(), "Library controls painted through the startup cover");
                                form.CaptureVisibleUi(root, "startup-wide");
                                form.Size = new Size(1100, 760);
                                phase.Restart();
                                stage = 1;
                                return;
                            }

                            if (stage == 1 && phase.ElapsedMilliseconds > 250)
                            {
                                AuditAssert(cover.Bounds == form.ClientRectangle && cover.StatusText == "Checking updates…", "Resize exposed library controls or changed progress");
                                form.CaptureVisibleUi(root, "startup-compact");
                                UiEffects.Enabled = false;
                                pausedFlavor = cover.FlavorText;
                                AuditAssert(!cover.AnimationRunning, "Reduced motion kept the loading clock active");
                                phase.Restart();
                                stage = 2;
                                return;
                            }

                            if (stage == 2 && phase.ElapsedMilliseconds > 400)
                            {
                                AuditAssert(cover.FlavorText == pausedFlavor, "Reduced motion kept changing flavor text");
                                UiEffects.Enabled = true;
                                form.Hide();
                                AuditAssert(!cover.AnimationRunning, "Hidden startup kept animation running");
                                form.Show();
                                form.WindowState = FormWindowState.Minimized;
                                AuditAssert(!cover.AnimationRunning, "Minimized startup kept animation running");
                                form.WindowState = FormWindowState.Normal;
                                AuditAssert(cover.AnimationRunning, "Restored startup did not resume animation");
                                checks.Add("Startup covers native library controls, resizes correctly, and pauses for reduced motion/hide/minimize");
                                stage = 3;
                                return;
                            }

                            if (stage == 3 && overall.ElapsedMilliseconds > 3900)
                            {
                                AuditAssert(cover.FlavorText != firstFlavor && cover.StatusText == "Checking updates…", "Cosmetic messages changed real status or failed to rotate");
                                checks.Add("Flavor rotates separately from real progress while a slow check remains pending");
                                gate.SetResult(true);
                                stage = 4;
                                return;
                            }

                            if (stage == 4 && preparation.IsCompleted)
                            {
                                AuditAssert(preparation.Status == TaskStatus.RanToCompletion && form.startupScreen == null && cover.IsDisposed, "Completed startup left an overlay or timer behind");
                                form.CaptureVisibleUi(root, "startup-library-ready");
                                checks.Add("Finished checks release the prepared library and dispose the startup surface");
                                using (var plain = new StartupScreen(StartupFlavorPool.Load(store.Root), false))
                                {
                                    AuditAssert(plain.FlavorText == "", "Disabled playful messages still rendered text");
                                }

                                checks.Add("Playful-message opt-out leaves only the real status and loader");
                                File.WriteAllText(Path.Combine(root, "result.json"), Engine.Json.Serialize(new
                                {
                                    Passed = true,
                                    Checks = checks,
                                    RealNetworkRequests = false,
                                    LivePreferencesChanged = false
                                }));
                                code = 0;
                                timer.Stop();
                                form.Close();
                            }
                        }
                        catch (Exception error)
                        {
                            timer.Stop();
                            gate.TrySetCanceled();
                            File.WriteAllText(Path.Combine(root, "failure.txt"), error.ToString());
                            form.Close();
                        }
                    };
                    Application.Run(form);
                    if (code == 0)
                    {
                        using (var offline = new LibraryForm(store, new Channel(), true))
                        {
                            offline.ShowInTaskbar = false;
                            offline.StartPosition = FormStartPosition.Manual;
                            offline.Location = new Point(-30000, -30000);
                            offline.Show();
                            var task = offline.PrepareStartup(async delegate (Action<string> report)
                            {
                                report("Checking updates…");
                                await Task.Delay(120);
                                throw new IOException("offline startup fixture");
                            });
                            var deadline = Stopwatch.StartNew();
                            while (!task.IsCompleted && deadline.ElapsedMilliseconds < 4000)
                                MotionAudit.Pump(10);
                            AuditAssert(task.Status == TaskStatus.RanToCompletion && offline.startupScreen == null, "Failed update check trapped startup behind its cover");
                            offline.Close();
                        }

                        checks.Add("A failed startup update check logs the error and opens the library with available content");
                        File.WriteAllText(Path.Combine(root, "result.json"), Engine.Json.Serialize(new
                        {
                            Passed = true,
                            Checks = checks,
                            RealNetworkRequests = false,
                            LivePreferencesChanged = false
                        }));
                    }

                    return code;
                }
            }
            catch (Exception error)
            {
                Directory.CreateDirectory(root);
                File.WriteAllText(Path.Combine(root, "failure.txt"), error.ToString());
                return 1;
            }
            finally
            {
                UiEffects.Enabled = effects;
            }
        }
    }
}
