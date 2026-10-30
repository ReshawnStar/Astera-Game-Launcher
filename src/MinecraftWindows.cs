using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using MC2Pack;

namespace Astera
{
    // Window readiness is separate from process/playtime detection. Existing Java
    // windows cannot complete a newly requested profile launch.
    static class MinecraftWindows
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern bool IsWindowVisible(IntPtr window);
        delegate bool WindowVisitor(IntPtr window, IntPtr unused);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern bool EnumWindows(WindowVisitor visitor, IntPtr unused);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        static extern int GetWindowText(IntPtr window, System.Text.StringBuilder title, int size);
        internal static HashSet<IntPtr> Visible()
        {
            var result = new HashSet<IntPtr>();
            var java = new HashSet<int>();
            foreach (string name in new[]
            {
                "javaw",
                "java"
            }

            )
                foreach (var process in Process.GetProcessesByName(name))
                {
                    try
                    {
                        java.Add(process.Id);
                    }
                    catch (InvalidOperationException)
                    {
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }

            EnumWindows(delegate (IntPtr window, IntPtr unused)
            {
                uint pid;
                GetWindowThreadProcessId(window, out pid);
                if (java.Contains((int)pid) && IsWindowVisible(window))
                {
                    var title = new System.Text.StringBuilder(256);
                    GetWindowText(window, title, title.Capacity);
                    if (title.ToString().StartsWith("Minecraft", StringComparison.OrdinalIgnoreCase))
                        result.Add(window);
                }

                return true;
            }, IntPtr.Zero);
            return result;
        }

        internal static bool NewWindow(IEnumerable<IntPtr> visible, HashSet<IntPtr> existing)
        {
            foreach (var window in visible)
                if (window != IntPtr.Zero && (existing == null || !existing.Contains(window)))
                    return true;
            return false;
        }

        internal static bool Ready(CustomGame game, HashSet<IntPtr> existing)
        {
            return PlaytimeProbe.Running(game) && NewWindow(Visible(), existing);
        }
    }
}
