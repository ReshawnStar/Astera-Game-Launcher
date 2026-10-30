using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("Gacha Nebula +")]
[assembly: System.Reflection.AssemblyProduct("Gacha Nebula +")]

namespace NebulaStability
{
    public sealed class SaveVault
    {
        private readonly object gate = new object();
        public readonly string Snapshots;
        public string LastBackup { get; private set; }
        public DateTime LastDiskWriteUtc { get; private set; }
        private string lastHash;
        private long lastLength = -1;
        private DateTime lastObservedWriteUtc;
        public SaveVault(string root) { Snapshots = Path.GetFullPath(Path.Combine(root, "Snapshots")); Directory.CreateDirectory(Snapshots); }
        public static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        public static void Validate(byte[] bytes)
        {
            if (bytes.Length < 32 || bytes.Length > 64 * 1024 * 1024 || bytes[0] != 0 || bytes[1] != 191 || Encoding.ASCII.GetString(bytes, 6, 4) != "TCSO") throw new InvalidDataException("Not a complete Flash save container.");
            long length = ((long)bytes[2] << 24) | ((long)bytes[3] << 16) | ((long)bytes[4] << 8) | bytes[5];
            if (length != bytes.Length - 6) throw new InvalidDataException("The save is incomplete or still being written.");
            string content = Encoding.UTF8.GetString(bytes);
            if (!content.Contains("mainstring") || !content.Contains("charstring1") || !content.Contains("charstring20") || !content.Contains("scenestring") || !content.Contains("petstring") || !content.Contains("objectstring")) throw new InvalidDataException("This is not a complete recognized Nebula save.");
        }
        // The loading screen flushes telemetry before the first gameplay save.
        // Recognize ONLY that exact AMF3 state; never import it as character data.
        public static bool IsStartupOnly(byte[] bytes)
        {
            try
            {
                if (bytes.Length < 36 || bytes.Length > 4096 || bytes[0] != 0 || bytes[1] != 191 || Encoding.ASCII.GetString(bytes, 6, 4) != "TCSO") return false;
                long size = ((long)bytes[2] << 24) | ((long)bytes[3] << 16) | ((long)bytes[4] << 8) | bytes[5];
                if (size != bytes.Length - 6 || bytes[10] != 0 || bytes[11] != 4) return false;
                for (int i = 12; i < 16; i++) if (bytes[i] != 0) return false;
                using (var stream = new MemoryStream(bytes)) using (var reader = new BinaryReader(stream))
                {
                    stream.Position = 16;
                    int length = (reader.ReadByte() << 8) | reader.ReadByte();
                    if (length != 14 || Encoding.ASCII.GetString(reader.ReadBytes(length)) != "gachaclub_save") return false;
                    if (reader.ReadByte() != 0 || reader.ReadByte() != 0 || reader.ReadByte() != 0 || reader.ReadByte() != 3) return false;
                    var seen = new HashSet<string>();
                    while (stream.Position < stream.Length)
                    {
                        uint token = ReadU29(reader);
                        if ((token & 1) == 0 || token >> 1 > 64) return false;
                        byte[] nameBytes = reader.ReadBytes((int)(token >> 1));
                        if (nameBytes.Length != (int)(token >> 1)) return false;
                        string name = new UTF8Encoding(false, true).GetString(nameBytes);
                        if ((name != "__nebulaBootLoadMs" && name != "__nebulaBootLoading") || !seen.Add(name)) return false;
                        if (reader.ReadByte() != 4) return false;
                        uint value = ReadU29(reader);
                        if (value >= 0x10000000 || (name == "__nebulaBootLoading" && value > 1) || reader.ReadByte() != 0) return false;
                    }
                    return seen.Count > 0;
                }
            }
            catch (EndOfStreamException) { return false; }
            catch (ArgumentException) { return false; }
        }
        private static uint ReadU29(BinaryReader reader)
        {
            uint value = 0;
            for (int i = 0; i < 3; i++) { byte next = reader.ReadByte(); value = (value << 7) | (uint)(next & 127); if ((next & 128) == 0) return value; }
            return (value << 8) | reader.ReadByte();
        }
        public void BeforeLaunch(string profile)
        {
            lock (gate)
            {
                if (!File.Exists(profile)) return;
                byte[] first = Read(profile);
                if (!IsStartupOnly(first)) { Snapshot(profile, "before-launch", true); return; }
                Thread.Sleep(120); byte[] second = Read(profile);
                if (!IsStartupOnly(second) || Hash(first) != Hash(second)) throw new IOException("Startup profile is still changing. Try again once the game is closed.");
                string folder = Path.Combine(Path.GetDirectoryName(Snapshots), "Initial-profile-evidence"); Directory.CreateDirectory(folder);
                string destination = Path.Combine(folder, Hash(second) + ".sol");
                if (!File.Exists(destination)) File.WriteAllBytes(destination, second);
                if (Hash(Read(destination)) != Hash(second)) throw new IOException("Could not preserve the startup profile. Launch stopped.");
            }
        }
        private static byte[] Read(string path)
        {
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                if (file.Length > 64 * 1024 * 1024) throw new InvalidDataException("Save container is too large.");
                using (var reader = new BinaryReader(file)) return reader.ReadBytes((int)file.Length);
            }
        }
        public string Snapshot(string profile, string reason, bool force)
        {
            lock (gate)
            {
                if (!File.Exists(profile)) return LastBackup;
                var metadata = new FileInfo(profile);
                if (!force && lastHash != null && File.Exists(LastBackup) && metadata.Length == lastLength && metadata.LastWriteTimeUtc == lastObservedWriteUtc) return LastBackup;
                var first = Read(profile); Validate(first); var hash = Hash(first);
                Thread.Sleep(120);
                var second = Read(profile); Validate(second);
                if (hash != Hash(second)) throw new IOException("Save changed during backup; waiting for the next complete write.");
                var afterRead = new FileInfo(profile);
                if (metadata.Length != afterRead.Length || metadata.LastWriteTimeUtc != afterRead.LastWriteTimeUtc) throw new IOException("Save changed during backup; waiting for the next complete write.");
                LastDiskWriteUtc = afterRead.LastWriteTimeUtc;
                lastLength = second.Length; lastObservedWriteUtc = LastDiskWriteUtc;
                if (!force && hash == lastHash && File.Exists(LastBackup)) return LastBackup;
                string prefix = reason.StartsWith("before-", StringComparison.Ordinal) ? "baseline-" : "save-";
                string name = prefix + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-" + hash.Substring(0, 12) + ".sol";
                string destination = Path.Combine(Snapshots, name);
                string temporary = destination + ".pending";
                File.WriteAllBytes(temporary, second);
                if (Hash(File.ReadAllBytes(temporary)) != hash) throw new IOException("Backup verification failed.");
                File.Move(temporary, destination); File.WriteAllText(destination + ".sha256", hash, Encoding.ASCII);
                lastHash = hash; LastBackup = destination;
                File.AppendAllText(Path.Combine(Path.GetDirectoryName(Snapshots), "backup-events.log"), DateTime.UtcNow.ToString("o") + " " + reason + " " + name + Environment.NewLine);
                Prune(); return destination;
            }
        }
        public void Restore(string selected, string profile)
        {
            lock (gate)
            {
                var bytes = Read(selected); Validate(bytes); string hash = Hash(bytes);
                if (File.Exists(selected + ".sha256") && File.ReadAllText(selected + ".sha256").Trim() != hash) throw new InvalidDataException("The backup checksum no longer matches. Existing save left untouched.");
                try { Snapshot(profile, "before-restore", true); }
                catch (InvalidDataException) { PreserveDamagedProfile(profile); }
                Directory.CreateDirectory(Path.GetDirectoryName(profile));
                string temporary = profile + ".restore-" + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    File.WriteAllBytes(temporary, bytes);
                    if (Hash(Read(temporary)) != hash) throw new IOException("Restore verification failed.");
                    if (File.Exists(profile)) File.Replace(temporary, profile, null); else File.Move(temporary, profile);
                    if (Hash(Read(profile)) != hash) throw new IOException("Restored save did not verify.");
                    lastHash = null; Snapshot(profile, "import-or-restore", true);
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
        }
        private void PreserveDamagedProfile(string profile)
        {
            if (!File.Exists(profile)) return;
            var bytes = Read(profile); string hash = Hash(bytes);
            string evidence = Path.Combine(Path.GetDirectoryName(Snapshots), "Damaged-save-evidence"); Directory.CreateDirectory(evidence);
            string destination = Path.Combine(evidence, DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-" + hash.Substring(0, 12) + ".bin");
            File.WriteAllBytes(destination, bytes);
            if (Hash(Read(destination)) != hash) throw new IOException("Could not preserve the damaged current save. Restore stopped.");
        }
        public void Prune()
        {
            var expression = new Regex(@"^save-\d{8}T\d{9}Z-[0-9a-f]{12}\.sol$");
            var files = new DirectoryInfo(Snapshots).GetFiles("save-*.sol").Where(file => expression.IsMatch(file.Name)).OrderByDescending(file => file.LastWriteTimeUtc).ToList();
            var keep = new HashSet<string>(files.Take(120).Select(file => file.FullName), StringComparer.OrdinalIgnoreCase);
            foreach (var daily in files.GroupBy(file => file.LastWriteTimeUtc.Date).Take(14)) keep.Add(daily.Last().FullName);
            if (LastBackup != null) keep.Add(LastBackup);
            foreach (var file in files.Where(file => !keep.Contains(file.FullName)))
            {
                if (!file.FullName.StartsWith(Snapshots + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new IOException("Backup cleanup path escaped its own folder.");
                File.Delete(file.FullName); if (File.Exists(file.FullName + ".sha256")) File.Delete(file.FullName + ".sha256");
            }
        }
        public static int SelfTest(string root)
        {
            Directory.CreateDirectory(root); var profile = Path.Combine(root, "test-profile.sol");
            var bytes = new byte[160]; bytes[1] = 191; Encoding.ASCII.GetBytes("TCSO").CopyTo(bytes, 6); Encoding.ASCII.GetBytes("mainstring charstring1 charstring20 scenestring petstring objectstring").CopyTo(bytes, 32);
            int size = bytes.Length - 6; bytes[2] = (byte)(size >> 24); bytes[3] = (byte)(size >> 16); bytes[4] = (byte)(size >> 8); bytes[5] = (byte)size;
            File.WriteAllBytes(profile, bytes); var vault = new SaveVault(Path.Combine(root, "vault"));
            string first = vault.Snapshot(profile, "autosave", false);
            if (first != vault.Snapshot(profile, "autosave", false)) throw new Exception("Unchanged save was duplicated.");
            var edited = (byte[])bytes.Clone(); edited[120] = 77; File.WriteAllBytes(profile, edited); string next = vault.Snapshot(profile, "autosave", false);
            if (next == first) throw new Exception("Changed save was not backed up.");
            vault.Restore(first, profile); if (Hash(Read(profile)) != Hash(bytes)) throw new Exception("Restore did not reproduce the original save.");
            File.WriteAllBytes(profile, new byte[12]);
            try { vault.Snapshot(profile, "autosave", false); throw new Exception("Truncated save accepted."); } catch (InvalidDataException) { }
            vault.Restore(first, profile); if (Hash(Read(profile)) != Hash(bytes)) throw new Exception("Damaged current save could not be recovered.");
            if (!Directory.GetFiles(Path.Combine(root, "vault", "Damaged-save-evidence")).Any()) throw new Exception("Damaged save evidence was not preserved.");
            File.WriteAllBytes(profile, bytes); File.WriteAllText(next + ".sha256", "wrong");
            try { vault.Restore(next, profile); throw new Exception("Corrupt backup accepted."); } catch (InvalidDataException) { }
            if (Hash(Read(profile)) != Hash(bytes)) throw new Exception("Rejected backup changed the current save.");
            var retention = new SaveVault(Path.Combine(root, "retention"));
            for (int index = 0; index < 150; index++)
            {
                string path = Path.Combine(retention.Snapshots, "save-20260101T" + index.ToString("D9") + "Z-0123456789ab.sol");
                File.WriteAllBytes(path, bytes); File.SetLastWriteTimeUtc(path, DateTime.UtcNow.Date.AddSeconds(index));
            }
            string baseline = Path.Combine(retention.Snapshots, "baseline-preserve.sol"); File.WriteAllBytes(baseline, bytes);
            string unrelated = Path.Combine(retention.Snapshots, "save-user-copy.sol"); File.WriteAllBytes(unrelated, bytes);
            retention.Prune();
            if (Directory.GetFiles(retention.Snapshots, "save-20260101T*.sol").Length != 121 || !File.Exists(baseline) || !File.Exists(unrelated)) throw new Exception("Retention did not preserve recent saves, daily baseline and unrelated files.");
            File.WriteAllText(Path.Combine(root, "result.txt"), "Passed container integrity, stable read, checksum backup, unchanged deduplication, atomic restore, truncated-write rejection and corrupt-backup preservation checks. Fixture tests validate container handling, not game-level AMF schema.\n");
            return 0;
        }
    }

    public sealed class SafeLauncher : Form
    {
        private readonly string gamePath;
        private readonly string profileRoot;
        private readonly SaveVault vault;
        private readonly Label status = new Label();
        private readonly Button play = new Button();
        private readonly Button import = new Button();
        private readonly Button restore = new Button();
        private readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        private readonly NotifyIcon tray = new NotifyIcon();
        private Process game;
        private int copying;
        private bool exiting;

        public SafeLauncher(bool autoPlay)
        {
            string root = AppDomain.CurrentDomain.BaseDirectory;
            gamePath = Path.Combine(root, "game", "Gacha Nebula.exe");
            profileRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "com.starsallies.nebula.stability", "Local Store");
            string recovery = Path.Combine(root, "Recovery");
            try { Directory.CreateDirectory(recovery); } catch (UnauthorizedAccessException) { recovery = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NebulaStability", "Recovery"); }
            vault = new SaveVault(recovery);
            Text = "Gacha Nebula + - save protection"; ClientSize = new Size(660, 350); MinimumSize = new Size(640, 380); StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(22, 20, 48); ForeColor = Color.White; Font = new Font("Segoe UI", 10);
            var title = new Label { Text = "GACHA NEBULA +", Location = new Point(22, 20), Size = new Size(610, 35), Font = new Font("Segoe UI", 15, FontStyle.Bold) };
            var description = new Label { Text = "30-second checkpoints in Home/Studio, save on focus loss, and versioned disk backups.\nThis build has its own save folder. Your original Nebula save is not changed.\nImport a copy of an existing save while the game is closed.", Location = new Point(24, 64), Size = new Size(610, 86) };
            Configure(play, "Play Nebula +", 24, 163, 180); play.Click += delegate { Launch(); };
            var backups = new Button(); Configure(backups, "Open backups", 218, 163, 180); backups.Click += delegate { Process.Start(new ProcessStartInfo(vault.Snapshots) { UseShellExecute = true }); };
            Configure(import, "Import save", 24, 221, 180); import.Click += delegate { ChooseSave(false); };
            Configure(restore, "Restore backup", 218, 221, 180); restore.Click += delegate { ChooseSave(true); };
            status.Location = new Point(24, 280); status.Size = new Size(610, 58); status.Text = "Ready. Backups stay local. Closing this window while playing keeps protection running in the tray.";
            Controls.Add(title); Controls.Add(description); Controls.Add(play); Controls.Add(backups); Controls.Add(import); Controls.Add(restore); Controls.Add(status);
            try { Icon = Icon.ExtractAssociatedIcon(gamePath); } catch { Icon = SystemIcons.Application; }
            tray.Icon = Icon; tray.Text = "Gacha Nebula + save protection"; tray.DoubleClick += delegate { Show(); WindowState = FormWindowState.Normal; Activate(); tray.Visible = false; };
            timer.Interval = 1000; timer.Tick += delegate { MonitorGame(); }; timer.Start();
            FormClosing += delegate(object sender, FormClosingEventArgs e) { if (!exiting && Running()) { e.Cancel = true; Hide(); tray.Visible = true; } };
            FormClosed += delegate { timer.Dispose(); tray.Dispose(); };
            if (autoPlay) Shown += delegate { BeginInvoke((Action)Launch); };
        }
        private void Configure(Button button, string text, int x, int y, int width) { button.Text = text; button.Location = new Point(x, y); button.Size = new Size(width, 44); button.BackColor = Color.FromArgb(69, 49, 115); button.ForeColor = Color.White; button.FlatStyle = FlatStyle.Flat; }
        private bool Running() { try { return game != null && !game.HasExited; } catch { return false; } }
        private string Profile()
        {
            if (Directory.Exists(profileRoot))
            {
                var found = new DirectoryInfo(profileRoot).GetFiles("gachaclub_save.sol", SearchOption.AllDirectories).OrderByDescending(file => file.LastWriteTimeUtc).FirstOrDefault();
                if (found != null) return found.FullName;
            }
            return Path.Combine(profileRoot, "#SharedObjects", "GachaNebula.swf", "gachaclub_save.sol");
        }
        private void Launch()
        {
            if (Running()) return;
            try
            {
                if (!File.Exists(gamePath)) throw new FileNotFoundException("Extract the full package before launching. The game folder is missing.");
                vault.BeforeLaunch(Profile());
                game = Process.Start(new ProcessStartInfo(gamePath) { WorkingDirectory = Path.GetDirectoryName(gamePath), UseShellExecute = false, WindowStyle = ProcessWindowStyle.Normal });
                play.Enabled = import.Enabled = restore.Enabled = false; status.Text = "Game running. Waiting for its next complete disk checkpoint.";
            }
            catch (Exception error) { MessageBox.Show(this, error.Message, "Nebula could not launch", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }
        private void ChooseSave(bool restoring)
        {
            if (Running()) return;
            using (var dialog = new OpenFileDialog { Filter = "Nebula Flash save (*.sol)|*.sol", Title = restoring ? "Choose a backup to restore" : "Choose an existing Nebula save to copy" })
            {
                if (restoring) dialog.InitialDirectory = vault.Snapshots;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                if (MessageBox.Show(this, "Copy this save into the stability build? The current stability save will be backed up first. The selected source file is left unchanged.", "Import / restore", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                try { vault.Restore(dialog.FileName, Profile()); status.Text = "Save copied and verified. Original source left unchanged. You can launch now."; }
                catch (Exception error) { MessageBox.Show(this, error.Message, "Save was not replaced", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            }
        }
        private void MonitorGame()
        {
            if (game == null || Interlocked.CompareExchange(ref copying, 1, 0) != 0) return;
            bool ended = !Running(); int exitCode = ended ? game.ExitCode : 0;
            Task.Run(delegate
            {
                string message;
                try
                {
                    var backup = vault.Snapshot(Profile(), ended ? "after-exit" : "autosave", false);
                    message = backup == null ? "No complete disk save yet. Enter Home/Studio and allow the first checkpoint." : "Complete disk save backed up. Latest file: " + Path.GetFileName(backup);
                    if (ended) message = (exitCode == 0 ? "Game closed. " : "Game stopped unexpectedly. ") + (backup == null ? "No complete checkpoint was available yet." : "Complete save backups are available in Open backups.");
                }
                catch (Exception error) { message = "Backup waiting: " + error.Message + " Previous backups are unchanged."; }
                try
                {
                    BeginInvoke((Action)delegate
                    {
                        status.Text = message;
                        if (ended)
                        {
                            game = null; play.Enabled = import.Enabled = restore.Enabled = true;
                            if (!Visible) { exiting = true; Close(); }
                        }
                    });
                }
                catch (InvalidOperationException) { }
                finally { Interlocked.Exchange(ref copying, 0); }
            });
        }
    }
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length == 2 && args[0] == "--self-test") return SaveVault.SelfTest(args[1]);
            bool created;
            using (var single = new Mutex(true, @"Local\StarsAllies.NebulaStability.Launcher", out created))
            {
                if (!created) { MessageBox.Show("Gacha Nebula + save protection is already running. Open its window or tray icon."); return 0; }
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); Application.Run(new SafeLauncher(args.Contains("--play"))); return 0;
            }
        }
    }
}
