using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using MC2Pack;

namespace Astera
{
    sealed class SingleInstance : IDisposable
    {
        readonly Mutex mutex;
        internal readonly bool Owner;
        readonly int message;
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int RegisterWindowMessage(string text);
        [DllImport("user32.dll")]
        static extern bool PostMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
        static string Key(string root)
        {
            return Engine.Hash(System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(root).TrimEnd('\\', '/').ToUpperInvariant()));
        }

        internal static int MessageFor(string root)
        {
            return RegisterWindowMessage("Astera.Show." + Key(root));
        }

        internal SingleInstance(string root)
        {
            string key = Key(root);
            message = MessageFor(root);
            mutex = new Mutex(false, @"Local\Astera." + key);
            try
            {
                Owner = mutex.WaitOne(0);
            }
            catch (AbandonedMutexException)
            {
                Owner = true;
            }
        }

        internal void ShowExisting()
        {
            PostMessage(new IntPtr(0xffff), message, IntPtr.Zero, IntPtr.Zero);
        }

        public void Dispose()
        {
            if (Owner)
                mutex.ReleaseMutex();
            mutex.Dispose();
        }
    }
}
