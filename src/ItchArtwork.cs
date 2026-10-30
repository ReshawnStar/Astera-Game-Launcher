using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Astera
{
    static class ItchArtwork
    {
        internal static bool Address(string value)
        {
            Uri uri;
            return Uri.TryCreate(value, UriKind.Absolute, out uri) && uri.Scheme == "https" && (uri.Host.Equals("img.itch.zone", StringComparison.OrdinalIgnoreCase) || uri.Host.Equals("img.itch.io", StringComparison.OrdinalIgnoreCase)) && String.IsNullOrEmpty(uri.UserInfo) && String.IsNullOrEmpty(uri.Fragment) && uri.IsDefaultPort;
        }

        internal static string Cached(string root, ItchReceiptGame game, bool legacy = false)
        {
            if (game == null || !PlatformLibraries.Id(game.id.ToString()) || ItchMetadata.Cover(game) == null)
                return null;
            string hash;
            using (var sha = SHA256.Create())
                hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(ItchMetadata.Cover(game, legacy) + (legacy ? "" : "|animated-v1")))).Replace("-", "").Substring(0, 20);
            return Path.Combine(root, game.id + "-" + hash + (legacy ? ".png" : ".image"));
        }

        internal static string Resolve(CustomGame game, string root, CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();
            var metadata = ItchMetadata.Find(game);
            string path = Cached(root, metadata);
            if (path == null)
                return null;
            if (File.Exists(path))
            {
                try
                {
                    using (var image = ArtworkAnimation.Open(path))
                    {
                    }

                    return path;
                }
                catch (ArgumentException)
                {
                }
                catch (IOException)
                {
                }
            }

            Directory.CreateDirectory(root);
            byte[] bytes;
            try
            {
                bytes = Download(ItchMetadata.Cover(metadata), cancel);
            }
            catch (WebException)
            {
                cancel.ThrowIfCancellationRequested();
                string old = Cached(root, metadata, true);
                if (old != null && File.Exists(old))
                {
                    using (var image = Banner.ReadArtwork(old))
                    {
                    }

                    return old;
                }

                throw;
            }

            string temp = path + "." + Guid.NewGuid().ToString("N") + ".partial";
            try
            {
                WriteCover(bytes, temp);
                cancel.ThrowIfCancellationRequested();
                if (new FileInfo(temp).Length > 8 * 1024 * 1024)
                    throw new IOException("Decoded itch image too large");
                SteamArtworkCache.Trim(root, path, new FileInfo(temp).Length, SteamArtworkCache.Budget, @"^[0-9]{1,12}-[A-F0-9]{20}\.(png|image)$");
                if (File.Exists(path))
                    File.Delete(path);
                File.Move(temp, path);
                return path;
            }
            finally
            {
                if (File.Exists(temp))
                    File.Delete(temp);
            }
        }

        internal static void WriteCover(byte[] bytes, string temp)
        {
            if (bytes == null || bytes.Length > 8 * 1024 * 1024)
                throw new IOException("Cover too large");
            using (var animation = ArtworkAnimation.FromBytes(bytes))
            {
                var image = animation.Image;
                if (image.Width < 8 || image.Height < 8 || image.Width > 4096 || image.Height > 4096)
                    throw new IOException("Unsupported itch cover dimensions");
                if (animation.Animated)
                {
                    File.WriteAllBytes(temp, bytes);
                    return;
                }

                double scale = Math.Min(1, 1200D / Math.Max(image.Width, image.Height));
                using (var bitmap = new Bitmap(Math.Max(1, (int)(image.Width * scale)), Math.Max(1, (int)(image.Height * scale)), PixelFormat.Format32bppArgb))
                {
                    using (var g = Graphics.FromImage(bitmap))
                    {
                        g.Clear(Color.Transparent);
                        g.DrawImage(image, 0, 0, bitmap.Width, bitmap.Height);
                    }

                    bitmap.Save(temp, ImageFormat.Png);
                }
            }
        }

        static byte[] Download(string address, CancellationToken cancel)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var uri = new Uri(address);
            for (int attempt = 0; attempt < 5; attempt++)
            {
                if (!Address(uri.AbsoluteUri))
                    throw new IOException("Untrusted itch image address");
                cancel.ThrowIfCancellationRequested();
                var request = (HttpWebRequest)WebRequest.Create(uri);
                request.AllowAutoRedirect = false;
                request.Timeout = request.ReadWriteTimeout = 15000;
                request.UserAgent = "Astera/0.1";
                using (cancel.Register(delegate
                {
                    request.Abort();
                }))
                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    int code = (int)response.StatusCode;
                    if (code >= 300 && code < 400)
                    {
                        uri = new Uri(uri, response.Headers["Location"]);
                        continue;
                    }

                    if (code != 200 || response.ContentLength > 8 * 1024 * 1024)
                        throw new IOException("Unexpected itch image response");
                    using (var input = response.GetResponseStream())
                    using (var output = new MemoryStream())
                    {
                        byte[] buffer = new byte[16384];
                        int n;
                        while ((n = input.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            cancel.ThrowIfCancellationRequested();
                            if (output.Length + n > 8 * 1024 * 1024)
                                throw new IOException("itch cover is too large");
                            output.Write(buffer, 0, n);
                        }

                        return output.ToArray();
                    }
                }
            }

            throw new IOException("Too many itch image redirects");
        }
    }

    sealed partial class LibraryForm
    {
        readonly Dictionary<string, string> itchArt = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, Image> itchIcons = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
        readonly HashSet<string> itchPending = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, DateTime> itchChecked = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        readonly SemaphoreSlim itchArtworkGate = new SemaphoreSlim(2);
        readonly CancellationTokenSource itchArtworkStop = new CancellationTokenSource();
        string AutomaticGameBackground(CustomGame game)
        {
            if (game.Platform == "roblox")
                return RobloxBackground(game);
            string steam = AutomaticSteamBackground(game), cover;
            return !String.IsNullOrEmpty(steam) ? steam : itchArt.TryGetValue(game.Executable, out cover) ? cover : null;
        }

        Image AutomaticGameIcon(CustomGame game)
        {
            if (game != null && game.ManagedProjectId == ManagedGames.Id)
                return GameArtwork.NebulaIcon;
            Image cached;
            if (game.Platform == "roblox" && customIcons.TryGetValue(game.RobloxIcon, out cached))
                return cached;
            Image steam = AutomaticSteamIcon(game), image;
            return steam ?? (itchIcons.TryGetValue(game.Executable, out image) ? image : VisibleLibraryIcon(game));
        }

        void ApplyItchArtwork(CustomGame game, string path)
        {
            string old;
            if (itchArt.TryGetValue(game.Executable, out old) && old == path)
                return;
            if (String.IsNullOrEmpty(path))
                return;
            var image = ReadCustomIcon(path);
            Image prior;
            if (itchIcons.TryGetValue(game.Executable, out prior))
                prior.Dispose();
            itchIcons[game.Executable] = image;
            itchArt[game.Executable] = path;
            RegisterIconAnimation(path);
            games.Invalidate();
            if (IsCustomGame && CurrentCustom.Id == game.Id)
                RefreshCustomView();
        }

        void QueuePlatformArtwork(CustomGame game, bool force = false)
        {
            QueueSteamArtwork(game, force);
            QueueItchArtwork(game);
        }

        void StopItchArtwork()
        {
            itchArtworkStop.Cancel();
            if (selectedPublicArtwork != null)
            {
                selectedPublicArtwork.Dispose();
                selectedPublicArtwork = null;
            }

            foreach (var image in itchIcons.Values)
                image.Dispose();
            itchIcons.Clear();
        }
    }
}
