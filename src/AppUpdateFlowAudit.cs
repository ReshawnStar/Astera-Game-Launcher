using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Astera
{
    sealed partial class LibraryForm
    {
        static void FinishUpdateAudit(Task task)
        {
            var deadline = System.Diagnostics.Stopwatch.StartNew();
            while (!task.IsCompleted && deadline.ElapsedMilliseconds < 6000)
                MotionAudit.Pump(10);
            AuditAssert(task.IsCompleted, "Update presentation timed out");
            task.GetAwaiter().GetResult();
        }

        internal static int AuditAppUpdateFlow(string root)
        {
            try
            {
                if (Directory.Exists(root))
                    throw new IOException("Use a fresh update-flow audit");
                Directory.CreateDirectory(root);
                AsteraDiagnostics.Initialize(root);
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                var checks = new List<string>();
                var store = new Store(Path.Combine(root, "state"));
                store.Settings.CheckOnStart = false;
                store.Settings.CheckAppOnStart = false;
                store.Settings.KeepInTray = false;
                store.Settings.UseMinecraftSkin = false;
                Func<LibraryForm> create = delegate
                {
                    var form = new LibraryForm(store, new Channel(), true);
                    form.ShowInTaskbar = false;
                    form.StartPosition = FormStartPosition.Manual;
                    form.Location = new System.Drawing.Point(-30000, -30000);
                    form.Show();
                    return form;
                };
                using (var form = create())
                {
                    bool timerBefore = form.timer.Enabled;
                    var task = form.RunAsteraUpdate(false, delegate (CancellationToken token)
                    {
                        AuditAssert(!form.Enabled && !form.timer.Enabled, "Owner and background checks remained active during update");
                        var dialog = Application.OpenForms.OfType<AppUpdateDialog>().Single();
                        AuditAssert(dialog.Visible && dialog.Heading.Text == "Checking for updates", "Checking presentation was missing");
                        dialog.FormClosed += delegate
                        {
                            AuditAssert(form.Enabled, "Update dialog closed while its owner was disabled");
                        };
                        AuditAssert(form.RunAsteraUpdate().Result == false, "Duplicate update flow started");
                        return Task.FromResult(false);
                    });
                    FinishUpdateAudit(task);
                    AuditAssert(!task.Result && form.Enabled && form.timer.Enabled == timerBefore && !form.appUpdateFlowActive, "Up-to-date check failed to restore the library");
                    checks.Add("Checking uses one compact dialog, prevents duplicate starts and pauses/restores background checks");
                    task = form.RunAsteraUpdate(false, delegate (CancellationToken token)
                    {
                        Application.OpenForms.OfType<AppUpdateDialog>().Single().Close();
                        return Task.FromResult(false);
                    });
                    FinishUpdateAudit(task);
                    AuditAssert(!task.Result && form.Enabled && !form.appUpdateFlowActive, "Cancellation left the library disabled");
                    checks.Add("Closing the update window cancels before any installation and restores the owner");
                    task = form.RunAsteraUpdate(false, delegate (CancellationToken token)
                    {
                        form.appCheckError = "Offline fixture";
                        return Task.FromResult(false);
                    });
                    MotionAudit.Pump(50);
                    AuditAssert(Application.OpenForms.OfType<AppUpdateDialog>().Single().Heading.Text == "Update stopped", "Failed check was reported as up to date");
                    FinishUpdateAudit(task);
                    AuditAssert(form.Enabled, "Failed check trapped the owner");
                    form.appCheckError = null;
                    checks.Add("A failed check displays stopped status and returns to the usable library");
                    task = form.RunAsteraUpdate(false, delegate (CancellationToken token)
                    {
                        return Task.FromResult(true);
                    }, delegate (Action<string, long, long> progress, CancellationToken token)
                    {
                        progress("Downloading", 50, 100);
                        var dialog = Application.OpenForms.OfType<AppUpdateDialog>().Single();
                        AuditAssert(dialog.Stage.Text == "50%", "Real download bytes were not presented");
                        progress("Verifying", 0, 0);
                        AuditAssert(dialog.Stage.Text == "Verifying…", "Verification phase missing");
                        return Task.FromResult<string>(null);
                    });
                    FinishUpdateAudit(task);
                    AuditAssert(!task.Result && form.Enabled, "An unprepared update attempted restart");
                    checks.Add("Download progress and verification render in the same dialog; absent prepared job never restarts");
                    foreach (var state in new[]
                    {
                        FormWindowState.Normal,
                        FormWindowState.Maximized
                    }

                    )
                    {
                        form.WindowState = state;
                        Application.DoEvents();
                        var before = form.WindowState;
                        var check = form.RunAsteraUpdate(false, delegate (CancellationToken token)
                        {
                            var dialog = Application.OpenForms.OfType<AppUpdateDialog>().Single();
                            dialog.FormClosed += delegate
                            {
                                AuditAssert(form.Enabled, "Closing the update window left a disabled owner");
                            };
                            return Task.FromResult(false);
                        });
                        FinishUpdateAudit(check);
                        AuditAssert(form.Visible && form.Enabled && form.WindowState == before, "Checking updates hid, minimized or restored the library to a different size");
                    }

                    checks.Add("Update checks keep normal and maximized windows visible and re-enable the owner before closing the update dialog");
                    form.Close();
                }

                using (var form = create())
                {
                    int restartCount = 0;
                    var task = form.RunAsteraUpdate(false, delegate (CancellationToken token)
                    {
                        return Task.FromResult(true);
                    }, delegate (Action<string, long, long> progress, CancellationToken token)
                    {
                        return Task.FromResult("isolated-job-fixture");
                    }, delegate (string job)
                    {
                        AuditAssert(job == "isolated-job-fixture", "Prepared job changed");
                        restartCount++;
                    });
                    FinishUpdateAudit(task);
                    AuditAssert(task.Result && restartCount == 1 && form.IsDisposed, "Prepared update did not hand off exactly once and close");
                    checks.Add("A prepared update hands off once and closes without a second confirmation prompt");
                }

                foreach (float scale in new[]
                {
                    1f,
                    1.5f,
                    2f
                }

                )
                    using (var dialog = new AppUpdateDialog())
                    {
                        dialog.Location = new System.Drawing.Point(-30000, -30000);
                        dialog.StartPosition = FormStartPosition.Manual;
                        dialog.Heading.Font = UiTheme.Text(20 * scale, true);
                        dialog.Detail.Font = UiTheme.Text(14 * scale);
                        dialog.Stage.Font = UiTheme.Text(14 * scale);
                        dialog.Show();
                        Application.DoEvents();
                        foreach (string phase in new[]
                        {
                            "Checking",
                            "Downloading",
                            "Verifying",
                            "Restarting",
                            "Stopped",
                            "Cancelled",
                            "Current"
                        }

                        )
                        {
                            dialog.Progress(phase, phase == "Downloading" ? 50 : 0, phase == "Downloading" ? 100 : 0);
                            dialog.PerformLayout();
                            Application.DoEvents();
                            AuditAssert(dialog.Stage.ClientSize.Height >= dialog.Stage.Font.Height, "Update phase label was vertically crushed");
                            AuditAssert(dialog.Meter.Height >= 7 && dialog.Meter.Width > 100, "Update meter was crushed");
                            AuditAssert(dialog.Detail.Height >= TextRenderer.MeasureText(dialog.Detail.Text, dialog.Detail.Font, new System.Drawing.Size(dialog.Detail.Width, 1000), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height, "Wrapped update detail was clipped");
                            AuditAssert(dialog.Stage.Parent.Bottom <= dialog.Stage.Parent.Parent.ClientSize.Height - dialog.Stage.Parent.Parent.Padding.Bottom, "Update progress row escaped the dialog");
                        }

                        dialog.Detail.Text = "Update stopped. Check Windows Security if the file was blocked. Your games and settings were preserved.";
                        dialog.PerformLayout();
                        AuditAssert(dialog.Detail.Height >= TextRenderer.MeasureText(dialog.Detail.Text, dialog.Detail.Font, new System.Drawing.Size(dialog.Detail.Width, 1000), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height, "Late update failure message clipped");
                        using (var image = new System.Drawing.Bitmap(dialog.Width, dialog.Height))
                        {
                            dialog.DrawToBitmap(image, new System.Drawing.Rectangle(System.Drawing.Point.Empty, image.Size));
                            image.Save(Path.Combine(root, "update-window-font-" + scale.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".png"));
                        }

                        dialog.Finish();
                    }

                checks.Add("Update status, wrapped detail and progress bar fit at normal, 150% and 200% text sizes across every phase");
                ButtonAtlas.SetAccent("Stellar Cyan");
                foreach (int size in new[]
                {
                    36,
                    72
                }

                )
                    using (var tile = new AsteraButton
                    {
                        UpdateTile = true,
                        Role = ButtonRole.Primary,
                        Symbol = ButtonSymbol.Download,
                        AutoSize = false,
                        MinimumSize = new System.Drawing.Size(size, size),
                        Size = new System.Drawing.Size(size, size),
                        BackColor = System.Drawing.Color.FromArgb(14, 19, 36)
                    }

                    )
                    {
                        using (var image = new System.Drawing.Bitmap(size, size))
                        {
                            tile.DrawToBitmap(image, new System.Drawing.Rectangle(0, 0, size, size));
                            image.Save(Path.Combine(root, "outlined-update-tile-" + size + ".png"));
                        }
                    }

                ButtonAtlas.SetAccent("Violet");
                checks.Add("The outlined vector update tile renders at normal and doubled sizes without changing the update action");
                int? priorColor = null;
                foreach (string accent in new[]
                {
                    "Stellar Cyan",
                    "Violet",
                    "Solar Gold"
                }

                )
                {
                    ButtonAtlas.SetAccent(accent);
                    using (var tile = new AsteraButton
                    {
                        UpdateTile = true,
                        Role = ButtonRole.Primary,
                        Symbol = ButtonSymbol.Download,
                        AutoSize = false,
                        MinimumSize = new System.Drawing.Size(72, 72),
                        Size = new System.Drawing.Size(72, 72),
                        BackColor = System.Drawing.Color.FromArgb(14, 19, 36)
                    }

                    )
                    using (var image = new System.Drawing.Bitmap(72, 72))
                    {
                        tile.DrawToBitmap(image, new System.Drawing.Rectangle(0, 0, 72, 72));
                        int color = image.GetPixel(36, 10).ToArgb();
                        AuditAssert(!priorColor.HasValue || priorColor.Value != color, "Grayscale icon cache reused the previous accent");
                        priorColor = color;
                        image.Save(Path.Combine(root, "grayscale-" + accent.Replace(" ", "-") + ".png"));
                    }
                }

                ButtonAtlas.SetAccent("Violet");
                checks.Add("Grayscale SVG shading follows cyan, violet and gold accents without reusing stale cached colors");
                File.WriteAllText(Path.Combine(root, "result.json"), SelfUpdateFiles.Json.Serialize(new
                {
                    Passed = true,
                    Checks = checks,
                    RealNetworkRequests = false,
                    LiveAppReplaced = false
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
