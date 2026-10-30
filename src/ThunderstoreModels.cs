using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using MC2Pack;

namespace Astera
{
    public sealed class ModProfileLink
    {
        public bool UseManaged = true;
        public string Id = Guid.NewGuid().ToString("N"), Name = "", Source = "", Manager = "", Managed = "", ManagedId = "", Code = "";
    }

    public sealed class ThunderGame
    {
        public string Slug, Name, Folder, SteamId, Loader;
        public string[] Executables;
    }

    public sealed class ThunderLoader
    {
        public string packageId, rootFolder, loader;
    }

    public sealed class ThunderCatalog
    {
        public string Source, Version;
        public ThunderGame[] Games;
        public ThunderLoader[] Loaders;
    }

    public sealed class ThunderMod
    {
        public string Name, Version;
        public bool Enabled = true;
    }

    public sealed class ThunderExport
    {
        public string Name;
        public List<ThunderMod> Mods = new List<ThunderMod>();
    }

    static class ThunderstoreProfiles
    {
        static ThunderCatalog catalog;
        internal static ThunderCatalog Catalog
        {
            get
            {
                if (catalog == null)
                    using (var reader = new StreamReader(typeof(ThunderstoreProfiles).Assembly.GetManifestResourceStream("Astera.Thunderstore.Games")))
                        catalog = Engine.Json.Deserialize<ThunderCatalog>(reader.ReadToEnd());
                return catalog;
            }
        }

        internal static ThunderGame Game(string appId)
        {
            return Catalog.Games.FirstOrDefault(g => g.SteamId == appId);
        }

        internal static bool PackageId(string name)
        {
            return name != null && Regex.IsMatch(name, "^[A-Za-z0-9_]{1,128}-[A-Za-z0-9_]{1,128}$");
        }

        internal static string Code(string input)
        {
            Guid code;
            if (!Guid.TryParseExact((input ?? "").Trim(), "D", out code))
                throw new IOException("Paste the profile share code from Thunderstore or r2modman.");
            return code.ToString("D");
        }

        internal static IEnumerable<string> Roots(IEnumerable<string> configured = null)
        {
            string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var roots = new List<string>
            {
                Path.Combine(roaming, "r2modmanPlus-local"),
                Path.Combine(roaming, "Thunderstore Mod Manager", "DataFolder")
            };
            if (configured != null)
                roots.AddRange(configured.Where(p => !String.IsNullOrWhiteSpace(p)));
            return roots.Distinct(StringComparer.OrdinalIgnoreCase);
        }

        internal static List<ModProfileLink> Discover(ThunderGame game, IEnumerable<string> roots)
        {
            var found = new List<ModProfileLink>();
            if (game == null)
                return found;
            foreach (string root in roots.Take(24))
                try
                {
                    SelfUpdateFiles.Plain(root);
                    string profiles = SelfUpdateFiles.Inside(SelfUpdateFiles.Root(root), game.Folder + "/profiles");
                    if (!Directory.Exists(profiles))
                        continue;
                    foreach (string path in Directory.EnumerateDirectories(profiles).Take(1000))
                    {
                        try
                        {
                            SelfUpdateFiles.Plain(path);
                            string manifest = Path.Combine(path, "mods.yml");
                            if (!File.Exists(manifest) || new FileInfo(manifest).Length > 8 * 1024 * 1024)
                                continue;
                            found.Add(new ModProfileLink { Name = Path.GetFileName(path), Source = Path.GetFullPath(path), Manager = root.IndexOf("Thunderstore", StringComparison.OrdinalIgnoreCase) >= 0 ? "Thunderstore" : "r2modman", Id = "manager-" + Engine.Hash(Encoding.UTF8.GetBytes(Path.GetFullPath(path).ToLowerInvariant())) });
                        }
                        catch (IOException)
                        {
                        }
                        catch (UnauthorizedAccessException)
                        {
                        }
                    }
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
                catch (ArgumentException)
                {
                }

            return found.GroupBy(p => p.Source, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ThenBy(p => p.Manager).ToList();
        }

        internal static string Active(ModProfileLink link)
        {
            return !link.UseManaged || String.IsNullOrEmpty(link.Managed) ? link.Source : link.Managed;
        }

        internal static void Merge(CustomGame game, IEnumerable<ModProfileLink> found)
        {
            if (game.ModProfiles == null)
                game.ModProfiles = new List<ModProfileLink>();
            foreach (var link in found)
                if (!game.ModProfiles.Any(p => p.Id == link.Id || String.Equals(p.Source, link.Source, StringComparison.OrdinalIgnoreCase)))
                    game.ModProfiles.Add(link);
            game.ModProfiles = game.ModProfiles.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ThenBy(p => p.Manager).ToList();
        }
    }

    static class ThunderYaml
    {
        static bool registered;
        internal static void Register()
        {
            if (registered)
                return;
            registered = true;
            AppDomain.CurrentDomain.AssemblyResolve += delegate (object sender, ResolveEventArgs args)
            {
                if (new AssemblyName(args.Name).Name != "YamlDotNet")
                    return null;
                using (var source = typeof(ThunderYaml).Assembly.GetManifestResourceStream("Astera.YamlDotNet"))
                using (var data = new MemoryStream())
                {
                    if (source == null)
                        throw new IOException("Profile reader is missing");
                    source.CopyTo(data);
                    var bytes = data.ToArray();
                    if (Engine.Hash(bytes) != ThunderYamlIdentity.Sha256)
                        throw new IOException("Profile reader checksum mismatch");
                    return Assembly.Load(bytes);
                }
            };
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        internal static ThunderExport Parse(string text)
        {
            if (text == null || text.Length > 1024 * 1024)
                throw new IOException("The profile's mod list is too large");
            var parser = new YamlDotNet.Core.Parser(new StringReader(text));
            int events = 0;
            while (parser.MoveNext())
            {
                if (++events > 60000 || parser.Current is YamlDotNet.Core.Events.AnchorAlias)
                    throw new IOException("Profile aliases or excessive metadata are not supported");
            }

            var stream = new YamlDotNet.RepresentationModel.YamlStream();
            stream.Load(new StringReader(text));
            if (stream.Documents.Count != 1)
                throw new IOException("Expected one profile document");
            var root = stream.Documents[0].RootNode as YamlDotNet.RepresentationModel.YamlMappingNode;
            if (root == null)
                throw new IOException("Invalid profile document");
            var result = new ThunderExport
            {
                Name = Scalar(root, "profileName")
            };
            if (String.IsNullOrWhiteSpace(result.Name) || result.Name.Length > 100 || result.Name.Any(Char.IsControl))
                throw new IOException("Invalid profile name");
            YamlDotNet.RepresentationModel.YamlNode mods;
            if (!root.Children.TryGetValue(new YamlDotNet.RepresentationModel.YamlScalarNode("mods"), out mods) || !(mods is YamlDotNet.RepresentationModel.YamlSequenceNode))
                throw new IOException("Profile mod list is missing");
            foreach (var node in ((YamlDotNet.RepresentationModel.YamlSequenceNode)mods).Children)
            {
                var item = node as YamlDotNet.RepresentationModel.YamlMappingNode;
                if (item == null || result.Mods.Count >= 1000)
                    throw new IOException("Invalid profile mod entry");
                string name = Scalar(item, "name");
                if (!ThunderstoreProfiles.PackageId(name) || result.Mods.Any(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                    throw new IOException("Invalid or duplicate package name");
                YamlDotNet.RepresentationModel.YamlNode version;
                var key = new YamlDotNet.RepresentationModel.YamlScalarNode("version");
                if (!item.Children.TryGetValue(key, out version) || !(version is YamlDotNet.RepresentationModel.YamlMappingNode))
                    throw new IOException("Missing package version");
                var mapping = (YamlDotNet.RepresentationModel.YamlMappingNode)version;
                string v = Number(mapping, "major") + "." + Number(mapping, "minor") + "." + Number(mapping, "patch");
                string enabled = Scalar(item, "enabled", false);
                bool active;
                if (enabled == null)
                    active = true;
                else if (!Boolean.TryParse(enabled, out active))
                    throw new IOException("Invalid package enabled state");
                result.Mods.Add(new ThunderMod { Name = name, Version = v, Enabled = active });
            }

            return result;
        }

        static string Scalar(YamlDotNet.RepresentationModel.YamlMappingNode node, string field, bool required = true)
        {
            YamlDotNet.RepresentationModel.YamlNode value;
            if (!node.Children.TryGetValue(new YamlDotNet.RepresentationModel.YamlScalarNode(field), out value))
            {
                if (required)
                    throw new IOException("Missing profile field: " + field);
                return null;
            }

            var scalar = value as YamlDotNet.RepresentationModel.YamlScalarNode;
            if (scalar == null)
                throw new IOException("Invalid profile field: " + field);
            return scalar.Value;
        }

        static int Number(YamlDotNet.RepresentationModel.YamlMappingNode node, string key)
        {
            int n;
            if (!Int32.TryParse(Scalar(node, key), out n) || n < 0 || n > 999999)
                throw new IOException("Invalid package version");
            return n;
        }
    }
}
