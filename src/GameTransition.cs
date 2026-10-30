using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace Astera
{
    // An opaque sibling owns presentation until preparation has actually completed.
    // Old and new pages are never composited over each other.
    sealed class GameTransition : Panel
    {
        Bitmap previous, incoming;
        readonly Timer clock = new Timer
        {
            Interval = 15
        };
        readonly Stopwatch elapsed = new Stopwatch(), entrance = new Stopwatch();
        readonly Action finished;
        Action prepare;
        Func<Bitmap> ready;
        bool preparing, completed;
        long nextProbe;
        internal const int FadeOutMilliseconds = 220, MinimumCoverMilliseconds = 1000, EntranceMilliseconds = 380;
        internal bool RevealStarted;
        internal long PaintCount, PaintTicks, MaxPaintTicks;
        internal bool Preparing
        {
            get
            {
                return preparing && incoming == null;
            }
        }

        internal GameTransition(Bitmap old, Action done)
        {
            previous = old;
            finished = done;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque, true);
            Dock = DockStyle.Fill;
            clock.Tick += Tick;
        }

        internal void Start(Bitmap next)
        {
            Start(null, delegate
            {
                return next;
            });
        }

        internal void Start(Action begin, Func<Bitmap> whenReady)
        {
            prepare = begin;
            ready = whenReady;
            elapsed.Restart();
            clock.Start();
            Invalidate();
        }

        void Tick(object sender, EventArgs e)
        {
            if (!preparing && elapsed.ElapsedMilliseconds >= FadeOutMilliseconds)
            {
                preparing = true;
                clock.Interval = 50;
                Invalidate();
                Update();
                if (prepare != null)
                    prepare();
                if (IsDisposed)
                    return;
            }

            if (preparing && incoming == null && elapsed.ElapsedMilliseconds >= MinimumCoverMilliseconds && elapsed.ElapsedMilliseconds >= nextProbe)
            {
                nextProbe = elapsed.ElapsedMilliseconds + 40;
                var next = ready == null ? null : ready();
                if (IsDisposed)
                {
                    if (next != null)
                        next.Dispose();
                    return;
                }

                if (next != null)
                {
                    incoming = next;
                    RevealStarted = true;
                    clock.Interval = 15;
                    entrance.Restart();
                }
            }

            if (incoming != null && entrance.ElapsedMilliseconds >= EntranceMilliseconds)
            {
                clock.Stop();
                completed = true;
                Invalidate();
                Update();
                if (finished != null)
                    finished();
                return;
            }

            if (!preparing || incoming != null)
                Invalidate();
            else if (UiEffects.Enabled && Visible && (FindForm() == null || FindForm().WindowState != FormWindowState.Minimized))
                Invalidate(LoadingBounds);
        }

        internal void Restage()
        {
            if (!preparing)
                return;
            if (incoming != null)
            {
                incoming.Dispose();
                incoming = null;
            }

            entrance.Reset();
            nextProbe = elapsed.ElapsedMilliseconds + 80;
            Invalidate();
        }

        internal static Bitmap CaptureFrame(Control content)
        {
            var image = new Bitmap(content.Width, content.Height, PixelFormat.Format32bppPArgb);
            try
            {
                using (var g = Graphics.FromImage(image))
                    g.Clear(content.BackColor);
                content.DrawToBitmap(image, content.ClientRectangle);
                return image;
            }
            catch
            {
                image.Dispose();
                throw;
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            long start = Stopwatch.GetTimestamp();
            DrawSurface(e.Graphics, preparing ? 1 : 0, incoming == null ? 0 : completed ? 1 : entrance.Elapsed.TotalMilliseconds / EntranceMilliseconds, elapsed.Elapsed.TotalMilliseconds / FadeOutMilliseconds);
            long ticks = Stopwatch.GetTimestamp() - start;
            PaintTicks += ticks;
            PaintCount++;
            MaxPaintTicks = Math.Max(MaxPaintTicks, ticks);
        }

        internal void DrawSurface(Graphics g, int phase, double reveal, double fade)
        {
            Color flat = Parent == null ? Color.FromArgb(12, 17, 33) : Parent.BackColor;
            g.Clear(flat);
            if (phase == 0)
            {
                if (previous != null)
                    g.DrawImageUnscaled(previous, 0, 0);
                Shade(g, flat, AsteraButton.Ease(fade));
                return;
            }

            if (incoming == null)
            {
                LoadingDots.Draw(g, LoadingBounds, elapsed.Elapsed.TotalSeconds, UiEffects.Enabled);
                return;
            }

            if (incoming != null)
            {
                double ease = AsteraButton.Ease(reveal);
                g.DrawImageUnscaled(incoming, 0, (int)Math.Round(48 * (1 - ease)));
                Shade(g, flat, 1 - ease);
            }
        }

        internal Rectangle LoadingBounds
        {
            get
            {
                return new Rectangle((Width - 64) / 2, (Height - 64) / 2, 64, 64);
            }
        }

        void Shade(Graphics g, Color flat, double opacity)
        {
            int alpha = (int)Math.Round(Math.Max(0, Math.Min(1, opacity)) * 255);
            if (alpha == 0)
                return;
            using (var brush = new SolidBrush(Color.FromArgb(alpha, flat)))
                g.FillRectangle(brush, ClientRectangle);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                clock.Dispose();
                if (previous != null)
                    previous.Dispose();
                if (incoming != null)
                    incoming.Dispose();
                previous = incoming = null;
                prepare = null;
                ready = null;
            }

            base.Dispose(disposing);
        }
    }

    sealed partial class LibraryForm
    {
        Panel gameViewport;
        TableLayoutPanel gameBody;
        GameTransition gameTransition;
        void CancelGameTransition()
        {
            var old = gameTransition;
            gameTransition = null;
            if (old != null)
            {
                old.Parent = null;
                old.Dispose();
            }

            banner.PauseTransition(false);
            if (gameBody != null)
                gameBody.Invalidate(true);
        }

        bool PageResourcesPending()
        {
            if (!IsCustomGame)
                return GameKind == 0 && loadingMinecraftSkin;
            var game = CurrentCustom;
            if (game == null)
                return false;
            var art = CustomSteam(game);
            return steamArtPending.Contains(game.Executable) || itchPending.Contains(game.Executable) || robloxDetailsPending.Contains(game.Id) || (art != null && (publicSteamPending.Contains(art.AppId) || newsPending.Contains(art.AppId)));
        }

        void SwitchGame()
        {
            UpdateRobloxSlidePlayback();
            UpdatePublicArtworkSelection();
            if (!UiEffects.Enabled)
            {
                CancelGameTransition();
                RefreshView();
                return;
            }

            if (gameViewport == null || gameBody == null || !Visible || !IsHandleCreated || gameBody.Width < 2 || gameBody.Height < 2)
            {
                RefreshView();
                return;
            }

            Bitmap before = null;
            try
            {
                before = GameTransition.CaptureFrame(gameTransition == null ? (Control)gameBody : gameTransition);
                var prior = gameTransition;
                GameTransition cover = null;
                int stable = 0;
                cover = new GameTransition(before, delegate
                {
                    if (gameTransition == cover)
                        CancelGameTransition();
                });
                before = null;
                gameTransition = cover;
                gameViewport.Controls.Add(cover);
                cover.BringToFront();
                cover.Refresh();
                if (prior != null)
                {
                    prior.Parent = null;
                    prior.Dispose();
                }

                banner.PauseTransition(true);
                cover.Start(delegate
                {
                    if (gameTransition != cover)
                        return;
                    try
                    {
                        using (new PageRedraw(gameBody))
                        {
                            RefreshView();
                            if (GameKind == 0)
                                RefreshMinecraftSkin();
                            gameBody.PerformLayout();
                        }
                    }
                    catch (Exception error)
                    {
                        AsteraDiagnostics.Write("Page preparation unavailable", error);
                        CancelGameTransition();
                    }
                }, delegate
                {
                    if (gameTransition != cover)
                        return null;
                    if (PageResourcesPending())
                    {
                        stable = 0;
                        return null;
                    }

                    if (++stable < 2)
                        return null;
                    try
                    {
                        using (new PageRedraw(gameBody))
                        {
                            gameBody.PerformLayout();
                        }

                        return GameTransition.CaptureFrame(gameBody);
                    }
                    catch (Exception error)
                    {
                        AsteraDiagnostics.Write("Prepared page capture unavailable", error);
                        CancelGameTransition();
                        return null;
                    }
                });
            }
            catch (Exception error)
            {
                if (before != null)
                    before.Dispose();
                CancelGameTransition();
                RefreshView();
                AsteraDiagnostics.Write("Game transition fallback", error);
            }
        }
    }
}
