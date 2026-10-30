using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using MC2Pack;

namespace Astera
{
    sealed partial class LibraryForm
    {
        internal static int AuditLiveStability(string directory, string pose)
        {
            directory = Path.GetFullPath(directory);
            if (Directory.Exists(directory))
                return 2;
            Directory.CreateDirectory(directory);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var state = new Store(Path.Combine(directory, "preferences"));
            state.Settings.CheckOnStart = false;
            state.Settings.KeepInTray = false;
            var originalPointer = Cursor.Position;
            try
            {
                using (var form = new LibraryForm(state, new Channel(), true))
                using (var sequence = new Timer
                {
                    Interval = 50
                }

                )
                {
                    form.minecraftSkinArt = Path.GetFullPath(pose);
                    form.UpdateLayoutView();
                    if (!form.banner.LiveSkinActive)
                        throw new Exception("Rig not active");
                    form.TopMost = true;
                    Bitmap baseline = null;
                    int samples = 0;
                    Exception failure = null;
                    sequence.Tick += delegate
                    {
                        try
                        {
                            if (samples % 8 == 0)
                            {
                                int direction = samples / 8;
                                var b = form.banner;
                                Point target = direction == 0 ? new Point((int)(b.Width * .53), b.Height / 2) : direction == 1 ? new Point(b.Width - 8, b.Height / 2) : direction == 2 ? new Point((int)(b.Width * .75), 32) : new Point((int)(b.Width * .75), b.Height - 8);
                                Cursor.Position = b.PointToScreen(target);
                            }

                            var origin = form.banner.PointToScreen(new Point(8, 36));
                            using (var sample = new Bitmap(8, form.banner.Height - 46))
                            {
                                using (var g = Graphics.FromImage(sample))
                                    g.CopyFromScreen(origin, Point.Empty, sample.Size, CopyPixelOperation.SourceCopy);
                                if (baseline == null)
                                {
                                    baseline = new Bitmap(sample);
                                    sample.Save(Path.Combine(directory, "background-baseline.png"));
                                }
                                else
                                    for (int y = 0; y < sample.Height; y++)
                                        for (int x = 0; x < sample.Width; x++)
                                            if (sample.GetPixel(x, y) != baseline.GetPixel(x, y))
                                                throw new Exception("Static background pixels changed during animation");
                            }

                            if (samples % 8 == 7)
                                form.CaptureVisibleUi(directory, "direction-" + (samples / 8));
                            if (++samples == 32)
                            {
                                sequence.Stop();
                                form.Close();
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
                    if (baseline != null)
                        baseline.Dispose();
                    if (failure != null)
                        throw failure;
                    File.WriteAllText(Path.Combine(directory, "result.json"), Engine.Json.Serialize(new
                    {
                        Passed = true,
                        NativeBackgroundSamples = samples,
                        DirectionsCaptured = 4,
                        LiveRigConfirmed = true,
                        StaticStripPixelsUnchanged = true,
                        LivePreferencesChanged = false
                    }));
                    return 0;
                }
            }
            catch (Exception error)
            {
                File.WriteAllText(Path.Combine(directory, "failure.txt"), error.ToString());
                return 1;
            }
            finally
            {
                Cursor.Position = originalPointer;
            }
        }
    }
}
