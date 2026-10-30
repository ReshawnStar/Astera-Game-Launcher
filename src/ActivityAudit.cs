using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MC2Pack;

namespace Astera
{
    sealed partial class LibraryForm
    {
        static void WaitActivityTask(Task task)
        {
            var watch = Stopwatch.StartNew();
            while (!task.IsCompleted && watch.ElapsedMilliseconds < 7000)
                MotionAudit.Pump(10);
            AuditAssert(task.Status == TaskStatus.RanToCompletion, "Activity worker failed or timed out");
            MotionAudit.Pump(50);
        }

        internal static int AuditActivity(string root)
        {
            try
            {
                if (Directory.Exists(root))
                    throw new IOException("Use a new activity audit directory");
                Directory.CreateDirectory(root);
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                var checks = new List<string>();
                var state = new Store(Path.Combine(root, "state"));
                state.Settings.CheckOnStart = false;
                state.Settings.UseMinecraftSkin = false;
                state.Settings.AnimatedEffects = false;
                CustomGame a = new CustomGame
                {
                    Name = "Unrelated itch game",
                    Executable = root
                }, b = new CustomGame
                {
                    Name = "Another game",
                    Executable = root
                };
                state.Settings.CustomGames.Add(a);
                state.Settings.CustomGames.Add(b);
                using (var form = new LibraryForm(state, new Channel(), true))
                {
                    form.Location = new Point(-30000, -30000);
                    form.Show();
                    form.SelectLibraryTab(1);
                    Application.DoEvents();
                    form.ReloadGames(a);
                    string notes = form.notes.Text;
                    form.LogActivity("builtin:0", "Minecraft fixture files match");
                    form.LogActivity("builtin:2", "Multiverse fixture ready");
                    form.LogActivity("app", "Astera fixture update ready");
                    AuditAssert(!form.log.Text.Contains("fixture") && form.notes.Text == notes, "Unrelated game displayed a managed/app update or lost its own notes");
                    form.GameKind = 0;
                    AuditAssert(form.log.Text.Contains("Minecraft fixture") && !form.log.Text.Contains("Multiverse") && !form.log.Text.Contains("Astera fixture"), "Minecraft activity mixed with another owner");
                    form.GameKind = 2;
                    AuditAssert(form.log.Text.Contains("Multiverse fixture") && !form.log.Text.Contains("Minecraft"), "FTL activity mixed with Minecraft");
                    form.ReloadGames(a);
                    checks.Add("Minecraft, FTL and Astera activity stay out of unrelated game pages, while each game retains its own messages");
                    using (var gate = new ManualResetEventSlim(false))
                    {
                        var started = new TaskCompletionSource<bool>();
                        Task work = form.Work(delegate
                        {
                            started.SetResult(true);
                            if (!gate.Wait(5000))
                                throw new IOException("Fixture gate timed out");
                            form.Log("Delayed Minecraft result");
                        }, true, "builtin:0");
                        WaitActivityTask(started.Task);
                        form.ReloadGames(b);
                        AuditAssert(!form.workPanel.Visible, "Background Minecraft progress appeared on another game's page");
                        gate.Set();
                        WaitActivityTask(work);
                        AuditAssert(!form.log.Text.Contains("Delayed") && form.Activity("builtin:0").Text.Contains("Delayed Minecraft"), "Late worker result used current selection instead of operation ownership");
                    }

                    Task failure = form.Work(delegate
                    {
                        throw new IOException("FTL fixture failure");
                    }, true, "builtin:2");
                    WaitActivityTask(failure);
                    AuditAssert(form.Activity("builtin:2").Text.Contains("FTL fixture failure") && !form.log.Text.Contains("FTL fixture"), "Background error was assigned to the selected custom game");
                    form.Log("Current custom result");
                    AuditAssert(form.log.Text.Contains("Current custom result") && !form.Activity("builtin:2").Text.Contains("Current custom result"), "Async owner leaked into the next UI action");
                    checks.Add("Delayed results and errors preserve their operation owner across selection changes; ambient routing resets afterward");
                    form.LogActivity("builtin:0", "Same fixture message");
                    form.LogActivity("builtin:0", "Same fixture message");
                    form.LogActivity("builtin:2", "Same fixture message");
                    AuditAssert(form.Activity("builtin:0").Text.Split(new[] { "Same fixture message" }, StringSplitOptions.None).Length == 2 && form.Activity("builtin:2").Text.Contains("Same fixture message"), "Deduplication crossed games or repeated within one game");
                    checks.Add("Duplicate suppression is per game rather than shared across the library");
                    form.ReloadGames(a);
                    for (int i = 0; i < 100; i++)
                        form.Log("Game A entry " + i);
                    form.log.MoveTo(10);
                    int line = form.log.FirstLine;
                    form.ReloadGames(b);
                    form.LogActivity("builtin:0", "Another background result");
                    form.ReloadGames(a);
                    AuditAssert(line > 0 && form.log.FirstLine == line, "Returning to a game's activity lost its saved scroll position");
                    int changes = 0;
                    form.log.TextChanged += delegate
                    {
                        changes++;
                    };
                    form.RefreshView();
                    form.RefreshView();
                    AuditAssert(changes == 0, "Unchanged refresh rewrote activity text");
                    checks.Add("Per-game scroll position survives switching and unchanged refreshes leave text untouched");
                    var bounded = new GameActivity();
                    for (int i = 0; i < 1000; i++)
                        bounded.Add(i + new String('x', 1000));
                    AuditAssert(bounded.Messages.Count <= 200 && bounded.Characters <= 64000, "Activity retained unbounded history");
                    checks.Add("In-memory activity is bounded; complete troubleshooting output remains in diagnostic logs");
                    form.ReloadGames(b);
                    form.SelectLibraryTab(1);
                    form.TopMost = true;
                    form.Location = Screen.PrimaryScreen.WorkingArea.Location;
                    form.BringToFront();
                    form.Activate();
                    form.Refresh();
                    MotionAudit.Pump(400);
                    form.CaptureVisibleUi(root, "unrelated-game-activity");
                    AuditAssert(form.Activity("app").Text.Contains("Astera fixture") && !form.log.Text.Contains("Astera fixture"), "Launcher update activity leaked into game activity");
                    checks.Add("Astera activity has a separate launcher history, accessible from its dedicated menu action");
                    form.Close();
                }

                File.WriteAllText(Path.Combine(root, "result.json"), Engine.Json.Serialize(new
                {
                    Passed = true,
                    Checks = checks,
                    NetworkRequests = false,
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
