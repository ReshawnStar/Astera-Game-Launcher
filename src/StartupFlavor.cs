using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web.Script.Serialization;

namespace Astera
{
    public sealed class StartupFlavorCategory
    {
        public int count, weight;
    }

    public sealed class StartupFlavorLine
    {
        public string id, category, text;
    }

    public sealed class StartupFlavorData
    {
        public int schema_version;
        public Dictionary<string, StartupFlavorCategory> categories;
        public StartupFlavorLine[] lines;
    }

    sealed class StartupFlavorPool
    {
        readonly StartupFlavorData data;
        readonly Random random;
        readonly HashSet<string> seen = new HashSet<string>();
        readonly int count;
        string last;
        internal int Count
        {
            get
            {
                return count;
            }
        }

        internal StartupFlavorPool(StartupFlavorData source, int seed)
        {
            data = source;
            random = new Random(seed);
            count = data.lines.Select(l => l.text).Distinct().Count();
        }

        internal static StartupFlavorData Parse(string text)
        {
            if (text.Length > 65536)
                throw new IOException("Startup flavor library exceeds its size limit");
            var data = new JavaScriptSerializer
            {
                MaxJsonLength = 65536
            }.Deserialize<StartupFlavorData>(text);
            var allowed = new[]
            {
                "boot",
                "cosmic",
                "games",
                "group",
                "rare"
            };
            if (data == null || data.schema_version != 1 || data.categories == null || data.lines == null || data.lines.Length < 1 || data.lines.Length > 1000 || data.categories.Count > 5)
                throw new IOException("Unsupported startup flavor library");
            if (data.categories.Any(c => !allowed.Contains(c.Key) || c.Value == null || c.Value.weight < 1 || c.Value.weight > 100))
                throw new IOException("Invalid startup flavor category");
            var ids = new HashSet<string>();
            foreach (var line in data.lines)
            {
                if (line == null || String.IsNullOrWhiteSpace(line.id) || !ids.Add(line.id) || !data.categories.ContainsKey(line.category ?? "") || String.IsNullOrWhiteSpace(line.text) || line.text.Length > 220 || line.text.Any(Char.IsControl))
                    throw new IOException("Invalid startup flavor line");
            }

            return data;
        }

        internal static StartupFlavorPool Load(string root)
        {
            StartupFlavorData data = null;
            string path = Path.Combine(root, "startup-lines.json");
            try
            {
                if (File.Exists(path))
                {
                    if (new FileInfo(path).Length > 65536)
                        throw new IOException("Startup flavor override exceeds its size limit");
                    data = Parse(File.ReadAllText(path));
                }
            }
            catch (Exception error)
            {
                AsteraDiagnostics.Write("Startup flavor override unavailable; using bundled lines", new IOException(error.GetType().Name));
            }

            if (data == null)
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Astera.Startup.Lines"))
                using (var reader = new StreamReader(stream))
                {
                    data = Parse(reader.ReadToEnd());
                }

            return new StartupFlavorPool(data, Guid.NewGuid().GetHashCode());
        }

        internal StartupFlavorPool ForLaunch(bool minecraft)
        {
            var lines = data.lines.Where(l => l.category != "boot" && (minecraft || l.category != "games")).ToArray();
            if (lines.Length == 0)
                lines = data.lines;
            return new StartupFlavorPool(new StartupFlavorData { schema_version = 1, categories = data.categories, lines = lines }, Guid.NewGuid().GetHashCode());
        }

        internal string Next()
        {
            if (seen.Count >= Count)
                seen.Clear();
            var eligible = data.lines.Where(l => !seen.Contains(l.text) && (Count == 1 || l.text != last)).ToArray();
            if (eligible.Length == 0)
            {
                seen.Clear();
                eligible = data.lines.Where(l => Count == 1 || l.text != last).ToArray();
            }

            var categories = eligible.Select(l => l.category).Distinct().ToArray();
            int draw = random.Next(categories.Sum(c => data.categories[c].weight));
            string chosen = categories[0];
            foreach (string category in categories)
            {
                draw -= data.categories[category].weight;
                if (draw < 0)
                {
                    chosen = category;
                    break;
                }
            }

            var options = eligible.Where(l => l.category == chosen).ToArray();
            last = options[random.Next(options.Length)].text;
            seen.Add(last);
            return last;
        }
    }
}
