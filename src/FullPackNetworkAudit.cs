using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using MC2Pack;

namespace Astera
{
    static partial class NetworkSelfTest
    {
        internal static int FullPackRun(string root)
        {
            try
            {
                if (Directory.Exists(root))
                    throw new Exception("Use a new full-pack test directory.");
                Directory.CreateDirectory(root);
                var source = Path.Combine(root, "source");
                var profile = Path.Combine(root, "profile");
                var cache = Path.Combine(root, "cache");
                Directory.CreateDirectory(source);
                Directory.CreateDirectory(profile);
                Directory.CreateDirectory(cache);
                var checks = new List<string>();
                Jar(source, "mods/allies.jar", "stars_allies", "0.7.173");
                Jar(source, "mods/fixes.jar", "stars_general_fixes", "1.7.206");
                Jar(source, "mods/third-party.jar", "test_third_party", "2.0.0");
                Put(source, "config/pack.json", "new defaults");
                using (var zip = ZipFile.Open(Path.Combine(source, "pack.zip"), ZipArchiveMode.Create))
                using (var w = new StreamWriter(zip.CreateEntry("pack.mcmeta").Open()))
                    w.Write("{}");
                Directory.CreateDirectory(Path.Combine(source, "resourcepacks"));
                File.Move(Path.Combine(source, "pack.zip"), Path.Combine(source, "resourcepacks/pack.zip"));
                Put(profile, "minecraftinstance.json", "{\"baseModLoader\":{\"minecraftVersion\":\"26.3\",\"name\":\"fabric-0.19.5-26.3\",\"forgeVersion\":\"0.19.5\"}}");
                Put(profile, "saves/keep/level.dat", "world");
                Put(profile, "options.txt", "music:0.23\nkey_key.forward:key.keyboard.up\nresourcePacks:[\"vanilla\"]\n");
                Put(profile, "config/pack.json", "personal");
                Jar(profile, "mods/third-old.jar", "test_third_party", "1.0.0");
                var release = new Release
                {
                    Schema = 2,
                    Sequence = 100,
                    PackId = "astera-minecraft-squared",
                    Name = "full-fixture",
                    Minecraft = "26.3",
                    Loader = "0.19.5",
                    Files = new List<PackFile>(),
                    Retired = new List<RetiredFile>(),
                    Packs = new List<string>
                    {
                        "file/pack.zip",
                        "stars_general_fixes:stars_compat"
                    }
                };
                foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
                {
                    string path = file.Substring(source.Length + 1).Replace('\\', '/');
                    var mod = path.StartsWith("mods/") ? Engine.Mod(file) : null;
                    release.Files.Add(new PackFile { Path = path, Hash = Engine.Hash(file), Size = new FileInfo(file).Length, Kind = mod != null ? "mod" : path.StartsWith("config/") ? "config" : "asset", Policy = path.StartsWith("config/") ? "seed" : "replace", Id = mod == null ? null : Convert.ToString(mod["id"]), Version = mod == null ? null : Convert.ToString(mod["version"]) });
                }

                var manifest = Engine.Json.Deserialize<Dictionary<string, object>>(Engine.Json.Serialize(release));
                manifest["Files"] = release.Files.Select(f => new Dictionary<string, object> { { "Size", f.Size }, { "Family", f.Family }, { "Version", f.Version }, { "Id", f.Id }, { "Policy", f.Policy }, { "Kind", f.Kind }, { "Hash", f.Hash }, { "Path", f.Path } }).ToArray();
                var bundle = Path.Combine(root, "release.zip");
                using (var zip = ZipFile.Open(bundle, ZipArchiveMode.Create))
                {
                    using (var w = new StreamWriter(zip.CreateEntry("mc2-release.json").Open()))
                        w.Write(Engine.Json.Serialize(manifest));
                    foreach (var file in release.Files)
                        zip.CreateEntryFromFile(Path.Combine(source, file.Path), file.Path);
                }

                using (var server = new FixtureServer())
                using (var rsa = new RSACryptoServiceProvider(2048, new CspParameters(24)))
                {
                    rsa.PersistKeyInCsp = false;
                    server.Files["/pack.zip"] = File.ReadAllBytes(bundle);
                    var feed = new UpdateFeed
                    {
                        Schema = 2,
                        GameId = "minecraft-squared",
                        ExpiresUtc = DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeSeconds(),
                        Notes = "Full pack test",
                        Sources = new List<PayloadSource>(),
                        RequiredMods = new List<RequiredMod>(),
                        Release = release,
                        Bundle = new BundleSource
                        {
                            Url = server.Address + "/pack.zip",
                            Hash = Engine.Hash(bundle),
                            Size = new FileInfo(bundle).Length
                        }
                    };
                    server.Files["/feed"] = Signed(feed, rsa);
                    var client = new UpdateClient(rsa.ToXmlString(false), new Transport(true));
                    var verified = client.Check(server.Address + "/feed", CancellationToken.None);
                    var plan = client.Prepare(verified, profile, cache);
                    Assert(plan.DownloadFiles == 1 && plan.DownloadBytes == feed.Bundle.Size, "Full transfer size incorrect");
                    long transferred = 0;
                    var phases = new List<string>();
                    var prepared = client.Materialize(plan, cache, CancellationToken.None, delegate
                    {
                    }, delegate (string phase, long done, long total)
                    {
                        phases.Add(phase);
                        if (phase == "Downloading")
                            transferred = done;
                    });
                    Assert(transferred == feed.Bundle.Size && phases.Contains("Verifying pack") && phases.Contains("Preparing install"), "Full download did not report bytes and phases");
                    checks.Add("Signed full-pack notice downloads and verifies the ZIP through actual HTTP, without per-mod URLs");
                    var apply = Engine.Scan(profile, prepared, true, false, false, delegate
                    {
                    });
                    apply.Preconditions = plan.Guards;
                    Engine.Apply(apply, delegate
                    {
                    }, true);
                    Assert(File.Exists(Path.Combine(profile, "mods/third-party.jar")) && !File.Exists(Path.Combine(profile, "mods/third-old.jar")), "Third-party update failed");
                    Assert(File.ReadAllText(Path.Combine(profile, "config/pack.json")) == "personal" && File.ReadAllText(Path.Combine(profile, "saves/keep/level.dat")) == "world", "Protected files changed");
                    var options = File.ReadAllText(Path.Combine(profile, "options.txt"));
                    Assert(options.Contains("music:0.23") && options.Contains("key.keyboard.up") && options.Contains("file/pack.zip"), "Resource pack setup changed personal settings");
                    checks.Add("Full pack replaces an outdated third-party mod, installs missing custom mods/resource packs and preserves worlds/config/keybinds");
                    File.Delete(prepared);
                    int requests = server.Requests.Count;
                    prepared = client.Materialize(client.Prepare(verified, profile, cache), cache, CancellationToken.None, delegate
                    {
                    });
                    Assert(server.Requests.Count == requests, "Cached full pack downloaded again");
                    Assert(Engine.Scan(profile, prepared, true, false, false, delegate
                    {
                    }).Edits.Count == 0, "Repeat full pack edits files");
                    checks.Add("Repeat install reuses the verified ZIP cache and makes zero changes");
                    var bytes = server.Files["/pack.zip"];
                    server.Files["/pack.zip"] = new byte[bytes.Length];
                    Reject(delegate
                    {
                        client.Materialize(plan, Path.Combine(root, "bad-cache"), CancellationToken.None, delegate
                        {
                        });
                    }, "Corrupt bundle accepted");
                    server.Files["/pack.zip"] = bytes;
                    checks.Add("Corrupt full ZIP is rejected before installation");
                    var wrong = Engine.Json.Deserialize<UpdateFeed>(Engine.Json.Serialize(feed));
                    wrong.Release.Name = "different";
                    Reject(delegate
                    {
                        client.Materialize(client.Prepare(wrong, profile, cache), cache, CancellationToken.None, delegate
                        {
                        });
                    }, "Signed metadata and ZIP mismatch accepted");
                    checks.Add("ZIP manifest must match the complete signed release metadata");
                    using (var cancellation = new CancellationTokenSource())
                    {
                        cancellation.Cancel();
                        Reject(delegate
                        {
                            client.Materialize(plan, cache, cancellation.Token, delegate
                            {
                            });
                        }, "Cancelled bundle accepted");
                    }

                    checks.Add("Cancellation prevents staging an install");
                    var legacy = Engine.Json.Deserialize<UpdateFeed>(Engine.Json.Serialize(feed));
                    legacy.Schema = 1;
                    Reject(delegate
                    {
                        client.Validate(legacy);
                    }, "Legacy component channel accepted broad bundle");
                    checks.Add("Component-only schema cannot request full-pack ownership");
                }

                File.WriteAllText(Path.Combine(root, "result.json"), Engine.Json.Serialize(new
                {
                    Passed = true,
                    Checks = checks,
                    LiveProfileChanged = false
                }));
                return 0;
            }
            catch (Exception error)
            {
                Directory.CreateDirectory(root);
                File.WriteAllText(Path.Combine(root, "failure.txt"), error.ToString());
                return 1;
            }
        }
    }
}
