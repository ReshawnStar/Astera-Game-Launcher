using System;
using System.IO;
using System.Linq;
using System.Threading;
using MC2Pack;

namespace Astera
{
    sealed partial class LibraryForm
    {
        internal static int AuditThunderstoreCode(string gameId, string code, string root)
        {
            try
            {
                if (Directory.Exists(root))
                    throw new IOException("Use a fresh live code audit");
                Directory.CreateDirectory(root);
                var game = ThunderstoreProfiles.Game(gameId);
                if (game == null)
                    throw new IOException("Unknown game");
                string log = Path.Combine(root, "progress.log");
                Action<string> write = message => File.AppendAllText(log, DateTime.UtcNow.ToString("o") + " " + message + Environment.NewLine);
                Action<string, long, long> progress = (phase, done, total) => write(phase + " " + done + "/" + total);
                var workspace = Path.Combine(root, "work");
                ThunderImportPlan plan;
                string cache = Path.Combine(root, "cache");
                if (Directory.Exists(code))
                {
                    Directory.CreateDirectory(workspace);
                    plan = ThunderstoreImport.ReadArchive(Path.Combine(code, "work", "profile.r2z"), workspace, "replay");
                    plan.MetadataRoot = Path.Combine(code, "work", "metadata");
                    ThunderstoreImport.Resolve(plan, CancellationToken.None, progress, null, game);
                    cache = Path.Combine(code, "cache");
                }
                else
                    plan = ThunderstoreImport.Download(code, workspace, CancellationToken.None, progress, game);
                write("Profile verified: " + plan.Export.Name + "; " + plan.Packages.Count + " enabled packages");
                string profiles = Path.Combine(root, "profiles"), id = Guid.NewGuid().ToString("N");
                var target = ThunderstoreImport.Install(plan, game, profiles, id, null, CancellationToken.None, progress, write, null, null, cache);
                var args = ThunderstoreLaunch.Arguments(game, target);
                File.WriteAllText(Path.Combine(root, "result.json"), Engine.Json.Serialize(new
                {
                    Passed = true,
                    Game = game.Name,
                    Profile = plan.Export.Name,
                    EnabledPackages = plan.Packages.Count,
                    DisabledPackages = plan.Export.Mods.Count(m => !m.Enabled),
                    ProfileArchiveSha256 = Engine.Hash(plan.Archive),
                    Target = target,
                    LaunchArguments = args,
                    LiveProfilesChanged = false,
                    LiveGameLaunched = false
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
