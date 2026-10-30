using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Astera
{
    sealed class CurseForgeBackground
    {
        readonly string hostDirectory;
        readonly ILaunchDesktop desktop;
        bool allowed, lastWasOwner;
        internal int BackgroundedWindows;
        internal readonly bool InitiallyArmed;
        internal CurseForgeBackground(Form owner, ProcessStartInfo command, ILaunchDesktop desktop = null)
        {
            this.desktop = desktop ?? new WindowsLaunchDesktop();
            hostDirectory = command.UseShellExecute ? "" : Path.GetDirectoryName(command.FileName);
            InitiallyArmed = allowed = hostDirectory.Length > 0 && this.desktop.Foreground() == owner.Handle;
            lastWasOwner = InitiallyArmed;
        }

        internal void Stop()
        {
            allowed = false;
        }

        internal void Pulse(Form owner)
        {
            if (!allowed || owner.IsDisposed || !owner.Visible || owner.WindowState == FormWindowState.Minimized)
                return;
            IntPtr foreground = desktop.Foreground();
            if (foreground == owner.Handle)
            {
                lastWasOwner = true;
                return;
            }

            if (foreground == IntPtr.Zero)
                return;
            if (desktop.Title(foreground) != "CurseForge")
            {
                lastWasOwner = false;
                return;
            }

            if (!desktop.FromHost(foreground, hostDirectory))
            {
                allowed = false;
                return;
            }

            if (desktop.Minimize(foreground))
            {
                BackgroundedWindows++;
                allowed = false;
                if (lastWasOwner)
                    desktop.Restore(owner);
            }
        }
    }
}
