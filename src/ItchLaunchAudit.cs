using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using MC2Pack;

namespace Astera
{
    sealed partial class LibraryForm
    {
        internal static int AuditItchLaunch(string root, string installed = null)
        {
            try
            {
                if (Directory.Exists(root))
                    throw new IOException("Use a fresh audit directory");
                Directory.CreateDirectory(root);
                root = Path.GetFullPath(root);
                var checks = new List<string>();
                string folder = Path.Combine(root, "Installed game");
                Directory.CreateDirectory(Path.Combine(folder, ".itch"));
                Directory.CreateDirectory(Path.Combine(folder, "Old version"));
                string gameExe = Path.Combine(folder, "Actual Game.exe"), helper = Path.Combine(folder, "UnityCrashHandler64.exe"), old = Path.Combine(folder, "Old version", "Actual Game.exe");
                foreach (string file in new[]
                {
                    gameExe,
                    helper,
                    old
                }

                )
                    File.Copy(Application.ExecutablePath, file);
                var receipt = new ItchReceipt
                {
                    game = new ItchReceiptGame
                    {
                        id = 123,
                        title = "Fixture"
                    },
                    upload = new ItchUpload
                    {
                        launchTargets = new[]
                        {
                            new ItchLaunchTarget
                            {
                                path = "Actual Game.exe",
                                flavor = "windows",
                                arch = "amd64"
                            }
                        }
                    },
                    files = new[]
                    {
                        "Actual Game.exe",
                        "UnityCrashHandler64.exe"
                    }
                };
                string receiptPath = Path.Combine(folder, ".itch", "receipt.json");
                File.WriteAllText(receiptPath, Engine.Json.Serialize(receipt));
                var game = new CustomGame
                {
                    Platform = "itch",
                    PlatformId = "123",
                    Executable = folder
                };
                var candidates = ItchLaunch.Candidates(game);
                AuditAssert(candidates.Length == 1 && candidates[0] == gameExe, "Receipt launch target was not selected uniquely");
                var start = ItchLaunch.Start(game, candidates[0]);
                AuditAssert(start.FileName == gameExe && start.WorkingDirectory == folder && start.Arguments == "" && !start.FileName.StartsWith("itch:"), "Installed game routed to installer or wrong working directory");
                checks.Add("The installed Windows target launches directly with its own working directory, ignoring crash helpers and stale nested versions");
                receipt.upload = null;
                receipt.files = new[]
                {
                    "Actual Game.exe",
                    "UnityCrashHandler64.exe",
                    "lib/python.exe"
                };
                File.WriteAllText(receiptPath, Engine.Json.Serialize(receipt));
                AuditAssert(ItchLaunch.Candidates(game).Single() == gameExe, "Older receipt file-list fallback failed");
                checks.Add("Older receipts resolve their installed file list without recursively guessing unrelated executables");
                foreach (string invalid in new[]
                {
                    "../outside.exe",
                    "C:/outside.exe",
                    "Actual Game.exe:stream",
                    "\\\\server\\game.exe",
                    "UnityCrashHandler64.exe",
                    "bad\0.exe"
                }

                )
                    AuditAssert(ItchLaunch.SafeExecutable(folder, invalid) == null, "Unsafe or helper target accepted");
                game.PlatformId = "456";
                bool rejected = false;
                try
                {
                    ItchLaunch.Candidates(game);
                }
                catch (IOException)
                {
                    rejected = true;
                }

                AuditAssert(rejected, "Mismatched installed identity accepted");
                game.PlatformId = "123";
                checks.Add("Launch targets stay inside the matching installation and reject traversal, streams, invalid paths and helper programs");
                receipt.files = new[]
                {
                    "Actual Game.exe",
                    "Old version/Actual Game.exe"
                };
                File.WriteAllText(receiptPath, Engine.Json.Serialize(receipt));
                AuditAssert(ItchLaunch.Candidates(game).Length == 2, "Ambiguous install was guessed");
                File.WriteAllText(Path.Combine(folder, ".itch.toml"), "[[actions]]\nname=\"play\"\npath=\"Actual Game.exe\"\nargs=[\"--custom\"]");
                AuditAssert(ItchLaunch.Candidates(game).Length == 0, "Manifest arguments were silently bypassed");
                AuditAssert(PlatformLibraries.Start(game).FileName == "itch://games/123", "Fallback still routes to install chooser");
                checks.Add("Genuinely multiple targets remain a user choice; manifest-specific launch actions open the itch game page, not its installer");
                var state = new Store(Path.Combine(root, "state"));
                game.ItchLaunchTarget = "Actual Game.exe";
                state.Settings.CustomGames.Add(game);
                state.Save();
                AuditAssert(new Store(state.Root).Settings.CustomGames.Single().ItchLaunchTarget == game.ItchLaunchTarget, "Remembered target lost across restart");
                checks.Add("The chosen relative launch target survives a restart and remains independent of the machine's library path");
                if (installed != null)
                {
                    var metadata = ItchMetadata.Read(installed);
                    AuditAssert(metadata != null, "Real installed receipt unavailable");
                    var actual = new CustomGame
                    {
                        Platform = "itch",
                        PlatformId = metadata.id.ToString(),
                        Executable = installed
                    };
                    var found = ItchLaunch.Candidates(actual);
                    AuditAssert(found.Length == 1, "Reported live install did not resolve one target");
                    var plan = ItchLaunch.Start(actual, found[0]);
                    File.WriteAllText(Path.Combine(root, "installed-launch-plan.json"), Engine.Json.Serialize(new
                    {
                        Executable = plan.FileName,
                        WorkingDirectory = plan.WorkingDirectory,
                        GameLaunched = false
                    }));
                    checks.Add("The reported real installation resolves one local Windows executable; dry-plan only, no game started");
                }

                File.WriteAllText(Path.Combine(root, "result.json"), Engine.Json.Serialize(new
                {
                    Passed = true,
                    Checks = checks,
                    GamesLaunched = false,
                    LivePreferencesChanged = false
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
