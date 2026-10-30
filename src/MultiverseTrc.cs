using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using MC2Pack;

namespace Astera
{
    static partial class Multiverse
    {
        internal static FtlAsset TrcAsset()
        {
            return new FtlAsset
            {
                Name = "Multiverse_-_TRC_2.6.zip",
                Id = "trc-2.6-original",
                Size = 7536655,
                Url = "https://raw.githubusercontent.com/ftl-mv-translation/trc/trc-english/Multiverse_-_TRC_2.6.zip",
                Sha256 = "F34C1D7F69DCE723E7BE3479C0F9D075E2A8858D3947F1AAD1F4C330501338E3"
            };
        }

        internal static FtlAsset TrcGibsAsset()
        {
            return new FtlAsset
            {
                Name = "MV TRC GenGibs v1.4.2.ftl",
                Id = "trc26-gengibs-1.4.2",
                Size = 3065732,
                Url = "https://drive.usercontent.google.com/download?id=1BoYFKqPQQJ5CE9kSbufSKx9jWdyeiYRl&export=download&confirm=t",
                Sha256 = "927D6FF155E89A46ED2AA543C74E4C4C9C7ABFC84216CEA653EA67E482C81149"
            };
        }

        internal static void SelectTrc(FtlPlan plan, bool enabled)
        {
            plan.Trc = null;
            plan.TrcVersion = null;
            plan.TrcGibs = null;
            plan.TrcGibsVersion = null;
            if (!enabled)
                return;
            var version = VersionOf(plan.DataVersion);
            if (version.Major != 5 || version.Minor != 5)
                throw new IOException("The Renegade Collection 2.6 is verified for Multiverse 5.5 only. Turn it off until a compatible release is available.");
            plan.Trc = TrcAsset();
            plan.TrcVersion = "2.6";
            if (plan.Gibs != null)
            {
                plan.TrcGibs = TrcGibsAsset();
                plan.TrcGibsVersion = "1.4.2";
            }
        }

        static string ZipText(ZipArchive zip, string name)
        {
            var entry = zip.GetEntry(name);
            if (entry == null || entry.Length > 8 * 1024 * 1024)
                throw new IOException("Missing/oversized addon definition: " + name);
            using (var reader = new StreamReader(entry.Open()))
                return reader.ReadToEnd();
        }

        static string[] ShipSlots(string text)
        {
            var match = Regex.Match(text, @"<shipOrder>(.*?)</shipOrder>", RegexOptions.Singleline);
            if (!match.Success)
                throw new IOException("Missing ship page order");
            return Regex.Matches(match.Groups[1].Value, @"<ship>\s*([^<]+?)\s*</ship>|<empty\s*/>").Cast<Match>().Select(m => m.Groups[1].Success ? m.Groups[1].Value.Trim() : "empty").ToArray();
        }

        internal static void VerifyTrc(FtlPlan plan, HashSet<string> names)
        {
            string path = Path.Combine(plan.Stage, "ftl.dat");
            var index = ContentIndex(path);
            using (var patched = File.OpenRead(path))
            using (var data = ZipFile.OpenRead(plan.Downloads[plan.Data.Name]))
            {
                var actual = ShipSlots(Encoding.UTF8.GetString(Resource(patched, index["data/hyperspace.xml"], 8 * 1024 * 1024)));
                var expected = ShipSlots(ZipText(data, "data/hyperspace.xml")).ToList();
                if (plan.Trc != null)
                {
                    string archive = plan.Downloads[plan.Trc.Name];
                    if (new FileInfo(archive).Length != plan.Trc.Size || Engine.Hash(archive) != plan.Trc.Sha256)
                        throw new IOException("TRC differs from its pinned original archive");
                    using (var zip = ZipFile.OpenRead(archive))
                    {
                        if (MetadataVersion(zip) != plan.TrcVersion)
                            throw new IOException("TRC version mismatch");
                        var extra = Regex.Matches(ZipText(zip, "data/hyperspace.xml.append"), @"<mod-append:ship>\s*(TRC_PLAYER_SHIP_[^<]+?)\s*</mod-append:ship>").Cast<Match>().Select(m => m.Groups[1].Value.Trim()).ToArray();
                        if (extra.Length != 10)
                            throw new IOException("TRC does not define the expected extra ship page");
                        expected.AddRange(extra);
                        foreach (var entry in zip.Entries.Where(e => e.Name.Length > 0 && (e.FullName.StartsWith("img/") || e.FullName.StartsWith("audio/"))))
                        {
                            SilEntry target;
                            if (!index.TryGetValue(entry.FullName, out target))
                                throw new IOException("TRC resource missing: " + entry.FullName);
                            using (var source = entry.Open())
                                if (Engine.Hash(source) != Engine.Hash(Resource(patched, target, 64 * 1024 * 1024)))
                                    throw new IOException("TRC resource mismatch: " + entry.FullName);
                        }

                        string icons = Encoding.UTF8.GetString(Resource(patched, index["data/events_addon_icon.xml"], 4 * 1024 * 1024));
                        if (!Regex.IsMatch(icons, @"<event\s+name=""ADDON_TRC""[^>]*>.*?Currently Enabled\.", RegexOptions.Singleline))
                            throw new IOException("TRC enabled indicator definition is missing");
                    }
                }

                if (!actual.SequenceEqual(expected))
                    throw new IOException("Installed ship pages do not match the selected Multiverse/TRC stack");
                if (actual.Length != (plan.Trc == null ? 170 : 180))
                    throw new IOException("Expected 17 base or 18 TRC ship pages");
            }
        }
    }
}
