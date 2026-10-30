using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Astera
{
    sealed class StartupScreen : Panel
    {
        const TextFormatFlags PaintFlags = TextFormatFlags.PreserveGraphicsClipping | TextFormatFlags.PreserveGraphicsTranslateTransform;
        readonly System.Windows.Forms.Timer clock = new System.Windows.Forms.Timer
        {
            Interval = 16
        };
        readonly Stopwatch time = Stopwatch.StartNew();
        readonly Font brandFont = UiTheme.Text(46, true), statusFont = UiTheme.Text(16), flavorFont = UiTheme.Text(15), captionFont = UiTheme.Text(12);
        readonly Action leaveLaunch;
        readonly StartupFlavorPool pool;
        readonly bool playful;
        readonly Bitmap mark;
        readonly AsteraButton close = new AsteraButton
        {
            Symbol = ButtonSymbol.Cancel,
            Text = "",
            Role = ButtonRole.Quiet,
            AutoSize = false,
            Size = new Size(38, 38),
            MinimumSize = new Size(38, 38),
            Padding = new Padding(6),
            AccessibleName = "Close Astera"
        };
        string stage = "Getting your library ready…", line = "";
        long nextLine = 3500;
        Form owner;
        internal int PaintCount;
        internal bool AnimationRunning
        {
            get
            {
                return clock.Enabled;
            }
        }

        internal string StatusText
        {
            get
            {
                return stage;
            }
        }

        internal string FlavorText
        {
            get
            {
                return line;
            }
        }

        internal Rectangle DotsBounds
        {
            get
            {
                return new Rectangle(Width / 2 - 32, Height / 2 - 4, 64, 64);
            }
        }

        Rectangle StatusBounds
        {
            get
            {
                return new Rectangle(40, Height / 2 + 83, Math.Max(1, Width - 80), 32);
            }
        }

        Rectangle FlavorBounds
        {
            get
            {
                return new Rectangle(Math.Max(30, (Width - 760) / 2), Height / 2 + 153, Math.Min(760, Math.Max(1, Width - 60)), 68);
            }
        }

        internal StartupScreen(StartupFlavorPool messages, bool showPlayful, Action leave = null, Action openLauncher = null)
        {
            leaveLaunch = leave;
            pool = messages;
            playful = showPlayful;
            line = playful ? pool.Next() : "";
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(12, 17, 33);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque | ControlStyles.ResizeRedraw, true);
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Astera.Brand.Mark"))
            using (var image = Image.FromStream(stream))
                mark = new Bitmap(image);
            if (leave != null)
            {
                close.Text = "Back to library";
                close.Symbol = ButtonSymbol.Previous;
                close.Width = 180;
                close.MinimumSize = new Size(180, 38);
                close.AccessibleName = "Back to library without stopping the game";
            }

            Controls.Add(close);
            close.Click += delegate
            {
                if (leaveLaunch != null)
                {
                    leaveLaunch();
                    return;
                }

                var form = FindForm();
                if (form != null)
                    form.Close();
            };
            if (openLauncher != null)
            {
                var open = new AsteraButton
                {
                    Text = "Open launcher",
                    AutoSize = false,
                    Size = new Size(170, 40),
                    MinimumSize = new Size(170, 40),
                    AccessibleName = "Open game launcher"
                };
                open.Click += delegate
                {
                    openLauncher();
                };
                Controls.Add(open);
                Resize += delegate
                {
                    open.Location = new Point((Width - open.Width) / 2, Height / 2 + 235);
                };
            }

            Resize += delegate
            {
                close.Location = new Point(Width - close.Width - 20, 20);
            };
            clock.Tick += delegate
            {
                Invalidate(DotsBounds);
                if (playful && time.ElapsedMilliseconds >= nextLine)
                {
                    nextLine = time.ElapsedMilliseconds + 3500;
                    line = pool.Next();
                    Invalidate(FlavorBounds);
                }
            };
            UiEffects.Changed += EffectsChanged;
        }

        internal void SetStatus(string text)
        {
            if (stage == text)
                return;
            stage = text;
            Invalidate(StatusBounds);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            owner = FindForm();
            if (owner != null)
            {
                owner.Resize += OwnerChanged;
                owner.VisibleChanged += OwnerChanged;
            }

            UpdatePlayback();
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            clock.Stop();
            DetachOwner();
            base.OnHandleDestroyed(e);
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            UpdatePlayback();
        }

        void OwnerChanged(object sender, EventArgs e)
        {
            UpdatePlayback();
        }

        void EffectsChanged(object sender, EventArgs e)
        {
            UpdatePlayback();
            Invalidate(DotsBounds);
        }

        void UpdatePlayback()
        {
            if (clock != null)
                clock.Enabled = UiEffects.Enabled && Visible && IsHandleCreated && !IsDisposed && (owner == null || owner.Visible && owner.WindowState != FormWindowState.Minimized);
        }

        void DetachOwner()
        {
            if (owner != null)
            {
                owner.Resize -= OwnerChanged;
                owner.VisibleChanged -= OwnerChanged;
                owner = null;
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            PaintCount++;
            var g = e.Graphics;
            g.Clear(BackColor);
            int y = Height / 2 - 120;
            Size title = TextRenderer.MeasureText("ASTERA", brandFont, Size.Empty, TextFormatFlags.NoPadding);
            int width = 58 + 20 + title.Width, x = (Width - width) / 2;
            g.DrawImageUnscaled(mark, x, y);
            TextRenderer.DrawText(g, "ASTERA", brandFont, new Point(x + 78, y + (58 - title.Height) / 2), Color.FromArgb(237, 241, 255), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | PaintFlags);
            LoadingDots.Draw(g, DotsBounds, time.Elapsed.TotalSeconds, UiEffects.Enabled);
            TextRenderer.DrawText(g, stage, statusFont, StatusBounds, Color.FromArgb(226, 232, 250), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | PaintFlags);
            if (playful)
            {
                var caption = new Rectangle(40, Height / 2 + 132, Math.Max(1, Width - 80), 20);
                TextRenderer.DrawText(g, "WHILE YOU WAIT", captionFont, caption, Color.FromArgb(135, 149, 182), TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix | PaintFlags);
                TextRenderer.DrawText(g, line, flavorFont, FlavorBounds, Color.FromArgb(171, 183, 213), TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | PaintFlags);
            }

            if (leaveLaunch != null)
                TextRenderer.DrawText(g, "Returning to Library will not stop the game.", captionFont, new Rectangle(30, Height - 48, Math.Max(1, Width - 60), 30), Color.FromArgb(135, 149, 182), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | PaintFlags);
            using (var border = new Pen(Color.FromArgb(44, 53, 80)))
                g.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                UiEffects.Changed -= EffectsChanged;
                DetachOwner();
                clock.Dispose();
                mark.Dispose();
                brandFont.Dispose();
                statusFont.Dispose();
                flavorFont.Dispose();
                captionFont.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    sealed partial class LibraryForm
    {
        StartupScreen startupScreen;
        CancellationTokenSource startupStop;
        bool preparingStartup;
        void BuildStartupPresentation()
        {
            if (startupScreen != null)
                return;
            startupStop = new CancellationTokenSource();
            startupScreen = new StartupScreen(StartupFlavorPool.Load(store.Root), store.Settings.StartupFlavor)
            {
                Dock = DockStyle.None,
                Bounds = ClientRectangle
            };
            Controls.Add(startupScreen);
            startupScreen.BringToFront();
            Resize += delegate
            {
                if (startupScreen != null && !startupScreen.IsDisposed)
                    startupScreen.Bounds = ClientRectangle;
            };
            banner.PauseTransition(true);
            Disposed += delegate
            {
                var stop = startupStop;
                startupStop = null;
                if (stop != null)
                {
                    stop.Cancel();
                    stop.Dispose();
                }
            };
        }

        internal async Task PrepareStartup(Func<Action<string>, Task> checks = null)
        {
            if (preparingStartup)
                return;
            preparingStartup = true;
            if (startupScreen == null)
                BuildStartupPresentation();
            var stop = startupStop.Token;
            try
            {
                await Task.Delay(65, stop);
                if (IsDisposed)
                    return;
                Action<string> report = delegate (string text)
                {
                    stop.ThrowIfCancellationRequested();
                    if (quitting)
                        throw new OperationCanceledException();
                    if (!IsDisposed && startupScreen != null)
                        startupScreen.SetStatus(text);
                };
                report("Getting your library ready…");
                RefreshMinecraftSkin();
                WarmVisibleLibraryIcons(false);
                if (checks != null)
                    await checks(report);
                else
                {
                    if (store.Settings.CheckAppOnStart)
                    {
                        report("Checking Astera updates…");
                        if (await RunAsteraUpdate(true))
                            return;
                    }

                    if (store.Settings.CheckOnStart)
                        await CheckScheduled(report);
                }

                report("Loading artwork…");
                while (!IsDisposed && (PageResourcesPending() || libraryIconsPending.Count > 0))
                {
                    await Task.Delay(50, stop);
                }

                stop.ThrowIfCancellationRequested();
                if (!IsDisposed)
                {
                    using (new PageRedraw(gameBody))
                    {
                        RefreshView();
                        gameBody.PerformLayout();
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception error)
            {
                AsteraDiagnostics.Write("Startup preparation could not finish; opening library with available content", error);
            }
            finally
            {
                if (!IsDisposed)
                {
                    var screen = startupScreen;
                    startupScreen = null;
                    if (screen != null)
                    {
                        Controls.Remove(screen);
                        screen.Dispose();
                    }

                    banner.PauseTransition(false);
                    Invalidate(true);
                    if (quitting)
                        Close();
                    else
                    {
                        foreach (var game in store.Settings.CustomGames)
                            libraryIconAttempts.Remove(LibraryIconKey(game));
                        ScheduleLibraryIcons();
                    }
                }
            }
        }
    }
}
