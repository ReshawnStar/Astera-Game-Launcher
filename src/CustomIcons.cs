using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace Astera
{
    sealed partial class LibraryForm
    {
        readonly Dictionary<string, Image> customIcons = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
        readonly HashSet<string> customIconAttempts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void CacheCustomIcon(CustomGame game)
        {
            if (game == null || String.IsNullOrWhiteSpace(game.Icon) || !customIconAttempts.Add(game.Icon))
                return;
            try
            {
                customIcons[game.Icon] = ReadCustomIcon(game.Icon);
                RegisterIconAnimation(game.Icon);
            }
            catch (Exception error)
            {
                AsteraDiagnostics.Write("Custom library icon unavailable", new IOException(error.GetType().Name));
            }
        }

        static Bitmap ReadCustomIcon(string path)
        {
            using (var source = Banner.ReadArtwork(path))
            {
                var small = new Bitmap(128, 128, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (var graphics = Graphics.FromImage(small))
                {
                    graphics.Clear(Color.Transparent);
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    GameArtwork.Contain(graphics, source, new Rectangle(6, 6, 116, 116));
                }

                return small;
            }
        }

        Image CustomLibraryIcon(CustomGame game)
        {
            var animated = AnimatedLibraryIcon(game);
            if (animated != null)
                return animated;
            Image image;
            return game != null && !String.IsNullOrWhiteSpace(game.Icon) && customIcons.TryGetValue(game.Icon, out image) ? image : AutomaticGameIcon(game);
        }

        void ChooseCustomIcon()
        {
            if (busy)
                return;
            if (GameKind == 0)
            {
                ChooseMinecraftIcon();
                return;
            }

            if (!IsCustomGame)
                return;
            using (var dialog = new OpenFileDialog
            {
                Title = "Choose a transparent game icon",
                Filter = "PNG or GIF icon|*.png;*.gif",
                CheckFileExists = true
            }

            )
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                try
                {
                    using (var proof = ArtworkAnimation.Open(dialog.FileName))
                    {
                    }

                    var next = ReadCustomIcon(dialog.FileName);
                    Image old;
                    if (customIcons.TryGetValue(dialog.FileName, out old))
                        old.Dispose();
                    customIcons[dialog.FileName] = next;
                    customIconAttempts.Add(dialog.FileName);
                    CurrentCustom.Icon = dialog.FileName;
                    RegisterIconAnimation(dialog.FileName);
                    store.Save();
                    games.Invalidate();
                }
                catch (Exception error)
                {
                    MessageBox.Show(this, error.Message, "Game icon");
                }
            }
        }
    }
}
