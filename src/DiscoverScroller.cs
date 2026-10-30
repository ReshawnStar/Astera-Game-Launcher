using System;
using System.Drawing;
using System.Windows.Forms;

namespace Astera
{
    sealed class DiscoverScroller : UserControl, IMessageFilter
    {
        internal readonly Panel Content = new Panel();
        readonly Panel clip = new Panel();
        readonly DiscoverThumb thumb;
        int offset;
        internal int Maximum
        {
            get
            {
                return Math.Max(0, Content.Height - clip.Height);
            }
        }

        internal int Offset
        {
            get
            {
                return offset;
            }
        }

        internal int PageHeight
        {
            get
            {
                return clip.Height;
            }
        }

        internal DiscoverScroller()
        {
            DoubleBuffered = true;
            clip.Dock = DockStyle.Fill;
            thumb = new DiscoverThumb(this)
            {
                Dock = DockStyle.Right,
                Width = 18
            };
            Controls.Add(clip);
            Controls.Add(thumb);
            clip.Controls.Add(Content);
            clip.Resize += delegate
            {
                Content.Width = clip.ClientSize.Width;
                MoveTo(offset);
            };
            Content.SizeChanged += delegate
            {
                MoveTo(offset);
            };
            Application.AddMessageFilter(this);
        }

        internal void MoveTo(int y)
        {
            offset = Math.Max(0, Math.Min(Maximum, y));
            Content.Top = -offset;
            thumb.Invalidate();
        }

        public bool PreFilterMessage(ref Message message)
        {
            if (message.Msg != 0x20A || !Visible || IsDisposed || !RectangleToScreen(ClientRectangle).Contains(Cursor.Position) || FindForm() != Form.ActiveForm)
                return false;
            int delta = (short)((message.WParam.ToInt64() >> 16) & 0xffff);
            if (SystemInformation.MouseWheelScrollLines == 0)
                return false;
            MoveTo(offset - delta * (SystemInformation.MouseWheelScrollLines < 0 ? PageHeight : 48) / 120);
            return true;
        }

        protected override void OnBackColorChanged(EventArgs e)
        {
            base.OnBackColorChanged(e);
            if (clip != null)
                clip.BackColor = BackColor;
            if (Content != null)
                Content.BackColor = BackColor;
            if (thumb != null)
                thumb.BackColor = BackColor;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                Application.RemoveMessageFilter(this);
            base.Dispose(disposing);
        }

        sealed class DiscoverThumb : Control
        {
            readonly DiscoverScroller owner;
            bool drag, hover;
            int grab;
            internal DiscoverThumb(DiscoverScroller value)
            {
                owner = value;
                SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
                Cursor = Cursors.Hand;
            }

            Rectangle Thumb
            {
                get
                {
                    if (owner.Maximum == 0 || Height < 8)
                        return Rectangle.Empty;
                    int h = Math.Min(Height - 4, Math.Max(28, (Height - 4) * owner.PageHeight / Math.Max(1, owner.Content.Height)));
                    return new Rectangle(3, 2 + (Height - h - 4) * owner.Offset / Math.Max(1, owner.Maximum), Math.Max(1, Width - 5), h);
                }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.Clear(BackColor);
                var area = Thumb;
                if (area.IsEmpty)
                    return;
                if (!ButtonAtlas.Draw(e.Graphics, area, ButtonRole.Primary, drag ? "pressed" : hover ? "hover" : "normal"))
                    using (var brush = new SolidBrush(ButtonAtlas.AccentColor))
                        e.Graphics.FillRectangle(brush, area);
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
                if (e.Button != MouseButtons.Left)
                    return;
                if (Thumb.Contains(e.Location))
                {
                    grab = e.Y - Thumb.Top;
                    drag = true;
                    Capture = true;
                }
                else
                    owner.MoveTo(owner.Offset + (e.Y < Thumb.Top ? -owner.PageHeight : owner.PageHeight));
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                if (drag)
                    owner.MoveTo((e.Y - grab - 2) * owner.Maximum / Math.Max(1, Height - Thumb.Height - 4));
            }

            protected override void OnMouseUp(MouseEventArgs e)
            {
                drag = false;
                Capture = false;
                Invalidate();
                base.OnMouseUp(e);
            }

            protected override void OnMouseCaptureChanged(EventArgs e)
            {
                if (!Capture)
                    drag = false;
                Invalidate();
                base.OnMouseCaptureChanged(e);
            }
        }
    }

    sealed class DiscoverArtwork : Panel
    {
        internal Image Artwork;
        internal bool Shade;
        internal string Fit = "cover";
        internal float ImageX = .5f, ImageY = .5f;
        internal string CatalogKey = "";
        internal DiscoverArtwork()
        {
            DoubleBuffered = true;
            BackColor = Color.FromArgb(22, 30, 54);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            if (Artwork != null)
            {
                float ratio = Fit == "contain" ? Math.Min(Width / (float)Artwork.Width, Height / (float)Artwork.Height) : Math.Max(Width / (float)Artwork.Width, Height / (float)Artwork.Height);
                float w = Artwork.Width * ratio, h = Artwork.Height * ratio;
                var destination = new RectangleF((Width - w) * ImageX, (Height - h) * ImageY, w, h);
                using (var attributes = new System.Drawing.Imaging.ImageAttributes())
                {
                    attributes.SetWrapMode(System.Drawing.Drawing2D.WrapMode.TileFlipXY);
                    e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    e.Graphics.DrawImage(Artwork, Rectangle.Round(destination), 0, 0, Artwork.Width, Artwork.Height, GraphicsUnit.Pixel, attributes);
                }
            }

            if (Shade && Width > 0 && Height > 0)
                using (var brush = new System.Drawing.Drawing2D.LinearGradientBrush(ClientRectangle, Color.FromArgb(235, 12, 17, 33), Color.FromArgb(45, 12, 17, 33), 0f))
                    e.Graphics.FillRectangle(brush, ClientRectangle);
        }
    }
}
