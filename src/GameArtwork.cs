using System;
using System.Drawing;
using System.Reflection;

namespace Astera
{
    static class GameArtwork
    {
        internal static readonly Image VanillaMinecraftIcon = Load("Astera.Games.MinecraftVanillaIcon");
        internal static readonly Image VanillaMinecraftLogo = Load("Astera.Games.MinecraftVanillaLogo");
        internal static readonly Rectangle VanillaLogoBounds = VisibleBounds(VanillaMinecraftLogo);
        internal static readonly Image MinecraftIcon = Load("Astera.Games.MinecraftSquaredIcon");
        internal static readonly Image NebulaIcon = Load("Astera.Games.GachaNebulaIcon");
        internal static readonly Image FtlIcon = Load("Astera.Games.FtlMultiverseIcon");
        internal static readonly Image StarDashIcon = Load("Astera.Games.StarDashIcon");
        static Rectangle VisibleBounds(Image source)
        {
            var image = source as Bitmap;
            if (image == null)
                return Rectangle.Empty;
            int left = image.Width, top = image.Height, right = -1, bottom = -1;
            for (int y = 0; y < image.Height; y++)
                for (int x = 0; x < image.Width; x++)
                    if (image.GetPixel(x, y).A > 8)
                    {
                        left = Math.Min(left, x);
                        top = Math.Min(top, y);
                        right = Math.Max(right, x);
                        bottom = Math.Max(bottom, y);
                    }

            return right < left ? Rectangle.Empty : Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
        }

        internal static Image Load(string name)
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
                if (stream != null)
                    using (var image = Image.FromStream(stream))
                        return new Bitmap(image);
            return null;
        }

        internal static void Contain(Graphics graphics, Image image, Rectangle target)
        {
            if (image == null || target.Width < 1 || target.Height < 1)
                return;
            float scale = Math.Min(target.Width / (float)image.Width, target.Height / (float)image.Height);
            float width = image.Width * scale, height = image.Height * scale;
            graphics.DrawImage(image, target.X + (target.Width - width) / 2, target.Y + (target.Height - height) / 2, width, height);
        }
    }
}
