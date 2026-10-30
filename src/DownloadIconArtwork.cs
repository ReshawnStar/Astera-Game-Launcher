using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;

namespace Astera
{
    // Render the supplied filled SVG directly. The other original variants are kept
    // as source assets; there is no external SVG engine, browser, script or network.
    static class DownloadIconArtwork
    {
        static readonly Dictionary<string, Bitmap> cache = new Dictionary<string, Bitmap>();
        static XmlDocument source;
        static float Number(string value)
        {
            return Single.Parse(value, CultureInfo.InvariantCulture);
        }

        static float Attribute(XmlElement element, string name, float fallback = 0)
        {
            string value = element.GetAttribute(name);
            return value.Length == 0 ? fallback : Number(value);
        }

        internal static void Draw(Graphics graphics, Rectangle bounds, bool enabled)
        {
            int size = Math.Max(1, Math.Min(bounds.Width, bounds.Height));
            string key = size + ":" + ButtonAtlas.AccentColor.ToArgb() + ":" + ButtonAtlas.AccentTextColor.ToArgb();
            Bitmap image;
            if (!cache.TryGetValue(key, out image))
            {
                if (source == null)
                {
                    var document = new XmlDocument
                    {
                        XmlResolver = null
                    };
                    using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Astera.Download.filled-grayscale"))
                    using (var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
                        document.Load(reader);
                    source = document;
                }

                image = new Bitmap(size, size, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(image))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.ScaleTransform(size / 64f, size / 64f);
                    foreach (XmlNode node in source.DocumentElement.ChildNodes)
                    {
                        var element = node as XmlElement;
                        if (element == null)
                            continue;
                        if (element.LocalName == "rect")
                            DrawRect(g, element);
                        else if (element.LocalName == "path")
                            DrawPath(g, element);
                    }
                }

                if (cache.Count >= 12)
                {
                    foreach (var old in cache.Values)
                        old.Dispose();
                    cache.Clear();
                }

                cache[key] = image;
            }

            var destination = new Rectangle(bounds.X + (bounds.Width - size) / 2, bounds.Y + (bounds.Height - size) / 2, size, size);
            if (enabled)
                graphics.DrawImageUnscaled(image, destination.Location);
            else
                using (var attributes = new ImageAttributes())
                {
                    var matrix = new ColorMatrix();
                    matrix.Matrix33 = .4f;
                    attributes.SetColorMatrix(matrix);
                    graphics.DrawImage(image, destination, 0, 0, size, size, GraphicsUnit.Pixel, attributes);
                }
        }

        static Color Tint(int gray)
        {
            var accent = ButtonAtlas.AccentColor;
            float ratio = gray / 190f;
            if (ratio <= 1)
                return Color.FromArgb((int)(accent.R * ratio), (int)(accent.G * ratio), (int)(accent.B * ratio));
            float light = Math.Min(.35f, (gray - 190) / 190f);
            return Color.FromArgb((int)(accent.R + (255 - accent.R) * light), (int)(accent.G + (255 - accent.G) * light), (int)(accent.B + (255 - accent.B) * light));
        }

        static void DrawRect(Graphics g, XmlElement element)
        {
            float x = Attribute(element, "x"), y = Attribute(element, "y"), w = Attribute(element, "width"), h = Attribute(element, "height"), radius = Attribute(element, "rx");
            using (var path = new GraphicsPath())
            {
                float d = radius * 2;
                path.AddArc(x, y, d, d, 180, 90);
                path.AddArc(x + w - d, y, d, d, 270, 90);
                path.AddArc(x + w - d, y + h - d, d, d, 0, 90);
                path.AddArc(x, y + h - d, d, d, 90, 90);
                path.CloseFigure();
                string fill = element.GetAttribute("fill");
                if (fill == "url(#cyan)")
                {
                    var gradient = (XmlElement)source.GetElementsByTagName("linearGradient")[0];
                    var stops = gradient.GetElementsByTagName("stop");
                    var colors = new Color[stops.Count];
                    var positions = new float[stops.Count];
                    for (int i = 0; i < stops.Count; i++)
                    {
                        var stop = (XmlElement)stops[i];
                        colors[i] = Tint(ColorTranslator.FromHtml(stop.GetAttribute("stop-color")).R);
                        positions[i] = Attribute(stop, "offset");
                    }

                    using (var brush = new LinearGradientBrush(new PointF(x + w * Attribute(gradient, "x1"), y + h * Attribute(gradient, "y1")), new PointF(x + w * Attribute(gradient, "x2", 1), y + h * Attribute(gradient, "y2", 1)), colors[0], colors[colors.Length - 1]))
                    {
                        brush.WrapMode = WrapMode.TileFlipXY;
                        brush.InterpolationColors = new ColorBlend
                        {
                            Colors = colors,
                            Positions = positions
                        };
                        g.FillPath(brush, path);
                    }
                }
                else
                    using (var brush = new SolidBrush(ColorTranslator.FromHtml(fill)))
                        g.FillPath(brush, path);
            }
        }

        static void DrawPath(Graphics g, XmlElement element)
        {
            using (var path = Path(element.GetAttribute("d")))
            using (var pen = new Pen(ButtonAtlas.AccentTextColor, Attribute(element, "stroke-width", 1)))
            {
                pen.StartCap = pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                g.DrawPath(pen, path);
            }
        }

        static GraphicsPath Path(string data)
        {
            var tokens = Regex.Matches(data, "[MLHVQCZ]|[-+]?(?:[0-9]*\\.)?[0-9]+");
            var path = new GraphicsPath();
            PointF point = PointF.Empty;
            char command = ' ';
            int i = 0;
            Func<float> read = delegate
            {
                return Number(tokens[i++].Value);
            };
            while (i < tokens.Count)
            {
                if (Char.IsLetter(tokens[i].Value[0]))
                    command = tokens[i++].Value[0];
                PointF next;
                switch (command)
                {
                    case 'M':
                        point = new PointF(read(), read());
                        path.StartFigure();
                        command = 'L';
                        break;
                    case 'L':
                        next = new PointF(read(), read());
                        path.AddLine(point, next);
                        point = next;
                        break;
                    case 'H':
                        next = new PointF(read(), point.Y);
                        path.AddLine(point, next);
                        point = next;
                        break;
                    case 'V':
                        next = new PointF(point.X, read());
                        path.AddLine(point, next);
                        point = next;
                        break;
                    case 'Q':
                        var control = new PointF(read(), read());
                        next = new PointF(read(), read());
                        path.AddBezier(point, new PointF(point.X + 2 * (control.X - point.X) / 3, point.Y + 2 * (control.Y - point.Y) / 3), new PointF(next.X + 2 * (control.X - next.X) / 3, next.Y + 2 * (control.Y - next.Y) / 3), next);
                        point = next;
                        break;
                    case 'C':
                        var one = new PointF(read(), read());
                        var two = new PointF(read(), read());
                        next = new PointF(read(), read());
                        path.AddBezier(point, one, two, next);
                        point = next;
                        break;
                    case 'Z':
                        path.CloseFigure();
                        command = ' ';
                        break;
                    default:
                        path.Dispose();
                        throw new IOException("Unsupported download icon path");
                }
            }

            return path;
        }
    }
}
