using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using MC2Pack;

namespace Astera
{
    static partial class Multiverse
    {
        // Original addon download from the Multiverse translation installer's
        // author-maintained catalogue. Pin the actual inspected 1.4.2 archive;
        // never silently accept changed bytes from this mutable Drive link.
        internal static FtlAsset GibsAsset()
        {
            return new FtlAsset
            {
                Name = "MV Addon GenGibs v1.4.2.ftl",
                Id = "mv55-gengibs-1.4.2",
                Size = 69907868,
                Url = "https://drive.usercontent.google.com/download?id=1JFSm0418yTVRloTiQLFM5E7Wvwu6w4Gi&export=download&confirm=t",
                Sha256 = "B8A616A8609B31951B4BCB71743AB9E29A8699BDFFAD12022E1029B3A91D139F"
            };
        }

        internal static void SelectGibs(FtlPlan plan, bool enabled)
        {
            plan.Gibs = null;
            plan.GibsVersion = null;
            if (!enabled)
                return;
            var version = VersionOf(plan.DataVersion);
            if (version.Major != 5 || version.Minor != 5)
                throw new IOException("Generated Gibs 1.4.2 supports Multiverse 5.5 only. Turn off ship breakup effects in FTL settings until a compatible addon is available.");
            plan.Gibs = GibsAsset();
            plan.GibsVersion = "1.4.2";
        }

        internal static IEnumerable<FtlAsset> Payloads(FtlPlan plan)
        {
            return new[]
            {
                plan.Assets,
                plan.Data,
                plan.Tool,
                plan.Trc,
                plan.Gibs,
                plan.TrcGibs
            }.Where(a => a != null);
        }

        internal static bool NeedsUpdate(FtlState installed, FtlPlan plan)
        {
            return installed == null || installed.DataVersion != plan.DataVersion || installed.AssetsVersion != plan.AssetsVersion || installed.HyperspaceVersion != plan.HyperspaceVersion || !String.Equals(installed.GibsVersion, plan.GibsVersion, StringComparison.Ordinal) || installed.TrcVersion != plan.TrcVersion || installed.TrcGibsVersion != plan.TrcGibsVersion;
        }

        internal static string[] PatchArguments(FtlPlan plan)
        {
            var arguments = new List<string>
            {
                "patch",
                "--data-dir",
                plan.Stage,
                plan.Downloads[plan.Assets.Name],
                plan.Downloads[plan.Data.Name]
            };
            if (plan.Trc != null)
                arguments.Add(plan.Downloads[plan.Trc.Name]);
            if (plan.TrcGibs != null && plan.Trc == null)
                throw new IOException("TRC debris requires The Renegade Collection");
            if (plan.Gibs != null)
                arguments.Add(plan.Downloads[plan.Gibs.Name]);
            if (plan.TrcGibs != null)
                arguments.Add(plan.Downloads[plan.TrcGibs.Name]);
            return arguments.ToArray();
        }

        static HashSet<string> GibOverrides(FtlPlan plan)
        {
            if (plan.Gibs == null)
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var zip = ZipFile.OpenRead(plan.Downloads[plan.Gibs.Name]))
                return new HashSet<string>(zip.Entries.Select(e => e.FullName), StringComparer.OrdinalIgnoreCase);
        }

        internal static void VerifyGibs(FtlPlan plan, HashSet<string> names)
        {
            VerifyGibsArchive(plan, names, plan.Gibs, plan.GibsVersion);
            VerifyGibsArchive(plan, names, plan.TrcGibs, plan.TrcGibsVersion);
        }

        static void VerifyGibsArchive(FtlPlan plan, HashSet<string> names, FtlAsset addon, string version)
        {
            if (addon == null)
                return;
            string archive = plan.Downloads[addon.Name];
            if (new FileInfo(archive).Length != addon.Size || !Engine.Hash(archive).Equals(addon.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Generated Gibs archive differs from the pinned release");
            string data = Path.Combine(plan.Stage, "ftl.dat");
            var index = ContentIndex(data);
            int images = 0, layouts = 0;
            using (var zip = ZipFile.OpenRead(archive))
            using (var patched = File.OpenRead(data))
            {
                if (MetadataVersion(zip) != version)
                    throw new IOException("Generated Gibs metadata does not match the selected version");
                foreach (var entry in zip.Entries)
                {
                    string name = entry.FullName;
                    if (entry.Name.Length == 0 || name.StartsWith("mod-appendix/", StringComparison.Ordinal))
                        continue;
                    if (name.StartsWith("img/", StringComparison.Ordinal) && name.IndexOf("_gib", StringComparison.OrdinalIgnoreCase) >= 0 && name.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!names.Contains(name))
                            throw new IOException("Generated ship debris is missing: " + name);
                        using (var expected = entry.Open())
                            if (Engine.Hash(expected) != Engine.Hash(Resource(patched, index[name], 64 * 1024 * 1024)))
                                throw new IOException("Generated ship debris was not patched correctly: " + name);
                        images++;
                    }
                    else if (name.StartsWith("data/", StringComparison.Ordinal) && name.EndsWith(".xml.append", StringComparison.OrdinalIgnoreCase))
                    {
                        string target = name.Substring(0, name.Length - 7);
                        if (!names.Contains(target))
                            throw new IOException("Generated Gibs layout target is missing: " + target);
                        string source;
                        using (var reader = new StreamReader(entry.Open()))
                            source = reader.ReadToEnd();
                        string actual = Encoding.UTF8.GetString(Resource(patched, index[target], 4 * 1024 * 1024));
                        var replacements = Regex.Matches(source, @"<mod-overwrite:(gib\d+)>(.*?)</mod-overwrite:\1>", RegexOptions.Singleline);
                        if (replacements.Count == 0)
                            throw new IOException("Generated Gibs layout has no recognised debris changes: " + target);
                        foreach (Match replacement in replacements)
                        {
                            var current = Regex.Match(actual, "<" + replacement.Groups[1].Value + @"\b[^>]*>(.*?)</" + replacement.Groups[1].Value + ">", RegexOptions.Singleline);
                            if (!current.Success || !SameGib(replacement.Groups[2].Value, current.Groups[1].Value))
                                throw new IOException("Generated Gibs layout was not patched correctly: " + target + " / " + replacement.Groups[1].Value);
                        }

                        layouts++;
                    }
                    else
                        throw new IOException("Unexpected Generated Gibs payload: " + name);
                }
            }

            if (images == 0 || layouts == 0)
                throw new IOException("Generated Gibs contains no verified images/layouts");
        }

        static bool SameGib(string expected, string actual)
        {
            var left = GibFields(expected);
            var right = GibFields(actual);
            return left.All(p => right.ContainsKey(p.Key) && right[p.Key] == p.Value);
        }

        static Dictionary<string, string> GibFields(string fragment)
        {
            var document = new XmlDocument
            {
                XmlResolver = null
            };
            document.LoadXml("<gib>" + fragment + "</gib>");
            var fields = new Dictionary<string, string>();
            foreach (XmlNode node in document.DocumentElement.ChildNodes)
                if (node.NodeType == XmlNodeType.Element)
                {
                    fields[node.Name + "/#text"] = node.InnerText.Trim();
                    foreach (XmlAttribute attribute in node.Attributes)
                        fields[node.Name + "/" + attribute.Name] = attribute.Value;
                }

            return fields;
        }
    }
}
