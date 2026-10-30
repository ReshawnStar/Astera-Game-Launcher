using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Astera
{
    public sealed class AppRelease
    {
        public int Schema;
        public string AppId, Version, Url, Sha256, Notes;
        public long Size, Sequence, ExpiresUtc;
    }

    public sealed class SelfUpdateJob
    {
        public string DataRoot, Target, Candidate, Envelope, ExpectedCurrentHash, Nonce, HelperHash;
        public int OwnerPid;
        public long OwnerStartTicks;
    }

    public sealed class SelfUpdateReceipt
    {
        public string Status, Message, BackupHash;
        public int NewPid;
    }

    static class SelfUpdate
    {
        internal static Channel Embedded()
        {
            using (var r = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("Astera.Channel")))
                return SelfUpdateFiles.Json.Deserialize<Channel>(r.ReadToEnd());
        }

        internal static string CurrentVersion
        {
            get
            {
                return Assembly.GetExecutingAssembly().GetName().Version.ToString(3);
            }
        }

        internal static Version ParseVersion(string text)
        {
            Version v;
            if (!Version.TryParse(text, out v) || v.Major < 0 || v.Minor < 0 || v.Build < 0)
                throw new Exception("Invalid launcher version");
            return v;
        }

        internal static AppRelease Verify(byte[] bytes, string publicKey)
        {
            if (bytes.Length > 1024 * 1024)
                throw new Exception("Launcher notice is too large");
            var signed = SelfUpdateFiles.Json.Deserialize<Envelope>(Encoding.UTF8.GetString(bytes));
            if (signed == null)
                throw new Exception("Missing signed launcher notice");
            var payload = Convert.FromBase64String(signed.Payload);
            using (var rsa = new RSACryptoServiceProvider(2048, new CspParameters(24)))
            {
                rsa.PersistKeyInCsp = false;
                rsa.FromXmlString(publicKey);
                if (!rsa.VerifyData(payload, "SHA256", Convert.FromBase64String(signed.Signature)))
                    throw new Exception("The launcher update signature is invalid");
            }

            var release = SelfUpdateFiles.Json.Deserialize<AppRelease>(Encoding.UTF8.GetString(payload));
            if (release == null || release.Schema != 1 || release.AppId != "astera-launcher" || release.Size < 1024 || release.Size > 128 * 1024 * 1024 || release.Sequence < 1 || release.ExpiresUtc < DateTimeOffset.UtcNow.ToUnixTimeSeconds() || release.Notes == null || release.Notes.Length > 16000 || release.Sha256 == null || !System.Text.RegularExpressions.Regex.IsMatch(release.Sha256, "^[a-fA-F0-9]{64}$"))
                throw new Exception("Invalid or expired launcher release");
            ParseVersion(release.Version);
            Uri address;
            if (!Uri.TryCreate(release.Url, UriKind.Absolute, out address) || address.Scheme != "https" || !String.IsNullOrEmpty(address.UserInfo) || !String.IsNullOrEmpty(address.Fragment))
                throw new Exception("Launcher update address must use HTTPS without credentials");
            return release;
        }

#if !ASTERA_UPDATER
        internal static byte[] Check(Channel channel, CancellationToken cancel)
        {
            return new Transport().Metadata(channel.AppFeedUrl, cancel);
        }

#endif
        static string Root(string data)
        {
            string folder = SelfUpdateFiles.Inside(SelfUpdateFiles.Root(data), "self-update");
            Directory.CreateDirectory(folder);
            return folder;
        }

        static void Atomic(string path, byte[] bytes)
        {
            SelfUpdateFiles.Plain(path);
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".next";
            try
            {
                File.WriteAllBytes(temp, bytes);
                if (File.Exists(path))
                    File.Replace(temp, path, null);
                else
                    File.Move(temp, path);
            }
            finally
            {
                if (File.Exists(temp))
                    File.Delete(temp);
            }
        }

        internal static void Save(string path, object value)
        {
            Atomic(path, Encoding.UTF8.GetBytes(SelfUpdateFiles.Json.Serialize(value)));
        }

#if !ASTERA_UPDATER
        internal static string Prepare(Store store, byte[] envelope, CancellationToken cancel, Action<string> log, Action<string, long, long> progress = null)
        {
            if (!File.Exists(System.Windows.Forms.Application.ExecutablePath))
                throw new IOException("The Astera executable is missing or blocked. Check Windows Security before updating; no replacement was attempted.");
            var release = Verify(envelope, Embedded().PublicKeyXml);
            if (ParseVersion(release.Version) <= ParseVersion(CurrentVersion) || release.Sequence < store.Settings.HighestAppSequence)
                throw new Exception("This launcher update is not newer than the installed version");
            string root = Root(store.Root), jobFolder = SelfUpdateFiles.Inside(root, "jobs/" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(jobFolder);
            string candidate = Path.Combine(jobFolder, "candidate.exe"), notice = Path.Combine(jobFolder, "release.json");
            log("Downloading Astera " + release.Version);
            try
            {
                using (var output = new FileStream(candidate, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    new Transport().Get(release.Url, output, release.Size, release.Size, cancel, delegate (long done)
                    {
                        if (progress != null)
                            progress("Downloading", done, release.Size);
                    });
                if (progress != null)
                    progress("Verifying", 0, 0);
                ValidateCandidate(candidate, release);
                File.WriteAllBytes(notice, envelope);
                string target = Path.GetFullPath(System.Windows.Forms.Application.ExecutablePath);
                SelfUpdateFiles.Plain(target);
                if (!Directory.Exists(Path.GetDirectoryName(target)))
                    throw new Exception("Launcher folder unavailable");
                string probe = Path.Combine(Path.GetDirectoryName(target), ".astera-write-" + Guid.NewGuid().ToString("N"));
                using (File.Create(probe))
                {
                }

                File.Delete(probe);
                var process = Process.GetCurrentProcess();
                var job = new SelfUpdateJob
                {
                    DataRoot = store.Root,
                    Target = target,
                    Candidate = candidate,
                    Envelope = notice,
                    ExpectedCurrentHash = SelfUpdateFiles.Hash(target),
                    HelperHash = UpdaterIdentity.Sha256,
                    OwnerPid = process.Id,
                    OwnerStartTicks = process.StartTime.ToUniversalTime().Ticks,
                    Nonce = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N")
                };
                string path = Path.Combine(jobFolder, "job.json");
                Save(path, job);
                store.Settings.HighestAppSequence = Math.Max(store.Settings.HighestAppSequence, release.Sequence);
                store.Save();
                return path;
            }
            catch
            {
                if (File.Exists(candidate))
                    File.Delete(candidate);
                throw;
            }
        }

#endif
        internal static void ValidateCandidate(string file, AppRelease release)
        {
            SelfUpdateFiles.Plain(file);
            if (new FileInfo(file).Length != release.Size || !SelfUpdateFiles.Hash(file).Equals(release.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new Exception("Launcher download checksum mismatch");
            var version = FileVersionInfo.GetVersionInfo(file);
            if (version.ProductName != "Astera Game Launcher" || ParseVersion(version.FileVersion) != ParseVersion(release.Version + ".0"))
                throw new Exception("Downloaded executable is not the announced Astera build");
        }

#if !ASTERA_UPDATER
        internal static string StartHelper(string jobFile)
        {
            var job = ReadJob(jobFile);
            var release = Verify(File.ReadAllBytes(job.Envelope), Embedded().PublicKeyXml);
            ValidateCandidate(job.Candidate, release);
            if (job.HelperHash != UpdaterIdentity.Sha256)
                throw new IOException("Update job does not name this trusted helper");
            string helper = Path.Combine(Path.GetDirectoryName(jobFile), "Astera.Updater.exe");
            SelfUpdateFiles.Plain(helper);
            byte[] bytes;
            using (var source = typeof(SelfUpdate).Assembly.GetManifestResourceStream("Astera.Updater"))
            using (var memory = new MemoryStream())
            {
                if (source == null)
                    throw new IOException("The dedicated updater is missing");
                source.CopyTo(memory);
                bytes = memory.ToArray();
            }

            if (SelfUpdateFiles.Hash(bytes) != UpdaterIdentity.Sha256)
                throw new IOException("Embedded updater checksum mismatch");
            using (var output = new FileStream(helper, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                output.Write(bytes, 0, bytes.Length);
            }

            if (SelfUpdateFiles.Hash(helper) != UpdaterIdentity.Sha256 || FileVersionInfo.GetVersionInfo(helper).ProductName != "Astera Update Helper")
                throw new IOException("Updater verification failed");
            using (var process = Process.Start(new ProcessStartInfo(helper, "--apply-self-update " + SelfUpdateFiles.Quote(jobFile)) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(helper) }))
            {
                Save(Path.Combine(Path.GetDirectoryName(jobFile), "helper-started.json"), new
                {
                    Pid = process.Id,
                    Path = helper,
                    Sha256 = UpdaterIdentity.Sha256
                });
            }

            return helper;
        }

#endif
        internal static SelfUpdateJob ReadJob(string jobFile)
        {
            SelfUpdateFiles.Plain(jobFile);
            var job = SelfUpdateFiles.Json.Deserialize<SelfUpdateJob>(File.ReadAllText(jobFile));
            if (job == null || String.IsNullOrWhiteSpace(job.DataRoot) || job.OwnerPid < 1 || job.Nonce == null || job.Nonce.Length != 64)
                throw new Exception("Invalid self-update job");
            string root = Root(job.DataRoot);
            string folder = Path.GetDirectoryName(Path.GetFullPath(jobFile));
            if (!folder.StartsWith(SelfUpdateFiles.Inside(root, "jobs") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new Exception("Update job is outside its own workspace");
            SelfUpdateFiles.Plain(folder);
            if (Path.GetFullPath(job.Candidate) != Path.Combine(folder, "candidate.exe") || Path.GetFullPath(job.Envelope) != Path.Combine(folder, "release.json"))
                throw new Exception("Update job paths changed");
            string target = Path.GetFullPath(job.Target);
            SelfUpdateFiles.Plain(target);
            if (!File.Exists(target) || FileVersionInfo.GetVersionInfo(target).ProductName != "Astera Game Launcher")
                throw new Exception("Self-update target is not Astera");
            return job;
        }

        internal static int Apply(string jobFile)
        {
            SelfUpdateJob job = null;
            string result = Path.Combine(Path.GetDirectoryName(jobFile), "result.json"), backup = null, pending = null;
            bool replaced = false;
            Process launched = null;
            try
            {
                job = ReadJob(jobFile);
                var release = Verify(File.ReadAllBytes(job.Envelope), Embedded().PublicKeyXml);
                ValidateCandidate(job.Candidate, release);
                if (ParseVersion(release.Version) <= ParseVersion(FileVersionInfo.GetVersionInfo(job.Target).FileVersion) || release.Sequence < SelfUpdateFiles.HighestSequence(job.DataRoot))
                    throw new Exception("Launcher rollback/downgrade refused");
                try
                {
                    using (var owner = Process.GetProcessById(job.OwnerPid))
                    {
                        if (owner.StartTime.ToUniversalTime().Ticks != job.OwnerStartTicks)
                            throw new Exception("Parent process identity changed");
                        if (!owner.WaitForExit(120000))
                            throw new Exception("Astera did not close; no update applied");
                    }
                }
                catch (ArgumentException)
                {
                }

                if (SelfUpdateFiles.Hash(job.Target) != job.ExpectedCurrentHash)
                    throw new Exception("Astera changed while the update was pending");
                string ledger = SelfUpdateFiles.Inside(Root(job.DataRoot), "installed-" + SelfUpdateFiles.Hash(Encoding.UTF8.GetBytes(job.Target)) + ".json");
                backup = job.Target + ".astera-rollback.exe";
                SelfUpdateFiles.Plain(backup);
                if (File.Exists(backup))
                {
                    if (!File.Exists(ledger) || SelfUpdateFiles.Json.Deserialize<SelfUpdateReceipt>(File.ReadAllText(ledger)).BackupHash != SelfUpdateFiles.Hash(backup))
                        throw new Exception("Existing rollback file was changed; it was preserved");
                    File.Delete(backup);
                }

                pending = job.Target + "." + Guid.NewGuid().ToString("N") + ".pending";
                File.Copy(job.Candidate, pending);
                ValidateCandidate(pending, release);
                File.Replace(pending, job.Target, backup);
                replaced = true;
                launched = Process.Start(new ProcessStartInfo(job.Target, "--after-self-update " + SelfUpdateFiles.Quote(jobFile)) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(job.Target) });
                string healthy = Path.Combine(Path.GetDirectoryName(jobFile), "healthy.json");
                var watch = Stopwatch.StartNew();
                bool ready = false;
                while (watch.ElapsedMilliseconds < 25000)
                {
                    if (launched.HasExited)
                        break;
                    if (File.Exists(healthy))
                    {
                        var proof = ReadHealth(healthy);
                        if (proof != null && proof.Nonce == job.Nonce && String.Equals(proof.Hash, release.Sha256, StringComparison.OrdinalIgnoreCase))
                        {
                            ready = true;
                            break;
                        }
                    }

                    Thread.Sleep(100);
                }

                if (!ready)
                    throw new Exception("The new Astera build did not finish startup");
                Thread.Sleep(400);
                if (launched.HasExited)
                    throw new Exception("The new Astera build exited during startup");
                var receipt = new SelfUpdateReceipt
                {
                    Status = "installed",
                    Message = "Astera " + release.Version + " started successfully",
                    BackupHash = job.ExpectedCurrentHash,
                    NewPid = launched.Id
                };
                Save(result, receipt);
                Save(ledger, receipt);
                return 0;
            }
            catch (Exception error)
            {
                if (job == null)
                    return 1;
                if (replaced && job != null)
                {
                    try
                    {
                        if (launched != null && !launched.HasExited)
                        {
                            launched.Kill();
                            launched.WaitForExit();
                        }

                        if (!File.Exists(job.Target))
                            throw new IOException("Astera was removed or blocked after replacement; automatic restoration stopped. Check Windows Security.");
                        if (SelfUpdateFiles.Hash(backup) != job.ExpectedCurrentHash)
                            throw new Exception("Rollback backup checksum mismatch");
                        File.Replace(backup, job.Target, null);
                        Save(result, new SelfUpdateReceipt { Status = "rolled-back", Message = error.Message });
                        Process.Start(new ProcessStartInfo(job.Target, "--data-dir " + SelfUpdateFiles.Quote(job.DataRoot)) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(job.Target) });
                    }
                    catch (Exception recovery)
                    {
                        Save(result, new SelfUpdateReceipt { Status = "recovery-required", Message = error.Message + "; " + recovery.Message });
                    }
                }
                else
                {
                    Save(result, new SelfUpdateReceipt { Status = "not-installed", Message = error.Message });
                    if (job != null)
                        try
                        {
                            Process.Start(new ProcessStartInfo(job.Target, "--data-dir " + SelfUpdateFiles.Quote(job.DataRoot)) { UseShellExecute = false });
                        }
                        catch
                        {
                        }
                }

                return 1;
            }
            finally
            {
                if (launched != null)
                    launched.Dispose();
                if (pending != null && File.Exists(pending))
                    File.Delete(pending);
            }
        }

#if !ASTERA_UPDATER
        internal static void CleanupCompleted(string data)
        {
            string jobs = SelfUpdateFiles.Inside(Root(data), "jobs");
            if (!Directory.Exists(jobs))
                return;
            foreach (string folder in Directory.GetDirectories(jobs))
            {
                try
                {
                    SelfUpdateFiles.Plain(folder);
                    string result = Path.Combine(folder, "result.json"), jobPath = Path.Combine(folder, "job.json");
                    if (!File.Exists(result) || !File.Exists(jobPath))
                        continue;
                    SelfUpdateFiles.Plain(result);
                    if (File.GetLastWriteTimeUtc(result) > DateTime.UtcNow.AddMinutes(-2))
                        continue;
                    var receipt = SelfUpdateFiles.Json.Deserialize<SelfUpdateReceipt>(File.ReadAllText(result));
                    if (receipt == null || !(receipt.Status == "installed" || receipt.Status == "rolled-back" || receipt.Status == "not-installed" || receipt.Status == "cancelled"))
                        continue;
                    var job = ReadJob(jobPath);
                    if (!String.Equals(job.DataRoot, data, StringComparison.OrdinalIgnoreCase))
                        continue;
                    var release = Verify(File.ReadAllBytes(job.Envelope), Embedded().PublicKeyXml);
                    string candidate = job.Candidate, helper = Path.Combine(folder, "Astera.Updater.exe");
                    if (File.Exists(candidate))
                    {
                        ValidateCandidate(candidate, release);
                        File.Delete(candidate);
                    }

                    if (File.Exists(helper))
                    {
                        SelfUpdateFiles.Plain(helper);
                        if (job.HelperHash == UpdaterIdentity.Sha256 && SelfUpdateFiles.Hash(helper) == job.HelperHash)
                            File.Delete(helper);
                    }
                }
                catch (Exception error)
                {
                    AsteraDiagnostics.Write("Completed self-update cache retained", new IOException(error.GetType().Name));
                }
            }
        }

#endif
        internal static SelfHealth ReadHealth(string path)
        {
            try
            {
                SelfUpdateFiles.Plain(path);
                using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    if (file.Length > 4096)
                        throw new InvalidDataException("Startup confirmation is too large");
                    using (var reader = new StreamReader(file, Encoding.UTF8))
                        return SelfUpdateFiles.Json.Deserialize<SelfHealth>(reader.ReadToEnd());
                }
            }
            catch (IOException)
            {
                return null;
            }
        }

        internal static int AuditHealthRead(string root)
        {
            try
            {
                if (Directory.Exists(root))
                    throw new IOException("Use a fresh startup-confirmation audit");
                Directory.CreateDirectory(root);
                var path = Path.Combine(root, "healthy.json");
                Save(path, new SelfHealth { Nonce = "sharing-fixture", Hash = new String('a', 64) });
                using (var blocked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    if (ReadHealth(path) != null)
                        throw new Exception("An exclusively locked confirmation was accepted");
                var proof = ReadHealth(path);
                if (proof == null || proof.Nonce != "sharing-fixture")
                    throw new Exception("Confirmation did not recover after its temporary lock");
                if (ReadHealth(Path.Combine(root, "not-yet-created.json")) != null)
                    throw new Exception("Missing confirmation was accepted");
                File.WriteAllText(Path.Combine(root, "result.json"), SelfUpdateFiles.Json.Serialize(new
                {
                    Passed = true,
                    Checks = new[] { "Temporarily locked or not-yet-created startup confirmations remain pending until the existing deadline", "A complete confirmation becomes readable after a transient file lock is released" }
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

        internal static void Healthy(string jobFile)
        {
            var job = ReadJob(jobFile);
            var release = Verify(File.ReadAllBytes(job.Envelope), Embedded().PublicKeyXml);
            string current = System.Windows.Forms.Application.ExecutablePath;
            ValidateCandidate(current, release);
            Save(Path.Combine(Path.GetDirectoryName(jobFile), "healthy.json"), new SelfHealth { Nonce = job.Nonce, Hash = SelfUpdateFiles.Hash(current) });
        }
    }

    public sealed class SelfHealth
    {
        public string Nonce, Hash;
    }
}
