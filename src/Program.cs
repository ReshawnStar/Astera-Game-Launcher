using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MC2Pack;

namespace Astera
{
    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            AppContext.SetSwitch("Switch.System.IO.UseLegacyPathHandling", false);
            AppContext.SetSwitch("Switch.System.IO.BlockLongPaths", false);
            EmbeddedWebView.Register();
            ThunderYaml.Register();
            if (args.Length == 1 && args[0] == "--ftl-tool-probe")
            {
                using (var output = new System.IO.StreamWriter(Console.OpenStandardOutput()))
                {
                    output.AutoFlush = true;
                    output.WriteLine("probe started");
                    System.Threading.Thread.Sleep(1300);
                    output.WriteLine("probe complete");
                }

                return 0;
            }

            if (args.Length == 2 && args[0] == "--ftl-progress-audit")
                return LibraryForm.AuditFtlProgress(args[1]);
            if (args.Length == 2 && args[0] == "--roblox-audit")
                return LibraryForm.AuditRoblox(args[1], false);
            if (args.Length == 2 && args[0] == "--roblox-live-audit")
                return LibraryForm.AuditRoblox(args[1], true);
            if (args.Length == 2 && args[0] == "--youtube-audit")
                return YouTubeAudit.Run(args[1], false);
            if (args.Length == 2 && args[0] == "--youtube-live-audit")
                return YouTubeAudit.Run(args[1], true);
            if (args.Length == 2 && args[0] == "--self-update-health-audit")
                return SelfUpdate.AuditHealthRead(args[1]);
            if (args.Length == 2 && args[0] == "--library-organization-audit")
                return LibraryForm.AuditLibraryOrganization(args[1]);
            if (args.Length == 3 && args[0] == "--itch-launch-check")
                return LibraryForm.AuditItchLaunch(args[1], args[2]);
            if (args.Length == 2 && args[0] == "--itch-launch-audit")
                return LibraryForm.AuditItchLaunch(args[1]);
            if (args.Length == 4 && args[0] == "--thunderstore-code-audit")
                return LibraryForm.AuditThunderstoreCode(args[1], args[2], args[3]);
            if (args.Length == 2 && args[0] == "--thunderstore-audit")
                return LibraryForm.AuditThunderstore(args[1]);
            if (args.Length == 2 && args[0] == "--thunderstore-live-audit")
                return LibraryForm.AuditThunderstore(args[1], true);
            if (args.Length == 2 && args[0] == "--app-update-flow-audit")
                return LibraryForm.AuditAppUpdateFlow(args[1]);
            if (args.Length == 2 && args[0] == "--catalog-verify")
            {
                try
                {
                    Channel source;
                    using (var reader = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("Astera.Channel")))
                        source = Engine.Json.Deserialize<Channel>(reader.ReadToEnd());
                    DiscoverCatalog.Verify(File.ReadAllBytes(args[1]), source.PublicKeyXml);
                    return 0;
                }
                catch
                {
                    return 1;
                }
            }

            if (args.Length == 2 && args[0] == "--catalog-validate")
            {
                try
                {
                    DiscoverCatalog.Parse(File.ReadAllText(args[1]));
                    return 0;
                }
                catch (Exception error)
                {
                    AsteraDiagnostics.Write("Invalid project catalog", error);
                    return 1;
                }
            }

            if (args.Length == 2 && args[0] == "--managed-library-audit")
                return LibraryForm.AuditManagedLibrary(args[1]);
            if (args.Length == 2 && args[0] == "--managed-game-audit")
                return LibraryForm.AuditManagedGame(args[1]);
            if (args.Length == 3 && args[0] == "--nebula-package-audit")
                return LibraryForm.AuditManagedGame(args[1], args[2]);
            if (args.Length == 2 && args[0] == "--primary-transition-audit")
                return LibraryForm.AuditPrimaryTransition(args[1]);
            if (args.Length == 2 && args[0] == "--discover-audit")
                return LibraryForm.AuditDiscover(args[1]);
            if (args.Length == 2 && args[0] == "--runtime-health-audit")
                return LibraryForm.AuditRuntimeHealth(args[1]);
            if (args.Length == 2 && args[0] == "--activity-audit")
                return LibraryForm.AuditActivity(args[1]);
            if (args.Length == 2 && args[0] == "--startup-audit")
                return LibraryForm.AuditStartup(args[1]);
            if (args.Length == 2 && args[0] == "--motion-audit")
                return MotionAudit.Run(args[1]);
            if (args.Length == 2 && args[0] == "--display-polish-audit")
                return LibraryForm.AuditDisplayPolish(args[1], false);
            if (args.Length == 2 && args[0] == "--roblox-gallery-live-audit")
                return LibraryForm.AuditDisplayPolish(args[1], true);
            if (args.Length == 2 && args[0] == "--library-wheel-audit")
                return LibraryForm.AuditLibraryWheel(args[1]);
            if (args.Length == 2 && args[0] == "--library-icons-audit")
                return LibraryForm.AuditLibraryIconStartup(args[1]);
            if (args.Length == 2 && args[0] == "--buffer-offset-audit")
                return LibraryForm.AuditBufferOffset(args[1]);
            if (args.Length == 2 && args[0] == "--library-paint-audit")
                return LibraryForm.AuditLibraryPaint(args[1]);
            if (args.Length == 2 && args[0] == "--gif-audit")
                return LibraryForm.AuditGif(args[1]);
            if (args.Length == 3 && args[0] == "--steam-public-artwork-check")
                return LibraryForm.AuditPublicSteamArtwork(args[1], args[2]);
            if (args.Length == 2 && args[0] == "--verify-app-feed")
                return SelfUpdateAudit.Verify(args[1]);
            if (args.Length == 3 && args[0] == "--verify-app-candidate")
                return SelfUpdateAudit.Verify(args[1], args[2]);
            if (args.Length == 2 && args[0] == "--apply-self-update")
                return SelfUpdate.Apply(args[1]);
            if (args.Length == 2 && args[0] == "--start-self-update-helper")
            {
                try
                {
                    SelfUpdate.StartHelper(args[1]);
                    return 0;
                }
                catch (Exception error)
                {
                    File.WriteAllText(Path.Combine(Path.GetDirectoryName(args[1]), "helper-failure.txt"), error.ToString());
                    return 1;
                }
            }

            if (args.Length == 2 && args[0] == "--multiverse-self-test")
                return MultiverseAudit.SelfTest(args[1]);
            if (args.Length == 2 && args[0] == "--multiverse-real-test")
                return MultiverseAudit.RealTest(args[1]);
            if (args.Length == 2 && args[0] == "--skin-preview")
                return MinecraftSkin.Preview(args[1]);
            if (args.Length == 3 && args[0] == "--steam-news-check")
                return LibraryForm.AuditSteamArtwork(args[1], args[2], true);
            if (args.Length == 3 && args[0] == "--steam-artwork-check")
                return LibraryForm.AuditSteamArtwork(args[1], args[2]);
            if (args.Length == 3 && args[0] == "--performance-check")
                return LibraryForm.AuditPerformance(args[1], args[2]);
            if (args.Length == 3 && args[0] == "--frame-check")
                return LibraryForm.AuditLiveStability(args[1], args[2]);
            if (args.Length == 3 && args[0] == "--motion-check")
                return LibraryForm.AuditVisibleUi(args[1], args[2], true);
            if (args.Length == 3 && args[0] == "--visual-check")
                return LibraryForm.AuditVisibleUi(args[1], args[2]);
            if (args.Length == 2 && args[0] == "--visual-check")
                return LibraryForm.AuditVisibleUi(args[1]);
            if (args.Length == 2 && args[0] == "--switch-check")
                return LibraryForm.AuditSwitchFrames(args[1]);
            if (args.Length == 2 && args[0] == "--folder-picker-audit")
                return FolderPickerAudit.Run(args[1]);
            if (args.Length == 2 && args[0] == "--folder-picker-demo")
            {
                using (var picker = new ExplorerFolderDialog
                {
                    Description = "Astera Explorer folder picker test",
                    SelectedPath = Path.Combine(args[1], "Folder with spaces")
                }

                )
                {
                    var result = picker.ShowDialog();
                    File.WriteAllText(Path.Combine(args[1], "demo-result.json"), Engine.Json.Serialize(new
                    {
                        Result = result.ToString(),
                        SelectedPath = picker.SelectedPath
                    }));
                    return 0;
                }
            }

            if (args.Length == 2 && args[0] == "--shutdown-audit")
                return LibraryForm.AuditShutdown(args[1]);
            if (args.Length == 2 && args[0] == "--dialog-audit")
                return LibraryForm.AuditDialogs(args[1]);
            if (args.Length == 2 && args[0] == "--library-features-audit")
                return LibraryForm.AuditLibraryFeatures(args[1]);
            if (args.Length == 3 && args[0] == "--itch-artwork-check")
                return LibraryForm.AuditItchArtwork(args[1], args[2]);
            if (args.Length == 2 && args[0] == "--layout-audit")
                return LibraryForm.AuditLayout(args[1]);
            if (args.Length == 2 && args[0] == "--install-lock-audit")
                return NetworkSelfTest.LockRun(args[1]);
            if (args.Length == 2 && args[0] == "--minecraft-profiles-audit")
                return LibraryForm.AuditMinecraftProfiles(args[1]);
            if (args.Length == 2 && args[0] == "--minecraft-launch-audit")
                return LibraryForm.AuditMinecraftLaunch(args[1]);
            if (args.Length == 3 && args[0] == "--minecraft-native-launch-audit")
                return LibraryForm.AuditMinecraftLaunch(args[1], args[2]);
            if (args.Length == 2 && args[0] == "--prism-audit")
                return NetworkSelfTest.PrismRun(args[1]);
            if (args.Length == 2 && args[0] == "--full-pack-self-test")
                return NetworkSelfTest.FullPackRun(args[1]);
            if (args.Length == 2 && args[0] == "--self-test")
                return NetworkSelfTest.Run(args[1]);
            if (args.Length == 3 && args[0] == "--audit-full-pack-release")
                return SelfTest.Audit(args[1], args[2]);
            if (args.Length == 2 && args[0] == "--engine-self-test")
                return SelfTest.Run(args[1]);
            if (args.Length == 4 && args[0] == "--audit-channel")
                return HostedAudit.Run(args[1], args[2], args[3]);
            if (args.Length == 2 && args[0] == "--verify-feed")
            {
                try
                {
                    Channel source;
                    using (var reader = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("Astera.Channel")))
                        source = Engine.Json.Deserialize<Channel>(reader.ReadToEnd());
                    var feed = new UpdateClient(source.PublicKeyXml).Verify(File.ReadAllBytes(args[1]));
                    File.WriteAllText(args[1] + ".verified.json", Engine.Json.Serialize(new
                    {
                        Passed = true,
                        Release = feed.Release.Name,
                        Sequence = feed.Release.Sequence,
                        ManagedFiles = feed.Release.Files.Count,
                        RequiredMods = feed.RequiredMods.Count
                    }));
                    return 0;
                }
                catch (Exception error)
                {
                    File.WriteAllText(args[1] + ".failure.txt", error.ToString());
                    return 1;
                }
            }

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate (object sender, ThreadExceptionEventArgs e)
            {
                AsteraDiagnostics.Report("Windows Forms event", e.Exception);
            };
            AppDomain.CurrentDomain.UnhandledException += delegate (object sender, UnhandledExceptionEventArgs e)
            {
                AsteraDiagnostics.Write("Unhandled process exception", e.ExceptionObject as Exception);
            };
            TaskScheduler.UnobservedTaskException += delegate (object sender, UnobservedTaskExceptionEventArgs e)
            {
                AsteraDiagnostics.Write("Unobserved background task", e.Exception);
                e.SetObserved();
            };
            try
            {
                var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Astera");
                string afterUpdateJob = null;
                if (args.Length == 2 && args[0] == "--after-self-update")
                {
                    afterUpdateJob = Path.GetFullPath(args[1]);
                    root = SelfUpdate.ReadJob(afterUpdateJob).DataRoot;
                }
                else if (args.Length == 2 && args[0] == "--data-dir")
                    root = Path.GetFullPath(args[1]);
                using (var instance = new SingleInstance(root))
                {
                    if (!instance.Owner)
                    {
                        instance.ShowExisting();
                        return 0;
                    }

                    AsteraDiagnostics.Initialize(root);
                    SelfUpdate.CleanupCompleted(root);
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Channel channel;
                    using (var reader = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("Astera.Channel")))
                        channel = Engine.Json.Deserialize<Channel>(reader.ReadToEnd());
                    using (var form = new LibraryForm(new Store(root), channel))
                    {
                        if (afterUpdateJob != null)
                        {
                            form.Shown += delegate
                            {
                                var health = new System.Windows.Forms.Timer
                                {
                                    Interval = 250
                                };
                                health.Tick += delegate
                                {
                                    health.Stop();
                                    health.Dispose();
                                    SelfUpdate.Healthy(afterUpdateJob);
                                };
                                health.Start();
                            };
                        }

                        Application.Run(form);
                    }

                    AsteraDiagnostics.Write("Session ended", null);
                    return 0;
                }
            }
            catch (Exception error)
            {
                AsteraDiagnostics.Report("Startup or message loop", error);
                return 1;
            }
        }
    }
}
