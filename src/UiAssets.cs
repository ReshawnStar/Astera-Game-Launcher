using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Reflection;

namespace Astera
{
    static class UiTheme
    {
        static readonly string Family = FindFamily();
        static string FindFamily()
        {
            using (var installed = new InstalledFontCollection())
            {
                foreach (string candidate in new[]
                {
                    "Segoe UI Variable Text",
                    "Segoe UI Variable",
                    "Segoe UI",
                    "Inter",
                    "Arial"
                }

                )
                    foreach (var family in installed.Families)
                        if (family.Name == candidate)
                            return candidate;
            }

            return FontFamily.GenericSansSerif.Name;
        }

        // The supplied guide is in CSS pixels at 96 DPI; GDI fonts use points.
        public static Font Text(float pixels, bool strong = false)
        {
            return new Font(Family, pixels * .75F, strong ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Point);
        }

        public static Font Button()
        {
            return new Font(Family == "Segoe UI" ? "Segoe UI Semibold" : Family, 15 * .75F, Family == "Segoe UI" ? FontStyle.Regular : FontStyle.Bold, GraphicsUnit.Point);
        }
    }

    static class UiAssets
    {
        sealed class Glyph
        {
            public Bitmap Image;
            public Rectangle Ink;
        }

        static readonly Dictionary<string, Glyph> Icons = new Dictionary<string, Glyph>();
        public static bool Draw(Graphics g, string name, Rectangle target, Color color)
        {
            if (name == null)
                return false;
            Glyph icon;
            if (!Icons.TryGetValue(name, out icon))
            {
                icon = null;
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Astera.Icons." + name))
                    if (stream != null)
                        using (var image = Image.FromStream(stream))
                        {
                            var bitmap = new Bitmap(image);
                            int left = bitmap.Width, top = bitmap.Height, right = -1, bottom = -1;
                            for (int y = 0; y < bitmap.Height; y++)
                                for (int x = 0; x < bitmap.Width; x++)
                                    if (bitmap.GetPixel(x, y).A > 8)
                                    {
                                        left = Math.Min(left, x);
                                        top = Math.Min(top, y);
                                        right = Math.Max(right, x);
                                        bottom = Math.Max(bottom, y);
                                    }

                            if (right >= left)
                            {
                                left = Math.Max(0, left - 1);
                                top = Math.Max(0, top - 1);
                                right = Math.Min(bitmap.Width - 1, right + 1);
                                bottom = Math.Min(bitmap.Height - 1, bottom + 1);
                                icon = new Glyph
                                {
                                    Image = bitmap,
                                    Ink = Rectangle.FromLTRB(left, top, right + 1, bottom + 1)
                                };
                            }
                            else
                                bitmap.Dispose();
                        }

                Icons[name] = icon;
            }

            if (icon == null)
                return false;
            float scale = Math.Min(target.Width / (float)icon.Ink.Width, target.Height / (float)icon.Ink.Height);
            int w = Math.Max(1, (int)Math.Round(icon.Ink.Width * scale)), h = Math.Max(1, (int)Math.Round(icon.Ink.Height * scale));
            var destination = new Rectangle(target.X + (target.Width - w) / 2, target.Y + (target.Height - h) / 2, w, h);
            using (var attributes = new ImageAttributes())
            {
                var matrix = new ColorMatrix();
                matrix.Matrix00 = color.R / 237F;
                matrix.Matrix11 = color.G / 241F;
                matrix.Matrix22 = color.B / 255F;
                matrix.Matrix33 = color.A / 255F;
                attributes.SetColorMatrix(matrix);
                attributes.SetWrapMode(WrapMode.TileFlipXY);
                var interpolation = g.InterpolationMode;
                var offset = g.PixelOffsetMode;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(icon.Image, destination, icon.Ink.X, icon.Ink.Y, icon.Ink.Width, icon.Ink.Height, GraphicsUnit.Pixel, attributes);
                g.InterpolationMode = interpolation;
                g.PixelOffsetMode = offset;
            }

            return true;
        }

        public static string Name(ButtonSymbol symbol)
        {
            switch (symbol)
            {
                case ButtonSymbol.Play:
                    return "play";
                case ButtonSymbol.Refresh:
                    return "refresh-cw";
                case ButtonSymbol.Settings:
                    return "settings";
                case ButtonSymbol.Exit:
                    return "log-out";
                case ButtonSymbol.Download:
                    return "download";
                case ButtonSymbol.Install:
                    return "package-check";
                case ButtonSymbol.Restore:
                    return "history";
                case ButtonSymbol.Cancel:
                    return "x";
                case ButtonSymbol.More:
                    return "ellipsis";
                case ButtonSymbol.Overview:
                    return "layout-dashboard";
                case ButtonSymbol.Updates:
                    return "scroll-text";
                case ButtonSymbol.Backgrounds:
                    return "image";
                case ButtonSymbol.Folder:
                    return "folder-open";
                case ButtonSymbol.AddArtwork:
                    return "image-plus";
                case ButtonSymbol.Drive:
                    return "hard-drive";
                case ButtonSymbol.Verified:
                    return "check-circle";
                case ButtonSymbol.Warning:
                    return "alert-triangle";
                case ButtonSymbol.Previous:
                    return "chevron-left";
                case ButtonSymbol.Next:
                    return "chevron-right";
                case ButtonSymbol.Pause:
                    return "pause";
                case ButtonSymbol.Retry:
                    return "rotate-ccw";
                default:
                    return null;
            }
        }
    }
}
