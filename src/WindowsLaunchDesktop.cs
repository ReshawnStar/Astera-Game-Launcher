using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Astera
{
    interface ILaunchDesktop
    {
        IntPtr Foreground();
        string Title(IntPtr window);
        bool FromHost(IntPtr window, string directory);
        bool Minimize(IntPtr window);
        void Restore(Form owner);
    }

    sealed class WindowsLaunchDesktop : ILaunchDesktop
    {
        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int GetWindowText(IntPtr window, StringBuilder title, int length);
        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
        [DllImport("user32.dll")]
        static extern bool ShowWindowAsync(IntPtr window, int command);
        public IntPtr Foreground()
        {
            return GetForegroundWindow();
        }

        public string Title(IntPtr window)
        {
            var text = new StringBuilder(128);
            GetWindowText(window, text, text.Capacity);
            return text.ToString();
        }

        public bool FromHost(IntPtr window, string directory)
        {
            uint pid;
            GetWindowThreadProcessId(window, out pid);
            try
            {
                using (var process = Process.GetProcessById((int)pid))
                {
                    string path = Path.GetFullPath(process.MainModule.FileName);
                    return path.StartsWith(directory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
                }
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        public bool Minimize(IntPtr window)
        {
            return ShowWindowAsync(window, 7);
        }

        public void Restore(Form owner)
        {
            owner.Activate();
        }
    }
}
