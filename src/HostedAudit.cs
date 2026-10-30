using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using MC2Pack;

namespace Astera
{
    static class HostedAudit
    {
        public static int Run(string url, string profile, string cache)
        {
            var log = new List<string>();
            string result = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(profile)), "hosted-result.json");
            try
            {
                var marker = Path.Combine(profile, "astera-test-fixture.txt");
                if (!File.Exists(marker) || File.ReadAllText(marker) != "Synthetic self-test profile; never a live game.")
                    throw new Exception("Hosted audit requires a synthetic fixture, never a live profile.");
                if (!Path.GetFileName(Path.GetDirectoryName(Path.GetFullPath(profile))).StartsWith("astera-hosted-", StringComparison.Ordinal))
                    throw new Exception("Use an explicitly named astera-hosted audit directory.");
                Channel channel;
                using (var reader = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("Astera.Channel")))
                    channel = Engine.Json.Deserialize<Channel>(reader.ReadToEnd());
                var client = new UpdateClient(channel.PublicKeyXml);
                var feed = client.Check(url, CancellationToken.None);
                var download = client.Prepare(feed, profile, cache);
                if (download.DownloadFiles == 0)
                    throw new Exception("Hosted fixture must exercise real payload downloads.");
                string sentinel = File.ReadAllText(Path.Combine(profile, "saves/protected/level.dat"));
                string config = File.ReadAllText(Path.Combine(profile, "config/stars-general-fixes.json"));
                string bundle = client.Materialize(download, cache, CancellationToken.None, log.Add);
                string bundleHash = Engine.Hash(bundle);
                var plan = Engine.Scan(profile, bundle, true, false, false, log.Add);
                plan.Preconditions = download.Guards;
                Engine.Apply(plan, log.Add, true);
                if (File.ReadAllText(Path.Combine(profile, "saves/protected/level.dat")) != sentinel || File.ReadAllText(Path.Combine(profile, "config/stars-general-fixes.json")) != config || !File.ReadAllText(Path.Combine(profile, "options.txt")).Contains("key_key.forward:key.keyboard.up"))
                    throw new Exception("Hosted installation changed a protected sentinel.");
                var repeat = Engine.Scan(profile, bundle, true, false, false, log.Add);
                if (repeat.Edits.Count != 0)
                    throw new Exception("Hosted repeat update is not a no-op.");
                var backup = Directory.GetDirectories(Path.Combine(profile, "Astera-backups")).Single();
                Engine.Restore(profile, backup, log.Add, true);
                if (File.ReadAllText(Path.Combine(profile, "saves/protected/level.dat")) != sentinel || File.ReadAllText(Path.Combine(profile, "config/stars-general-fixes.json")) != config)
                    throw new Exception("Hosted restore changed protected data.");
                File.WriteAllText(result, Engine.Json.Serialize(new
                {
                    Passed = true,
                    Feed = url,
                    Release = feed.Release.Name,
                    RealHttpsDownloads = download.DownloadFiles,
                    DownloadedBytes = download.DownloadBytes,
                    SignatureVerified = true,
                    ProtectedSentinels = true,
                    InstalledAndRestored = true,
                    RepeatChanges = repeat.Edits.Count,
                    JarCodeExecuted = false,
                    LiveProfileChanged = false,
                    BundleSha256 = bundleHash,
                    Log = log
                }));
                File.Delete(bundle);
                return 0;
            }
            catch (Exception failure)
            {
                File.WriteAllText(result, Engine.Json.Serialize(new
                {
                    Passed = false,
                    Error = failure.ToString(),
                    Log = log
                }));
                return 1;
            }
        }
    }
}
