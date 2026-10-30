using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using MC2Pack;

namespace Astera
{
    static class CurseForgeSetup
    {
        internal static void Create(Release release, string path)
        {
            Engine.Validate(release);
            if (!System.Text.RegularExpressions.Regex.IsMatch(release.Minecraft ?? "", "\\A[0-9A-Za-z.+_-]{1,80}\\z") || !System.Text.RegularExpressions.Regex.IsMatch(release.Loader ?? "", "\\A[0-9]+\\.[0-9]+\\.[0-9]+\\z"))
                throw new IOException("Unsupported Minecraft runtime version");
            PrismProfiles.SafePath(path);
            var manifest = new
            {
                minecraft = new
                {
                    version = release.Minecraft,
                    modLoaders = new[]
                    {
                        new
                        {
                            id = "fabric-" + release.Loader,
                            primary = true
                        }
                    }
                },
                manifestType = "minecraftModpack",
                manifestVersion = 1,
                name = "Minecraft Squared",
                version = release.Name,
                author = "Star",
                files = new object[0],
                overrides = "overrides"
            };
            using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            using (var zip = new ZipArchive(file, ZipArchiveMode.Create))
            {
                using (var writer = new StreamWriter(zip.CreateEntry("manifest.json").Open(), new UTF8Encoding(false)))
                    writer.Write(Engine.Json.Serialize(manifest));
                zip.CreateEntry("overrides/");
            }
        }
    }
}
