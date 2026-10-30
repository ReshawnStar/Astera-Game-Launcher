using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Astera
{
    sealed partial class LibraryForm
    {
        GameTransition primaryTransition;
        int primaryTarget = -1;
        Rectangle PrimarySurfaceBounds
        {
            get
            {
                int top = topNavigation.Parent.Bottom;
                return new Rectangle(Padding.Left, top, Math.Max(1, ClientSize.Width - Padding.Horizontal), Math.Max(1, ClientSize.Height - top - Padding.Bottom));
            }
        }

        void CancelPrimaryTransition()
        {
            var old = primaryTransition;
            primaryTransition = null;
            primaryTarget = -1;
            if (old != null)
            {
                old.Parent = null;
                old.Dispose();
            }

            banner.PauseTransition(primaryPage != 0);
        }

        Bitmap CapturePrimarySurface()
        {
            if (primaryTransition != null)
                return GameTransition.CaptureFrame(primaryTransition);
            var area = PrimarySurfaceBounds;
            var image = new Bitmap(area.Width, area.Height);
            try
            {
                using (var graphics = Graphics.FromImage(image))
                {
                    graphics.Clear(BackColor);
                    foreach (Control page in new Control[]
                    {
                        gameViewport,
                        librarySidebar,
                        discoverPage,
                        arcadePage
                    }

                    )
                        if (page.Visible)
                            using (var piece = GameTransition.CaptureFrame(page))
                                graphics.DrawImageUnscaled(piece, page.Left - area.Left, page.Top - area.Top);
                }

                return image;
            }
            catch
            {
                image.Dispose();
                throw;
            }
        }

        void SelectPrimaryPage(int page)
        {
            if (page < 0 || page > 2 || primaryTarget == page || primaryTransition == null && primaryPage == page)
                return;
            if (!UiEffects.Enabled || !Visible || !IsHandleCreated)
            {
                CancelPrimaryTransition();
                ApplyPrimaryPage(page);
                return;
            }

            Bitmap previous = null;
            try
            {
                previous = CapturePrimarySurface();
                CancelPrimaryTransition();
                CancelGameTransition();
                GameTransition cover = null;
                int stable = 0;
                cover = new GameTransition(previous, delegate
                {
                    if (primaryTransition == cover)
                        CancelPrimaryTransition();
                })
                {
                    Dock = DockStyle.None,
                    Bounds = PrimarySurfaceBounds
                };
                previous = null;
                primaryTransition = cover;
                primaryTarget = page;
                Controls.Add(cover);
                cover.BringToFront();
                banner.PauseTransition(true);
                for (int i = 0; i < primaryTabs.Length; i++)
                {
                    primaryTabs[i].Selected = i == page;
                    primaryTabs[i].Invalidate();
                }

                cover.Start(delegate
                {
                    if (primaryTransition != cover)
                        return;
                    try
                    {
                        ApplyPrimaryPage(page);
                        banner.PauseTransition(true);
                        PerformLayout();
                        cover.BringToFront();
                    }
                    catch (Exception error)
                    {
                        AsteraDiagnostics.Write("Top navigation preparation", error);
                        CancelPrimaryTransition();
                        ApplyPrimaryPage(page);
                    }
                }, delegate
                {
                    if (primaryTransition != cover)
                        return null;
                    if (page == 0 && PageResourcesPending() || page == 1 && DiscoverResourcesPending())
                    {
                        stable = 0;
                        return null;
                    }

                    if (++stable < 2)
                        return null;
                    try
                    {
                        var current = primaryTransition;
                        primaryTransition = null;
                        try
                        {
                            return CapturePrimarySurface();
                        }
                        finally
                        {
                            primaryTransition = current;
                        }
                    }
                    catch (Exception error)
                    {
                        AsteraDiagnostics.Write("Top navigation capture", error);
                        CancelPrimaryTransition();
                        return null;
                    }
                });
            }
            catch (Exception error)
            {
                if (previous != null)
                    previous.Dispose();
                CancelPrimaryTransition();
                ApplyPrimaryPage(page);
                AsteraDiagnostics.Write("Top navigation fallback", error);
            }
        }

        void PrimaryEffectsChanged(object sender, EventArgs args)
        {
            if (!UiEffects.Enabled && primaryTransition != null)
            {
                int target = primaryTarget;
                CancelPrimaryTransition();
                ApplyPrimaryPage(target);
            }
        }

        void BuildPrimaryTransitionLifecycle()
        {
            UiEffects.Changed += PrimaryEffectsChanged;
            UiEffects.Changed += DiscoverEffectsChanged;
            Resize += delegate
            {
                if (primaryTransition != null)
                {
                    primaryTransition.Bounds = PrimarySurfaceBounds;
                    primaryTransition.Restage();
                }
            };
            Disposed += delegate
            {
                UiEffects.Changed -= PrimaryEffectsChanged;
                UiEffects.Changed -= DiscoverEffectsChanged;
                CancelPrimaryTransition();
            };
        }
    }
}
