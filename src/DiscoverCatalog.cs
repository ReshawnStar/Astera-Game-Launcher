using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using MC2Pack;

namespace Astera
{
    public sealed class CatalogImage
    {
        public string Resource = "", Url = "", Hash = "", Fit = "";
        public float X = .5f, Y = .5f;
    }

    public sealed class CatalogProject
    {
        public string Id = "", Title = "", Category = "", Summary = "", Description = "", Requirements = "", Notes = "", Status = "", OfficialUrl = "";
        public CatalogImage HeroArtwork;
        public ManagedGamePackage Package;
        public bool Featured;
        public int LibraryKind = -1;
        public CatalogImage Artwork = new CatalogImage();
        public List<CatalogImage> Screenshots = new List<CatalogImage>();
    }

    public sealed class ProjectCatalog
    {
        public int Schema = 1;
        public List<CatalogProject> Projects = new List<CatalogProject>();
    }

    static class DiscoverCatalog
    {
        internal const string FeedUrl = "https://raw.githubusercontent.com/ReshawnStar/Astera-Game-Launcher/main/channels/discover.json";
        internal static ProjectCatalog Bundled()
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Astera.Discover.Catalog"))
            using (var reader = new StreamReader(stream))
                return Parse(reader.ReadToEnd());
        }

        internal static ProjectCatalog Parse(string json)
        {
            if (json == null || json.Length > 256 * 1024)
                throw new IOException("Catalog is too large");
            var catalog = Engine.Json.Deserialize<ProjectCatalog>(json);
            Validate(catalog);
            return catalog;
        }

        internal static void Text(string value, int max, bool required = false)
        {
            if (value == null || value.Length > max || required && String.IsNullOrWhiteSpace(value) || value.Any(c => Char.IsControl(c) && c != '\n' && c != '\r' && c != '\t'))
                throw new IOException("Invalid catalog text");
        }

        internal static void Image(CatalogImage image)
        {
            if (image == null)
                throw new IOException("Missing catalog image");
            Text(image.Resource, 100);
            if (image.Fit != "" && image.Fit != "cover" && image.Fit != "contain" || Single.IsNaN(image.X) || Single.IsNaN(image.Y) || image.X < 0 || image.X > 1 || image.Y < 0 || image.Y > 1)
                throw new IOException("Invalid artwork positioning");
            if (image.Resource.Length > 0 && !new[]
            {
                "Astera.Backgrounds.MinecraftSquared",
                "Astera.Backgrounds.StarDash",
                "Astera.Backgrounds.FtlMultiverse",
                "Astera.Backgrounds.GachaNebulaPlus",
                "Astera.Games.LastLightLogo",
                "Astera.Games.LastLightBanner"
            }.Contains(image.Resource))
                throw new IOException("Unknown catalog artwork");
            if (!String.IsNullOrEmpty(image.Url))
            {
                if (image.Resource.Length > 0)
                    throw new IOException("Choose either embedded or remote artwork");
                new Transport().Address(image.Url);
                if (!Regex.IsMatch(image.Hash ?? "", "\\A[0-9a-fA-F]{64}\\z"))
                    throw new IOException("Remote artwork needs a SHA-256 hash");
            }
            else if (!String.IsNullOrEmpty(image.Hash))
                throw new IOException("Artwork hash has no URL");
        }

        internal static void Validate(ProjectCatalog catalog)
        {
            if (catalog == null || catalog.Schema != 1 || catalog.Projects == null || catalog.Projects.Count < 1 || catalog.Projects.Count > 80)
                throw new IOException("Unsupported catalog");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in catalog.Projects)
            {
                if (p == null || !Regex.IsMatch(p.Id ?? "", "\\A[a-z0-9][a-z0-9-]{0,63}\\z") || !ids.Add(p.Id))
                    throw new IOException("Invalid or duplicate project ID");
                Text(p.Title, 100, true);
                Text(p.Category, 40, true);
                Text(p.Summary, 220);
                Text(p.Description, 8000);
                Text(p.Requirements, 4000);
                Text(p.Notes, 16000);
                if (p.Status != "available" && p.Status != "coming-soon")
                    throw new IOException("Invalid availability");
                int kind = p.Id == "minecraft-squared" ? 0 : p.Id == "star-dash" ? 1 : p.Id == "ftl-multiverse" ? 2 : -1;
                if (p.Package != null)
                {
                    if (p.Id != ManagedGames.Id || p.Status != "available")
                        throw new IOException("Unsupported managed game");
                    ManagedGames.Validate(p.Package);
                }

                if (p.LibraryKind != kind)
                    throw new IOException("Catalog cannot introduce an executable or installation route");
                if (!String.IsNullOrEmpty(p.OfficialUrl))
                    new Transport().Address(p.OfficialUrl);
                Image(p.Artwork);
                if (p.HeroArtwork != null)
                    Image(p.HeroArtwork);
                if (p.Screenshots == null || p.Screenshots.Count > 8)
                    throw new IOException("Too many screenshots");
                foreach (var image in p.Screenshots)
                    Image(image);
            }

            if (catalog.Projects.SelectMany(p => new[] { p.Artwork, p.HeroArtwork }.Where(i => i != null).Concat(p.Screenshots)).Where(i => !String.IsNullOrEmpty(i.Url)).Select(i => i.Hash.ToLowerInvariant()).Distinct().Count() > 32)
                throw new IOException("Catalog supports at most 32 distinct remote images");
        }

        internal static ProjectCatalog Verify(byte[] bytes, string key)
        {
            if (bytes == null || bytes.Length > 512 * 1024)
                throw new IOException("Catalog envelope is too large");
            var envelope = Engine.Json.Deserialize<Envelope>(Encoding.UTF8.GetString(bytes));
            if (envelope == null)
                throw new IOException("Missing catalog signature");
            var payload = Convert.FromBase64String(envelope.Payload);
            if (payload.Length > 256 * 1024)
                throw new IOException("Catalog is too large");
            using (var rsa = new RSACryptoServiceProvider(2048, new CspParameters(24)))
            {
                rsa.PersistKeyInCsp = false;
                rsa.FromXmlString(key);
                if (!rsa.VerifyData(payload, "SHA256", Convert.FromBase64String(envelope.Signature)))
                    throw new IOException("Catalog signature does not match Astera");
            }

            return Parse(Encoding.UTF8.GetString(payload));
        }

        internal static string ImageKey(CatalogImage image)
        {
            return String.IsNullOrEmpty(image.Url) ? image.Resource : image.Hash.ToLowerInvariant();
        }

        internal static Bitmap ReadRemoteImage(CatalogImage image, string cache, CancellationToken stop)
        {
            Image(image);
            if (String.IsNullOrEmpty(image.Url))
                throw new IOException("Remote artwork URL missing");
            Directory.CreateDirectory(cache);
            string path = Path.Combine(cache, image.Hash.ToLowerInvariant() + ".image");
            if (File.Exists(path) && new FileInfo(path).Length <= 8 * 1024 * 1024 && Engine.Hash(path).Equals(image.Hash, StringComparison.OrdinalIgnoreCase))
                return ReadImage(File.ReadAllBytes(path));
            using (var stream = new MemoryStream())
            {
                new Transport().Get(image.Url, stream, 8 * 1024 * 1024, -1, stop, null);
                var bytes = stream.ToArray();
                string hash;
                using (var sha = SHA256.Create())
                    hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
                if (!hash.Equals(image.Hash, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Catalog artwork hash does not match");
                var bitmap = ReadImage(bytes);
                try
                {
                    File.WriteAllBytes(path, bytes);
                }
                catch
                {
                    bitmap.Dispose();
                    throw;
                }

                return bitmap;
            }
        }

        static Bitmap ReadImage(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            using (var image = System.Drawing.Image.FromStream(stream))
            {
                if (image.Width > 4096 || image.Height > 4096 || (long)image.Width * image.Height > 12000000)
                    throw new IOException("Catalog artwork is too large");
                float scale = Math.Min(1f, Math.Min(1200f / image.Width, 700f / image.Height));
                return new Bitmap(image, new Size(Math.Max(1, (int)(image.Width * scale)), Math.Max(1, (int)(image.Height * scale))));
            }
        }
    }
}
