using System;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace Astera
{
    // Keep native list selection/keyboard behaviour. Clip its Windows scrollbar out
    // of the viewport and paint a separate thumb, without compositing native HWNDs.
    sealed class GameListBox : ListBox
    {
        internal Action ViewChanged;
        internal Action<Rectangle> FramePresented;
        bool painting;
        [StructLayout(LayoutKind.Sequential)]
        struct PaintInfo
        {
            internal IntPtr hdc;
            internal int erase, left, top, right, bottom, restore, update;
            internal int reserved0, reserved1, reserved2, reserved3, reserved4, reserved5, reserved6, reserved7;
        }

        [DllImport("user32.dll")]
        static extern IntPtr BeginPaint(IntPtr window, out PaintInfo paint);
        [DllImport("user32.dll")]
        static extern bool EndPaint(IntPtr window, ref PaintInfo paint);
        // Buffer this HWND only. Parent compositing stalls native child controls.
        void Present(IntPtr target, Rectangle dirty)
        {
            if (target == IntPtr.Zero || ClientSize.Width < 1 || ClientSize.Height < 1)
                return;
            dirty = Rectangle.Intersect(ClientRectangle, dirty);
            if (dirty.IsEmpty)
                return;
            using (var buffer = BufferedGraphicsManager.Current.Allocate(target, ClientRectangle))
            {
                buffer.Graphics.Clear(BackColor);
                buffer.Graphics.SetClip(dirty);
                painting = true;
                try
                {
                    int first = Items.Count == 0 ? 0 : TopIndex, last = Math.Min(Items.Count, first + ClientSize.Height / Math.Max(1, ItemHeight) + 1);
                    for (int i = first; i < last; i++)
                    {
                        var row = GetItemRectangle(i);
                        if (!row.IntersectsWith(dirty))
                            continue;
                        var state = SelectedIndices.Contains(i) ? DrawItemState.Selected : DrawItemState.None;
                        if (!Enabled)
                            state |= DrawItemState.Disabled;
                        if (Focused && i == SelectedIndex)
                            state |= DrawItemState.Focus;
                        OnDrawItem(new DrawItemEventArgs(buffer.Graphics, Font, row, i, state, ForeColor, BackColor));
                    }
                }
                finally
                {
                    painting = false;
                }

                buffer.Render(target);
            }

            if (FramePresented != null)
                FramePresented(dirty);
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (painting || e.Bounds.Width < 1 || e.Bounds.Height < 1)
            {
                base.OnDrawItem(e);
                return;
            }

            using (var buffer = BufferedGraphicsManager.Current.Allocate(e.Graphics, e.Bounds))
            {
                using (var brush = new SolidBrush(BackColor))
                    buffer.Graphics.FillRectangle(brush, e.Bounds);
                base.OnDrawItem(new DrawItemEventArgs(buffer.Graphics, e.Font, e.Bounds, e.Index, e.State, e.ForeColor, e.BackColor));
                buffer.Render(e.Graphics);
            }
        }

        protected override void WndProc(ref Message m)
        {
            // Native erase + direct owner drawing exposed the blank row between GIF frames.
            if (m.Msg == 0x14)
            {
                m.Result = new IntPtr(1);
                return;
            }

            if (m.Msg == 0xF && m.WParam == IntPtr.Zero)
            {
                PaintInfo paint;
                var dc = BeginPaint(Handle, out paint);
                try
                {
                    Present(dc, Rectangle.FromLTRB(paint.left, paint.top, paint.right, paint.bottom));
                }
                finally
                {
                    EndPaint(Handle, ref paint);
                }

                m.Result = IntPtr.Zero;
                return;
            }

            if (m.Msg == 0x318)
            {
                Present(m.WParam, ClientRectangle);
                m.Result = IntPtr.Zero;
                return;
            }

            base.WndProc(ref m);
            if (ViewChanged != null && (m.Msg == 0x20A || m.Msg == 0x115 || m.Msg == 0x100 || m.Msg == 0x101 || m.Msg == 0x197 || m.Msg == 0x185 || m.Msg == 0x5))
                ViewChanged();
        }
    }

    sealed class LibraryScrollbar : Control
    {
        readonly GameListBox list;
        readonly Control viewport;
        bool hover, dragging;
        int grab, wheelRemainder;
        internal LibraryScrollbar(GameListBox gameList, Control clip)
        {
            list = gameList;
            viewport = clip;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            TabStop = false;
            Cursor = Cursors.Hand;
        }

        internal int Page
        {
            get
            {
                return Math.Max(1, list.ClientSize.Height / Math.Max(1, list.ItemHeight));
            }
        }

        internal int Maximum
        {
            get
            {
                return Math.Max(0, list.Items.Count - Page);
            }
        }

        internal void MoveTo(int index)
        {
            if (list.Items.Count == 0)
                return;
            int next = Math.Max(0, Math.Min(Maximum, index));
            if (list.TopIndex != next)
                list.TopIndex = next;
            Invalidate();
        }

        internal Rectangle Thumb
        {
            get
            {
                if (Maximum == 0 || Height < 8)
                    return Rectangle.Empty;
                int h = Math.Min(Height - 4, Math.Max(28, (int)((Height - 4) * Page / (float)list.Items.Count)));
                return new Rectangle(3, 2 + (int)((Height - h - 4) * Math.Min(Maximum, list.TopIndex) / (float)Maximum), Math.Max(1, Width - 6), h);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            if (Maximum == 0 || Height < 8)
                return;
            using (var brush = new SolidBrush(Color.FromArgb(12, 17, 31)))
                e.Graphics.FillRectangle(brush, 4, 2, Math.Max(1, Width - 8), Height - 4);
            var thumb = Thumb;
            if (!ButtonAtlas.Draw(e.Graphics, thumb, ButtonRole.Primary, dragging ? "pressed" : hover ? "hover" : "normal"))
                using (var brush = new SolidBrush(ButtonAtlas.AccentColor))
                    e.Graphics.FillRectangle(brush, thumb);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hover = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left || Maximum == 0)
                return;
            var thumb = Thumb;
            if (thumb.Contains(e.Location))
            {
                dragging = true;
                grab = e.Y - thumb.Y;
                Capture = true;
            }
            else
                MoveTo(list.TopIndex + (e.Y < thumb.Y ? -Page : Page));
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!dragging)
                return;
            int range = Math.Max(1, Height - Thumb.Height - 4);
            MoveTo((int)Math.Round(Math.Max(0, Math.Min(range, e.Y - grab - 2)) * Maximum / (double)range));
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            dragging = false;
            Capture = false;
            Invalidate();
            base.OnMouseUp(e);
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            if (!Capture)
                dragging = false;
            base.OnMouseCaptureChanged(e);
        }

        internal static int WheelRows(int delta, int setting, int page, ref int remainder)
        {
            if (setting == 0)
            {
                remainder = 0;
                return 0;
            }

            remainder += delta;
            int steps = remainder / 120;
            remainder %= 120;
            long rows = (long)steps * (setting < 0 ? Math.Max(1, page) : setting);
            return (int)Math.Max(Int32.MinValue, Math.Min(Int32.MaxValue, rows));
        }

        internal void ScrollWheel(int delta)
        {
            if (!list.Enabled || list.Items.Count == 0)
                return;
            int rows = WheelRows(delta, SystemInformation.MouseWheelScrollLines, Page, ref wheelRemainder);
            if (rows == 0)
                return;
            long next = (long)list.TopIndex - rows;
            MoveTo((int)Math.Max(0, Math.Min(Maximum, next)));
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            ScrollWheel(e.Delta);
            var handled = e as HandledMouseEventArgs;
            if (handled != null)
                handled.Handled = true;
            base.OnMouseWheel(e);
        }
    }

    sealed partial class LibraryForm
    {
        Panel libraryListHost, libraryListClip;
        LibraryScrollbar libraryScrollbar;
        LibraryWheelRouting libraryWheelRouting;
        void BuildLibraryViewport(Control sidebar)
        {
            games.Dock = DockStyle.None;
            games.IntegralHeight = false;
            libraryListHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = sidebar.BackColor
            };
            libraryListClip = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = sidebar.BackColor
            };
            libraryScrollbar = new LibraryScrollbar(games, libraryListClip)
            {
                Dock = DockStyle.Right,
                Width = 18,
                BackColor = sidebar.BackColor,
                Margin = Padding.Empty
            };
            libraryListHost.Controls.Add(libraryListClip);
            libraryListHost.Controls.Add(libraryScrollbar);
            libraryListClip.Controls.Add(games);
            libraryListClip.Resize += delegate
            {
                games.Bounds = new Rectangle(0, 0, libraryListClip.ClientSize.Width + SystemInformation.VerticalScrollBarWidth, libraryListClip.ClientSize.Height);
                libraryScrollbar.Invalidate();
            };
            games.ViewChanged = delegate
            {
                if (libraryScrollbar != null && !libraryScrollbar.IsDisposed)
                    libraryScrollbar.Invalidate();
                UpdateIconAnimationPlayback();
                ScheduleLibraryIcons();
            };
            sidebar.Controls.Add(libraryListHost);
            libraryListHost.BringToFront();
            libraryWheelRouting = new LibraryWheelRouting(this, libraryListHost, games, libraryScrollbar);
            Disposed += delegate
            {
                libraryWheelRouting.Dispose();
            };
        }

        int LibraryRowRight
        {
            get
            {
                return libraryListClip == null ? games.ClientSize.Width : libraryListClip.ClientSize.Width;
            }
        }
    }
}
