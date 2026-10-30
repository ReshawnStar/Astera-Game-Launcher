using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using MC2Pack;

namespace Astera
{
    sealed partial class LibraryForm
    {
        internal static int AuditSwitchFrames(string directory)
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
            using (var form = new LibraryForm(state, new Channel(), true))
            using (var sequence = new Timer
            {
                Interval = 500
            }

            )
            {
                int step = 0, activeFrames = 0;
                Exception failure = null;
                form.TopMost = true;
                sequence.Tick += delegate
                {
                    sequence.Stop();
                    try
                    {
                        switch (step++)
                        {
                            case 0:
                                form.CaptureVisibleUi(directory, "before");
                                form.GameKind = 1;
                                sequence.Interval = 45;
                                break;
                            case 1:
                                if (form.gameTransition == null)
                                    throw new Exception("First transition not active");
                                activeFrames++;
                                form.CaptureVisibleUi(directory, "to-stardash-moving");
                                sequence.Interval = 1700;
                                break;
                            case 2:
                                if (form.gameTransition != null)
                                    throw new Exception("Transition did not settle");
                                form.CaptureVisibleUi(directory, "stardash-settled");
                                form.GameKind = 0;
                                sequence.Interval = 45;
                                break;
                            case 3:
                                if (form.gameTransition == null)
                                    throw new Exception("Return transition not active");
                                activeFrames++;
                                form.CaptureVisibleUi(directory, "to-minecraft-moving");
                                sequence.Interval = 1700;
                                break;
                            case 4:
                                form.GameKind = 1;
                                form.GameKind = 0;
                                form.GameKind = 1;
                                sequence.Interval = 1700;
                                break;
                            case 5:
                                if (form.gameTransition != null || form.GameKind != 1 || form.heroTitle.Text != "Star Dash")
                                    throw new Exception("Rapid selection left a stale page or cover: " + form.GameKind + " / " + form.heroTitle.Text + " / active=" + (form.gameTransition != null) + "");
                                form.CaptureVisibleUi(directory, "rapid-switch-settled");
                                form.GameKind = 0;
                                form.Size = new Size(1100, 760);
                                sequence.Interval = 1700;
                                break;
                            default:
                                if (form.gameTransition != null || form.heroTitle.Text != "Minecraft²")
                                    throw new Exception("Resize did not release transition");
                                form.CaptureVisibleUi(directory, "resize-settled");
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
                    finally
                    {
                        if (!form.IsDisposed && form.Visible && failure == null)
                            sequence.Start();
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

                File.WriteAllText(Path.Combine(directory, "result.json"), Engine.Json.Serialize(new
                {
                    Passed = true,
                    ActiveFrames = activeFrames,
                    RapidSelectionsSettled = true,
                    ResizeReleasedCover = true,
                    MinimumCoveredMilliseconds = GameTransition.MinimumCoverMilliseconds,
                    EntranceMilliseconds = GameTransition.EntranceMilliseconds,
                    LivePreferencesChanged = false,
                    NativePixelReviewRequired = true
                }));
                return 0;
            }
        }
    }
}
