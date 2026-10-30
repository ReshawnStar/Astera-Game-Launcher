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
        sealed class SilEntry
        {
            internal uint Flags;
            internal long Offset;
            internal int Length, Unpacked;
        }

        static Dictionary<string, SilEntry> ContentIndex(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var reader = new BinaryReader(stream))
            {
                byte[] header = reader.ReadBytes(16);
                if (header.Length != 16 || Encoding.ASCII.GetString(header, 0, 4) != "PKG\n" || header[4] != 0 || header[5] != 16 || header[6] != 0 || header[7] != 20)
                    throw new IOException("Invalid SIL archive header");
                uint count = Big(header, 8), nameBytes = Big(header, 12);
                if (count > 1000000 || nameBytes > 64 * 1024 * 1024 || 16L + 20L * count + nameBytes > stream.Length)
                    throw new IOException("Invalid SIL index size");
                var records = new List<Tuple<int, SilEntry>>();
                for (int i = 0; i < count; i++)
                {
                    byte[] entry = reader.ReadBytes(20);
                    uint bits = Big(entry, 4), offset = Big(entry, 8), length = Big(entry, 12), unpacked = Big(entry, 16);
                    if (offset == 0)
                        continue;
                    if ((long)offset + length > stream.Length || length > Int32.MaxValue || unpacked > Int32.MaxValue)
                        throw new IOException("Invalid SIL resource range");
                    records.Add(Tuple.Create((int)(bits & 0xffffff), new SilEntry { Flags = bits & 0xff000000, Offset = offset, Length = (int)length, Unpacked = (int)unpacked }));
                }

                byte[] names = reader.ReadBytes((int)nameBytes);
                var index = new Dictionary<string, SilEntry>(StringComparer.OrdinalIgnoreCase);
                foreach (var record in records)
                {
                    int start = record.Item1;
                    if (start < 0 || start >= names.Length)
                        throw new IOException("Invalid SIL name offset");
                    int end = Array.IndexOf(names, (byte)0, start);
                    if (end < 0)
                        throw new IOException("Unterminated SIL resource name");
                    index[Encoding.ASCII.GetString(names, start, end - start)] = record.Item2;
                }

                return index;
            }
        }

        static byte[] Resource(FileStream stream, SilEntry entry, int maximum)
        {
            if (entry.Unpacked > maximum || entry.Length > maximum || entry.Flags != 0 && entry.Flags != 0x01000000)
                throw new IOException("Unsupported or oversized SIL resource");
            stream.Position = entry.Offset;
            byte[] bytes = new byte[entry.Length];
            int done = 0;
            while (done < bytes.Length)
            {
                int read = stream.Read(bytes, done, bytes.Length - done);
                if (read == 0)
                    throw new EndOfStreamException();
                done += read;
            }

            if (entry.Flags != 0)
            {
                if (bytes.Length < 6 || (bytes[0] & 15) != 8 || ((bytes[0] << 8) + bytes[1]) % 31 != 0 || (bytes[1] & 32) != 0)
                    throw new IOException("Invalid zlib resource header");
                using (var input = new MemoryStream(bytes, 2, bytes.Length - 6))
                using (var inflate = new DeflateStream(input, CompressionMode.Decompress))
                using (var output = new MemoryStream())
                {
                    byte[] buffer = new byte[65536];
                    int read;
                    while ((read = inflate.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        if (output.Length + read > maximum)
                            throw new IOException("Oversized expanded SIL resource");
                        output.Write(buffer, 0, read);
                    }

                    bytes = output.ToArray();
                }
            }

            if (bytes.Length != entry.Unpacked)
                throw new IOException("SIL resource size mismatch");
            return bytes;
        }

        internal static string InstalledDataVersion(string path)
        {
            var index = ContentIndex(path);
            SilEntry entry;
            if (!index.TryGetValue("data/text_misc.xml", out entry))
                return "Unknown";
            using (var stream = File.OpenRead(path))
            {
                string text = Encoding.UTF8.GetString(Resource(stream, entry, 4 * 1024 * 1024));
                var match = Regex.Match(text, @"<text\s+name\s*=\s*[""']version[""']\s*>\s*Version\s+([0-9]+(?:\.[0-9]+)+)", RegexOptions.IgnoreCase);
                return match.Success ? match.Groups[1].Value : "Unknown";
            }
        }

        static string MetadataVersion(ZipArchive archive)
        {
            var entry = archive.GetEntry("mod-appendix/metadata.xml");
            if (entry == null || entry.Length > 1024 * 1024)
                throw new IOException("Missing or oversized Multiverse metadata");
            using (var reader = new StreamReader(entry.Open()))
            {
                var match = Regex.Match(reader.ReadToEnd(), @"<version>\s*(?:<!\[CDATA\[\s*)?([0-9]+(?:\.[0-9]+)+)", RegexOptions.IgnoreCase);
                if (!match.Success)
                    throw new IOException("Multiverse metadata has no recognised version");
                return match.Groups[1].Value;
            }
        }

        internal static void VerifyContent(FtlPlan plan, HashSet<string> names)
        {
            string data = Path.Combine(plan.Stage, "ftl.dat");
            using (var zip = ZipFile.OpenRead(plan.Downloads[plan.Data.Name]))
                if (MetadataVersion(zip) != plan.DataVersion)
                    throw new IOException("The Data ZIP does not match the selected release");
            if (InstalledDataVersion(data) != plan.DataVersion)
                throw new IOException("Patched Multiverse data does not report the selected version");
            var index = ContentIndex(data);
            var overrides = GibOverrides(plan);
            using (var zip = ZipFile.OpenRead(plan.Downloads[plan.Assets.Name]))
            using (var patched = File.OpenRead(data))
            {
                if (MetadataVersion(zip) != plan.AssetsVersion)
                    throw new IOException("The Assets ZIP does not match the selected release");
                foreach (var entry in zip.Entries.Where(e => e.Name.Length > 0 && (e.FullName.StartsWith("img/", StringComparison.Ordinal) || e.FullName.StartsWith("audio/", StringComparison.Ordinal) || e.FullName.StartsWith("fonts/", StringComparison.Ordinal))))
                {
                    if (!names.Contains(entry.FullName))
                        throw new IOException("Patched Multiverse resource is missing: " + entry.FullName);
                    if (entry.FullName.IndexOf("_gib", StringComparison.OrdinalIgnoreCase) < 0 || !entry.FullName.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || overrides.Contains(entry.FullName))
                        continue;
                    using (var expected = entry.Open())
                        if (Engine.Hash(expected) != Engine.Hash(Resource(patched, index[entry.FullName], 64 * 1024 * 1024)))
                            throw new IOException("Patched ship debris differs from official Assets: " + entry.FullName);
                }
            }
        }

        internal static string ContentReport(string root)
        {
            var state = State(root);
            string path = Path.Combine(root, "ftl.dat");
            var names = ArchiveNames(path);
            var settings = new Dictionary<string, string>();
            string ini = Path.Combine(root, "settings.ini");
            if (File.Exists(ini))
                foreach (string line in File.ReadLines(ini))
                {
                    var match = Regex.Match(line, @"^\s*(lowend|vsync|framelimit|fullscreen|logging)\s*=\s*(\d+)\s*$");
                    if (match.Success)
                        settings[match.Groups[1].Value] = match.Groups[2].Value;
                }

            var changed = state == null ? new string[0] : state.Files.Where(p => Current(root, p.Key) != p.Value).Select(p => p.Key).ToArray();
            return Encode(new
            {
                InstalledGameData = InstalledDataVersion(path),
                RecordedData = state == null ? null : state.DataVersion,
                RecordedAssets = state == null ? null : state.AssetsVersion,
                RecordedHyperspace = state == null ? null : state.HyperspaceVersion,
                GeneratedGibs = state == null ? null : state.GibsVersion,
                RenegadeCollection = state == null ? null : state.TrcVersion,
                RenegadeGibs = state == null ? null : state.TrcGibsVersion,
                ArchiveEntries = names.Count,
                DebrisImages = names.Count(n => n.IndexOf("_gib", StringComparison.OrdinalIgnoreCase) >= 0 && n.EndsWith(".png", StringComparison.OrdinalIgnoreCase)),
                ChangedManagedFiles = changed,
                GraphicsSettings = settings,
                AnimationVisuallyVerified = false,
                Note = "Many base Multiverse ships have no breakup debris; the optional Generated Gibs addon fills these gaps. Assets and Data can have different official versions. Windows Hyperspace intentionally uses the compatible older FTL base executable. Resource presence does not prove rendered animation."
            });
        }
    }
}
