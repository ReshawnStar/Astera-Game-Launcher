using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace Astera
{
    sealed class Card : Panel
    {
        public Card()
        {
            SetStyle(ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            BackColor = Color.FromArgb(21, 28, 47);
            Padding = new Padding(16);
            Margin = new Padding(0, 0, 14, 0);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Width > 2 && Height > 2)
                using (var pen = new Pen(Color.FromArgb(49, 64, 101)))
                using (var path = AsteraButton.Round(new Rectangle(0, 0, Width - 1, Height - 1), 9))
                    e.Graphics.DrawPath(pen, path);
        }
    }

    sealed class ScenePreview : Panel
    {
        public Image Artwork;
        public ScenePreview()
        {
            DoubleBuffered = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Width < 2 || Height < 2)
                return;
            if (Artwork != null)
                Banner.Cover(e.Graphics, Artwork, ClientRectangle);
            else
            {
                using (var fill = new LinearGradientBrush(ClientRectangle, Color.FromArgb(23, 36, 76), Color.FromArgb(61, 38, 91), 25F))
                    e.Graphics.FillRectangle(fill, ClientRectangle);
                var random = new Random(19);
                for (int i = 0; i < 40; i++)
                    e.Graphics.FillEllipse(Brushes.LightSlateGray, random.Next(Width), random.Next(Height), 2, 2);
            }
        }
    }
}
