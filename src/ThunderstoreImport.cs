using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using MC2Pack;

namespace Astera
{
    sealed class ThunderVersion
    {
        public string @namespace = "", name = "", version_number = "", download_url = "";
        public string[] dependencies = new string[0];
    }

    sealed class ThunderListing
    {
        public string @namespace = "", name = "";
        public ThunderCommunity[] community_listings = new ThunderCommunity[0];
    }

    sealed class ThunderCommunity
    {
        public string community = "", review_status = "";
    }

    sealed class ThunderImportPlan
    {
        internal string Code, Archive, Workspace, MetadataRoot;
        internal ThunderExport Export;
        internal List<ThunderVersion> Packages = new List<ThunderVersion>();
    }

    static class ThunderstoreImport
    {
        static readonly object NetworkGate = new object();
        static DateTime nextMetadataUtc;
        static byte[] Metadata(Transport network, string url, string root, CancellationToken stop)
        {
            root = SelfUpdateFiles.Root(root);
            Directory.CreateDirectory(root);
            string file = SelfUpdateFiles.Inside(root, Engine.Hash(Encoding.UTF8.GetBytes(url)) + ".json");
            if (File.Exists(file) && new FileInfo(file).Length <= 8 * 1024 * 1024 && File.GetLastWriteTimeUtc(file) > DateTime.UtcNow.AddHours(-1))
                return File.ReadAllBytes(file);
            var data = Retry(delegate
            {
                int delay;
                lock (NetworkGate)
                {
                    delay = Math.Max(0, (int)(nextMetadataUtc - DateTime.UtcNow).TotalMilliseconds);
                    nextMetadataUtc = DateTime.UtcNow.AddMilliseconds(delay + 1100);
                }

                if (delay > 0 && stop.WaitHandle.WaitOne(delay))
                    stop.ThrowIfCancellationRequested();
                return network.Metadata(url, stop);
            }, stop);
            string pending = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllBytes(pending, data);
                if (File.Exists(file))
                    File.Replace(pending, file, null);
                else
                    File.Move(pending, file);
            }
            finally
            {
                if (File.Exists(pending))
                    File.Delete(pending);
            }

            return data;
        }

        internal static T Retry<T>(Func<T> action, CancellationToken stop)
        {
            for (int attempt = 0; ; attempt++)
            {
                stop.ThrowIfCancellationRequested();
                try
                {
                    return action();
                }
                catch (WebException error)
                {
                    var response = error.Response as HttpWebResponse;
                    int status = response == null ? 0 : (int)response.StatusCode;
                    bool retry = status == 429 || status == 500 || status == 502 || status == 503 || status == 504 || response == null && (error.Status == WebExceptionStatus.Timeout || error.Status == WebExceptionStatus.ConnectFailure || error.Status == WebExceptionStatus.ReceiveFailure || error.Status == WebExceptionStatus.ConnectionClosed);
                    int delay = 1000 * (attempt + 1);
                    if (status == 429)
                    {
                        delay = 60000;
                        int seconds;
                        if (response != null && Int32.TryParse(response.Headers["Retry-After"], out seconds))
                        {
                            if (seconds > 120)
                            {
                                response.Dispose();
                                throw;
                            }

                            delay = Math.Max(1000, seconds * 1000);
                        }
                        else if (response != null)
                        {
                            DateTimeOffset until;
                            if (DateTimeOffset.TryParse(response.Headers["Retry-After"], out until))
                            {
                                double wait = (until.UtcDateTime - DateTime.UtcNow).TotalMilliseconds;
                                if (wait > 120000)
                                {
                                    response.Dispose();
                                    throw;
                                }

                                delay = (int)Math.Max(1000, wait);
                            }
                        }
                    }

                    if (response != null)
                        response.Dispose();
                    if (!retry || attempt >= 2)
                        throw;
                    if (stop.WaitHandle.WaitOne(delay))
                        stop.ThrowIfCancellationRequested();
                }
            }
        }

        internal static string Relative(string path)
        {
            path = path.Replace('\\', '/');
            if (path.EndsWith("/"))
                path = path.TrimEnd('/');
            SelfUpdateFiles.Inside(Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar), path);
            return path;
        }

        internal static byte[] ReadEntry(ZipArchiveEntry entry, long limit)
        {
            if (entry.Length > limit || entry.Length < 0)
                throw new IOException("Archive entry is too large");
            using (var input = entry.Open())
            using (var output = new MemoryStream())
            {
                byte[] buffer = new byte[65536];
                int count;
                long size = 0;
                while ((count = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    size += count;
                    if (size > limit || size > entry.Length)
                        throw new IOException("Archive expanded beyond its limit");
                    output.Write(buffer, 0, count);
                }

                if (size != entry.Length)
                    throw new IOException("Truncated archive entry");
                return output.ToArray();
            }
        }

        internal static void InspectZip(ZipArchive zip, long maximum)
        {
            long expanded = 0;
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (zip.Entries.Count > 30000)
                throw new IOException("Too many archive entries");
            foreach (var entry in zip.Entries)
            {
                string name = Relative(entry.FullName);
                if (!names.Add(name) || ((entry.ExternalAttributes >> 16) & 0xf000) == 0xa000)
                    throw new IOException("Duplicate or redirected archive entry");
                if (entry.Length > maximum || expanded > maximum - entry.Length)
                    throw new IOException("Archive expansion exceeds its limit");
                expanded += entry.Length;
            }
        }

        internal static ThunderImportPlan ReadArchive(string archive, string workspace, string code)
        {
            using (var zip = ZipFile.OpenRead(archive))
            {
                InspectZip(zip, 256L * 1024 * 1024);
                var metadata = zip.Entries.SingleOrDefault(e => Relative(e.FullName) == "export.r2x");
                if (metadata == null)
                    throw new IOException("This code does not contain an exported profile");
                var export = ThunderYaml.Parse(Encoding.UTF8.GetString(ReadEntry(metadata, 1024 * 1024)));
                return new ThunderImportPlan
                {
                    Archive = archive,
                    Workspace = workspace,
                    Code = code,
                    Export = export
                };
            }
        }

        internal static ThunderImportPlan Download(string code, string workspace, CancellationToken stop, Action<string, long, long> progress, ThunderGame game, string metadataRoot = null)
        {
            code = ThunderstoreProfiles.Code(code);
            SelfUpdateFiles.Plain(workspace);
            Directory.CreateDirectory(workspace);
            string archive = Path.Combine(workspace, "profile.r2z");
            var network = new Transport();
            byte[] response;
            using (var memory = new MemoryStream())
            {
                Retry(delegate
                {
                    memory.SetLength(0);
                    network.Get("https://thunderstore.io/api/experimental/legacyprofile/get/" + code + "/", memory, 64 * 1024 * 1024, -1, stop, null);
                    return true;
                }, stop);
                response = memory.ToArray();
            }

            string text = Encoding.UTF8.GetString(response);
            if (!text.StartsWith("#r2modman", StringComparison.Ordinal))
                throw new IOException("The share code returned an invalid profile");
            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(text.Substring(9).Trim());
            }
            catch (FormatException)
            {
                throw new IOException("The share code returned invalid profile data");
            }

            if (bytes.Length > 48 * 1024 * 1024)
                throw new IOException("The shared profile is too large");
            File.WriteAllBytes(archive, bytes);
            var plan = ReadArchive(archive, workspace, code);
            plan.MetadataRoot = metadataRoot ?? Path.Combine(workspace, "metadata");
            Resolve(plan, stop, progress, null, game);
            return plan;
        }

        internal static void Resolve(ThunderImportPlan plan, CancellationToken stop, Action<string, long, long> progress, Func<string, byte[]> fetch = null, ThunderGame game = null)
        {
            var pending = new Queue<ThunderMod>(plan.Export.Mods.Where(m => m.Enabled));
            var chosen = plan.Export.Mods.ToDictionary(m => m.Name, StringComparer.OrdinalIgnoreCase);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var network = new Transport();
            while (pending.Count > 0)
            {
                stop.ThrowIfCancellationRequested();
                if (seen.Count >= 1000)
                    throw new IOException("Profile has too many dependencies");
                var mod = pending.Dequeue();
                if (!seen.Add(mod.Name))
                    continue;
                string[] id = mod.Name.Split('-');
                string url = "https://thunderstore.io/api/experimental/package/" + id[0] + "/" + id[1] + "/" + mod.Version + "/";
                progress("Checking " + mod.Name, seen.Count, 0);
                var version = Engine.Json.Deserialize<ThunderVersion>(Encoding.UTF8.GetString((fetch == null ? Metadata(network, url, plan.MetadataRoot ?? Path.Combine(plan.Workspace, "metadata"), stop) : fetch(url))));
                string listingUrl = "https://thunderstore.io/api/experimental/package/" + id[0] + "/" + id[1] + "/";
                var listing = Engine.Json.Deserialize<ThunderListing>(Encoding.UTF8.GetString(fetch == null ? Metadata(network, listingUrl, plan.MetadataRoot ?? Path.Combine(plan.Workspace, "metadata"), stop) : fetch(listingUrl)));
                if (game == null || listing == null || listing.@namespace != id[0] || listing.name != id[1] || !(listing.community_listings ?? new ThunderCommunity[0]).Any(c => c.community == game.Slug && c.review_status != "rejected"))
                    throw new IOException("Package is not listed for " + (game == null ? "this game" : game.Name) + ": " + mod.Name);
                if (version == null || version.@namespace != id[0] || version.name != id[1] || version.version_number != mod.Version || version.download_url != "https://thunderstore.io/package/download/" + id[0] + "/" + id[1] + "/" + mod.Version + "/")
                    throw new IOException("Package identity does not match the selected profile");
                plan.Packages.Add(version);
                foreach (string dependency in version.dependencies ?? new string[0])
                {
                    int split = dependency.LastIndexOf('-');
                    if (split < 0)
                        throw new IOException("Invalid dependency");
                    string name = dependency.Substring(0, split), number = dependency.Substring(split + 1);
                    Version minimum;
                    if (!ThunderstoreProfiles.PackageId(name) || !Version.TryParse(number, out minimum) || minimum.Build < 0)
                        throw new IOException("Invalid dependency version");
                    ThunderMod selected;
                    if (chosen.TryGetValue(name, out selected))
                    {
                        if (!selected.Enabled || new Version(selected.Version) < minimum)
                            throw new IOException("Profile dependency conflict: " + name);
                    }
                    else
                    {
                        selected = new ThunderMod
                        {
                            Name = name,
                            Version = number
                        };
                        chosen.Add(name, selected);
                        plan.Export.Mods.Add(selected);
                    }

                    if (!seen.Contains(name))
                        pending.Enqueue(selected);
                }
            }
        }

        internal static string Install(ThunderImportPlan plan, ThunderGame game, string profilesRoot, string profileId, string prior, CancellationToken stop, Action<string, long, long> progress, Action<string> log, Func<bool> running = null, Action<ThunderVersion, string, CancellationToken> download = null, string cacheRoot = null)
        {
            if (game.Loader != "bepinex")
                throw new IOException("This game's loader needs its own installer; its existing profiles remain available");
            Guid id;
            if (!Guid.TryParseExact(profileId, "N", out id))
                throw new IOException("Invalid managed profile identity");
            profilesRoot = SelfUpdateFiles.Root(profilesRoot);
            Directory.CreateDirectory(profilesRoot);
            string target = SelfUpdateFiles.Inside(profilesRoot, id.ToString("N"));
            string stage = SelfUpdateFiles.Inside(profilesRoot, "s-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stage);
            long total = 0, expanded = 0;
            bool committed = false;
            string backup = null;
            try
            {
                // Personal config absent from the new pack survives. Supplied pack config takes precedence.
                if (!String.IsNullOrEmpty(prior) && Directory.Exists(prior))
                {
                    string config = SelfUpdateFiles.Inside(SelfUpdateFiles.Root(prior), "BepInEx/config");
                    if (Directory.Exists(config))
                        CopyTree(config, SelfUpdateFiles.Inside(stage, "BepInEx/config"), stop, 128L * 1024 * 1024);
                }

                for (int i = 0; i < plan.Packages.Count; i++)
                {
                    stop.ThrowIfCancellationRequested();
                    var package = plan.Packages[i];
                    string name = package.@namespace + "-" + package.name;
                    progress("Preparing mod " + name, i, plan.Packages.Count);
                    string archive = PackageArchive(package, plan.Workspace, cacheRoot, stop, download, log);
                    total += new FileInfo(archive).Length;
                    if (total > 2L * 1024 * 1024 * 1024)
                        throw new IOException("Profile download exceeds 2 GiB");
                    using (var contents = ZipFile.OpenRead(archive))
                    {
                        foreach (var entry in contents.Entries)
                        {
                            expanded += entry.Length;
                            if (expanded > 4L * 1024 * 1024 * 1024)
                                throw new IOException("Expanded profile exceeds 4 GiB");
                        }
                    }

                    InstallPackage(archive, stage, name, package.version_number);
                    log("Installed " + name + " " + package.version_number);
                }

                ApplyExportConfig(plan.Archive, stage);
                stop.ThrowIfCancellationRequested();
                if (!File.Exists(Path.Combine(stage, "BepInEx", "core", "BepInEx.Preloader.dll")) && !File.Exists(Path.Combine(stage, "BepInEx", "core", "BepInEx.Unity.Mono.Preloader.dll")))
                    throw new IOException("The profile does not contain a supported Mono BepInEx loader");
                File.WriteAllText(Path.Combine(stage, "Astera-profile.json"), Engine.Json.Serialize(new
                {
                    Schema = 1,
                    Id = id.ToString("N"),
                    Game = game.SteamId,
                    Profile = plan.Export.Name,
                    ShareCode = plan.Code,
                    Mods = plan.Export.Mods,
                    InstalledUtc = DateTime.UtcNow.ToString("o")
                }));
                if (running != null && running())
                    throw new IOException("The game started during download. Close it and retry; no profile was replaced.");
                if (Directory.Exists(target))
                {
                    SelfUpdateFiles.Plain(target);
                    backup = SelfUpdateFiles.Inside(profilesRoot, id.ToString("N") + ".backup-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6));
                    Directory.Move(target, backup);
                }

                try
                {
                    Directory.Move(stage, target);
                    committed = true;
                }
                catch
                {
                    if (backup != null && !Directory.Exists(target))
                        Directory.Move(backup, target);
                    throw;
                }

                return target;
            }
            finally
            {
                if (!committed && Directory.Exists(stage))
                    log("Unfinished staging retained for inspection: " + stage);
            }
        }

        static string PackageArchive(ThunderVersion package, string workspace, string cacheRoot, CancellationToken stop, Action<ThunderVersion, string, CancellationToken> download, Action<string> log)
        {
            string key = Engine.Hash(Encoding.UTF8.GetBytes(package.@namespace + "-" + package.name + "-" + package.version_number));
            string cache = SelfUpdateFiles.Root(cacheRoot ?? Path.Combine(workspace, "cache"));
            Directory.CreateDirectory(cache);
            string archive = SelfUpdateFiles.Inside(cache, key + ".zip"), receipt = archive + ".sha256";
            if (File.Exists(archive) && File.Exists(receipt) && new FileInfo(receipt).Length < 128 && File.ReadAllText(receipt).Trim() == Engine.Hash(archive))
            {
                log("Using cached " + package.name + " " + package.version_number);
                return archive;
            }

            if (File.Exists(archive))
            {
                SelfUpdateFiles.Plain(archive);
                File.Move(archive, Path.Combine(workspace, "invalid-cache-" + Guid.NewGuid().ToString("N") + ".zip"));
            }

            string pending = archive + "." + Guid.NewGuid().ToString("N") + ".part";
            try
            {
                if (download == null)
                {
                    Retry(delegate
                    {
                        using (var output = new FileStream(pending, FileMode.Create, FileAccess.Write, FileShare.None))
                            new Transport().Get(package.download_url, output, 512L * 1024 * 1024, -1, stop, null);
                        return true;
                    }, stop);
                }
                else
                    download(package, pending, stop);
                using (var zip = ZipFile.OpenRead(pending))
                    InspectZip(zip, 768L * 1024 * 1024);
                string hash = Engine.Hash(pending);
                stop.ThrowIfCancellationRequested();
                File.Move(pending, archive);
                SelfUpdateFiles.Plain(receipt);
                File.WriteAllText(receipt, hash);
                return archive;
            }
            finally
            {
                if (File.Exists(pending))
                {
                    SelfUpdateFiles.Plain(pending);
                    File.Delete(pending);
                }
            }
        }

        internal static void CopyTree(string source, string target, CancellationToken stop, long limit)
        {
            long bytes = 0;
            var pending = new Queue<string>();
            pending.Enqueue(source);
            while (pending.Count > 0)
            {
                string directory = pending.Dequeue();
                SelfUpdateFiles.Plain(directory);
                foreach (string sub in Directory.GetDirectories(directory))
                    pending.Enqueue(sub);
                foreach (string file in Directory.GetFiles(directory))
                {
                    stop.ThrowIfCancellationRequested();
                    SelfUpdateFiles.Plain(file);
                    bytes += new FileInfo(file).Length;
                    if (bytes > limit)
                        throw new IOException("Profile config exceeds its size limit");
                    string relative = file.Substring(source.TrimEnd(Path.DirectorySeparatorChar).Length + 1).Replace('\\', '/');
                    Write(target, relative, File.ReadAllBytes(file), true);
                }
            }
        }

        static void Write(string root, string relative, byte[] bytes, bool replace)
        {
            string destination = SelfUpdateFiles.Inside(SelfUpdateFiles.Root(root), relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            if (File.Exists(destination) && !replace && Engine.Hash(destination) != Engine.Hash(bytes))
                throw new IOException("Conflicting package file: " + relative);
            File.WriteAllBytes(destination, bytes);
        }

        internal static void InstallPackage(string archive, string stage, string name, string version)
        {
            using (var zip = ZipFile.OpenRead(archive))
            {
                InspectZip(zip, 768L * 1024 * 1024);
                var manifest = zip.Entries.SingleOrDefault(e => Relative(e.FullName) == "manifest.json");
                if (manifest == null)
                    throw new IOException("Package manifest missing");
                var data = Engine.Json.Deserialize<Dictionary<string, object>>(Encoding.UTF8.GetString(ReadEntry(manifest, 1024 * 1024)).TrimStart((char)0xfeff));
                object packageName, packageVersion;
                if (data == null || !data.TryGetValue("name", out packageName) || !data.TryGetValue("version_number", out packageVersion) || Convert.ToString(packageName) != name.Split('-')[1] || Convert.ToString(packageVersion) != version)
                    throw new IOException("Downloaded package manifest differs from its announced identity");
                var loader = ThunderstoreProfiles.Catalog.Loaders.FirstOrDefault(l => String.Equals(l.packageId, name, StringComparison.OrdinalIgnoreCase));
                if (loader != null && loader.loader != "bepinex")
                    throw new IOException("Unsupported loader in this profile: " + name);
                foreach (var entry in zip.Entries)
                {
                    if (entry.FullName.EndsWith("/") || entry.FullName.EndsWith("\\"))
                        continue;
                    string path = Relative(entry.FullName);
                    if (loader != null)
                    {
                        string prefix = String.IsNullOrEmpty(loader.rootFolder) ? "" : loader.rootFolder + "/";
                        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                            continue;
                        path = path.Substring(prefix.Length);
                        if (Metadata(path))
                            continue;
                        Write(stage, path, ReadEntry(entry, 256L * 1024 * 1024), false);
                        continue;
                    }

                    if (Metadata(path))
                        continue;
                    if (path.StartsWith("BepInEx/", StringComparison.OrdinalIgnoreCase))
                        path = path.Substring(8);
                    string route = "plugins", tail = path;
                    foreach (string category in new[]
                    {
                        "plugins",
                        "patchers",
                        "core",
                        "monomod",
                        "config"
                    }

                    )
                        if (path.StartsWith(category + "/", StringComparison.OrdinalIgnoreCase))
                        {
                            route = category;
                            tail = path.Substring(category.Length + 1);
                            break;
                        }

                    if (route == "plugins" && tail.EndsWith(".mm.dll", StringComparison.OrdinalIgnoreCase))
                        route = "monomod";
                    string destination = "BepInEx/" + route + "/" + (route == "config" ? "" : name + "/") + tail;
                    if (route == "config" && File.Exists(SelfUpdateFiles.Inside(SelfUpdateFiles.Root(stage), destination)))
                        continue;
                    Write(stage, destination, ReadEntry(entry, 256L * 1024 * 1024), false);
                }
            }
        }

        static bool Metadata(string path)
        {
            return new[]
            {
                "manifest.json",
                "readme.md",
                "icon.png",
                "changelog.md",
                "license",
                "license.md",
                "license.txt"
            }.Contains(path.ToLowerInvariant());
        }

        internal static void ApplyExportConfig(string archive, string stage)
        {
            using (var zip = ZipFile.OpenRead(archive))
            {
                foreach (var entry in zip.Entries)
                {
                    if (entry.FullName.EndsWith("/") || entry.FullName.EndsWith("\\"))
                        continue;
                    string path = Relative(entry.FullName);
                    if (path == "export.r2x" || path == "mods.yml" || path == ".doorstop_version" || path.StartsWith("BepInEx/cache/", StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (path == "doorstop_config.ini")
                    {
                        Write(stage, path, ReadEntry(entry, 128 * 1024), true);
                        continue;
                    }

                    string extension = Path.GetExtension(path).ToLowerInvariant();
                    if (!new[]
                    {
                        ".cfg",
                        ".json",
                        ".toml",
                        ".txt",
                        ".yaml",
                        ".yml",
                        ".ini",
                        ".xml",
                        ".png",
                        ".jpg",
                        ".jpeg",
                        ".ogg",
                        ".wav",
                        ".mp3"
                    }.Contains(extension))
                        throw new IOException("Shared profile contains an unsupported extra file: " + path);
                    if (path.StartsWith("config/", StringComparison.OrdinalIgnoreCase) || path.StartsWith("plugins/", StringComparison.OrdinalIgnoreCase))
                        path = "BepInEx/" + path;
                    else if (!path.StartsWith("BepInEx/config/", StringComparison.OrdinalIgnoreCase) && !path.StartsWith("BepInEx/plugins/", StringComparison.OrdinalIgnoreCase) && !path.StartsWith("BepInEx/patchers/", StringComparison.OrdinalIgnoreCase) && !path.StartsWith("BepInEx/monomod/", StringComparison.OrdinalIgnoreCase))
                        throw new IOException("Shared file is outside supported config/media folders: " + path);
                    Write(stage, path, ReadEntry(entry, 32L * 1024 * 1024), true);
                }
            }
        }
    }
}
