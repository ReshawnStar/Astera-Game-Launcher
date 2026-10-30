using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace MC2Pack
{
    public static partial class Engine
    {
        internal static void RetrySharing(Action action)
        {
            var clock = Stopwatch.StartNew();
            for (; ; )
            {
                try
                {
                    action();
                    return;
                }
                catch (IOException error)
                {
                    int code = error.HResult & 0xffff;
                    if ((code != 32 && code != 33) || clock.ElapsedMilliseconds >= 4000)
                        throw;
                    Thread.Sleep(100);
                }
            }
        }

        static void CheckEditTarget(Edit edit, string full)
        {
            if (File.Exists(full) != edit.Existed || (edit.Existed && Hash(full) != edit.Expected))
                throw new Exception("Target changed during installation; preserved: " + edit.Path);
        }

        static void CommitEdit(Plan plan, string backup, Edit edit, Action<string> log)
        {
            string full = Inside(plan.Profile, edit.Path);
            if (edit.Desired == null)
            {
                RetrySharing(delegate
                {
                    CheckEditTarget(edit, full);
                    if (File.Exists(full))
                        File.Delete(full);
                });
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(full));
            // Same volume as the profile, outside the active mods/resource-pack folders.
            string pending = Inside(backup, "pending/" + Guid.NewGuid().ToString("N") + ".tmp");
            Directory.CreateDirectory(Path.GetDirectoryName(pending));
            try
            {
                RetrySharing(delegate
                {
                    File.Copy(Inside(backup, "new/" + edit.Path), pending, true);
                });
                RetrySharing(delegate
                {
                    if (Hash(pending) != edit.Desired)
                        throw new Exception("Installed hash mismatch.");
                });
                RetrySharing(delegate
                {
                    CheckEditTarget(edit, full);
                    if (edit.Existed)
                        File.Replace(pending, full, null);
                    else
                        File.Move(pending, full);
                });
            }
            finally
            {
                try
                {
                    if (File.Exists(pending))
                        File.Delete(pending);
                }
                catch (IOException)
                {
                    log("Temporary staging file retained in rollback backup.");
                }
                catch (UnauthorizedAccessException)
                {
                    log("Temporary staging file retained in rollback backup.");
                }
            }
        }
    }
}
