using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using MC2Pack;

namespace Astera
{
    sealed partial class LibraryForm
    {
        [DllImport("user32.dll")]
        static extern IntPtr WindowFromPoint(Point point);
        [DllImport("user32.dll")]
        static extern IntPtr GetAncestor(IntPtr window, uint flags);
        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();
        internal void CaptureVisibleUi(string directory, string name)
        {
            Directory.CreateDirectory(directory);
            if (!TopMost && GetForegroundWindow() != Handle)
                throw new InvalidOperationException("Activate Astera before taking its UI snapshot.");
            Point origin = PointToScreen(Point.Empty);
            foreach (var sample in new[]
            {
                new Point(2, 2),
                new Point(ClientSize.Width - 3, 2),
                new Point(2, ClientSize.Height - 3),
                new Point(ClientSize.Width - 3, ClientSize.Height - 3),
                new Point(ClientSize.Width / 2, ClientSize.Height / 2)
            }

            )
            {
                if (GetAncestor(WindowFromPoint(new Point(origin.X + sample.X, origin.Y + sample.Y)), 2) != Handle)
                    throw new InvalidOperationException("Astera is obscured. Bring its window fully into view before taking a UI snapshot.");
            }

            for (int y = 16; y < ClientSize.Height; y += 64)
                for (int x = 16; x < ClientSize.Width; x += 64)
                    if (GetAncestor(WindowFromPoint(new Point(origin.X + x, origin.Y + y)), 2) != Handle)
                        throw new InvalidOperationException("Another window is covering Astera; UI capture was stopped.");
            var pixels = PhysicalCaptureBounds(this, ClientRectangle);
            using (var image = new Bitmap(pixels.Width, pixels.Height))
            using (var graphics = Graphics.FromImage(image))
            {
                graphics.CopyFromScreen(pixels.Location, Point.Empty, pixels.Size, CopyPixelOperation.SourceCopy);
                image.Save(Path.Combine(directory, name + ".png"), ImageFormat.Png);
            }

            var controls = new List<object>();
            DescribeControls(this, controls);
            File.WriteAllText(Path.Combine(directory, name + ".json"), Engine.Json.Serialize(new
            {
                Version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(),
                Capture = "Displayed physical client pixels; not DrawToBitmap",
                PhysicalPixels = pixels.ToString(),
                Window = Bounds.ToString(),
                Client = ClientSize.ToString(),
                Tab = selectedTab,
                LiveSkinActive = banner.LiveSkinActive,
                TransitionActive = gameTransition != null,
                Controls = controls
            }));
        }

        static void DescribeControls(Control parent, List<object> controls)
        {
            foreach (Control child in parent.Controls)
            {
                if (child.Visible)
                    controls.Add(new
                    {
                        Type = child.GetType().Name,
                        Text = child.Text.Length > 256 ? child.Text.Substring(0, 256) : child.Text,
                        Bounds = child.Bounds.ToString(),
                        Parent = parent.GetType().Name,
                        Enabled = child.Enabled
                    });
                DescribeControls(child, controls);
            }
        }

        internal void CaptureUiFromMenu()
        {
            var captureTimer = new System.Windows.Forms.Timer
            {
                Interval = 300
            };
            captureTimer.Tick += delegate
            {
                captureTimer.Stop();
                captureTimer.Dispose();
                if (IsDisposed || !Visible || WindowState == FormWindowState.Minimized)
                    return;
                try
                {
                    string activityKey = SelectedActivityKey;
                    string directory = Path.Combine(DiagnosticsRoot, "ui", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
                    Refresh();
                    CaptureVisibleUi(directory, "window");
                    LogActivity(activityKey, "Saved visible UI pixels and control bounds: " + directory);
                    SelectLibraryTab(1);
                }
                catch (Exception error)
                {
                    AsteraDiagnostics.Report("Capture UI snapshot", error);
                }
            };
            captureTimer.Start();
        }

        public static int AuditVisibleUi(string directory, string characterFile = null, bool liveSkin = false)
        {
            directory = Path.GetFullPath(directory);
            if (Directory.Exists(directory))
                return 2;
            Directory.CreateDirectory(directory);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var store = new Store(Path.Combine(directory, "preferences"));
            store.Settings.CheckOnStart = false;
            store.Settings.KeepInTray = false;
            if (characterFile != null)
            {
                using (var proof = Banner.ReadArtwork(characterFile))
                {
                }

                if (!liveSkin)
                    store.Settings.CharacterMinecraft = Path.GetFullPath(characterFile);
            }

            using (var form = new LibraryForm(store, new Channel(), true))
            using (var sequence = new System.Windows.Forms.Timer
            {
                Interval = 700
            }

            )
            {
                if (liveSkin)
                {
                    form.minecraftSkinArt = Path.GetFullPath(characterFile);
                    form.UpdateLayoutView();
                    if (!form.banner.LiveSkinActive)
                        throw new InvalidOperationException("Live skin audit did not load the rig");
                }

                int step = 0;
                Exception failure = null;
                form.TopMost = true;
                form.StartPosition = FormStartPosition.CenterScreen;
                form.notes.Text = String.Join(Environment.NewLine, Enumerable.Range(1, 60).Select(i => "Update-note scroll fixture line " + i));
                form.log.Text = String.Join(Environment.NewLine, Enumerable.Range(1, 60).Select(i => "Activity scroll fixture line " + i));
                sequence.Tick += delegate
                {
                    try
                    {
                        switch (step++)
                        {
                            case 0:
                                form.CaptureVisibleUi(directory, "overview");
                                form.Refresh();
                                break;
                            case 1:
                                form.CaptureVisibleUi(directory, "overview-repainted");
                                form.SelectLibraryTab(1);
                                break;
                            case 2:
                                form.CaptureVisibleUi(directory, "updates");
                                form.SelectLibraryTab(2);
                                break;
                            case 3:
                                form.CaptureVisibleUi(directory, "backgrounds");
                                form.SelectLibraryTab(3);
                                break;
                            case 4:
                                form.CaptureVisibleUi(directory, "settings");
                                form.SelectLibraryTab(0);
                                form.Size = new Size(1100, 760);
                                break;
                            case 5:
                                form.CaptureVisibleUi(directory, "compact");
                                form.Size = new Size(1400, 900);
                                break;
                            case 6:
                                form.CaptureVisibleUi(directory, "restored");
                                form.play.Enabled = false;
                                break;
                            case 7:
                                form.CaptureVisibleUi(directory, "disabled");
                                form.play.Enabled = true;
                                break;
                            case 8:
                                form.CaptureVisibleUi(directory, "resumed");
                                form.ToggleMaximize();
                                break;
                            case 9:
                                form.CaptureVisibleUi(directory, "maximized");
                                form.ToggleMaximize();
                                break;
                            case 10:
                                form.CaptureVisibleUi(directory, "restored-from-maximized");
                                form.GameKind = 1;
                                break;
                            case 11:
                                form.CaptureVisibleUi(directory, "stardash");
                                form.Size = new Size(1100, 760);
                                break;
                            case 12:
                                form.CaptureVisibleUi(directory, "stardash-compact");
                                form.GameKind = 0;
                                break;
                            case 13:
                                form.CaptureVisibleUi(directory, "minecraft-after-stardash");
                                form.GameKind = 2;
                                break;
                            case 14:
                                form.CaptureVisibleUi(directory, "multiverse");
                                form.Size = new Size(1400, 900);
                                break;
                            case 15:
                                form.CaptureVisibleUi(directory, "multiverse-wide");
                                form.GameKind = 0;
                                break;
                            default:
                                form.CaptureVisibleUi(directory, "minecraft-after-multiverse");
                                sequence.Stop();
                                form.Close();
                                break;
                        }
                    }
                    catch (Exception error)
                    {
                        failure = error;
                        sequence.Stop();
                        form.Close();
                    }
                };
                form.Shown += delegate
                {
                    form.Activate();
                    sequence.Start();
                };
                Application.Run(form);
                if (failure != null)
                {
                    File.WriteAllText(Path.Combine(directory, "failure.txt"), failure.ToString());
                    return 1;
                }
            }

            File.WriteAllText(Path.Combine(directory, "result.json"), Engine.Json.Serialize(new
            {
                Completed = true,
                Capture = "Actual displayed client pixels",
                Snapshots = 17,
                LiveProfilesChanged = false,
                NetworkUsed = false,
                VisualReviewRequired = true
            }));
            return 0;
        }
    }
}
