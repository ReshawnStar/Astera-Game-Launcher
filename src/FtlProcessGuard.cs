using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Astera
{
    static partial class Multiverse
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern uint GetFinalPathNameByHandle(SafeFileHandle file, StringBuilder name, uint size, uint flags);
        static string PhysicalExecutable(string path)
        {
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                var name = new StringBuilder(32768);
                uint length = GetFinalPathNameByHandle(file.SafeFileHandle, name, (uint)name.Capacity, 0);
                if (length == 0 || length >= name.Capacity)
                    throw new IOException("Cannot resolve the running FTL executable location");
                return name.ToString();
            }
        }
    }
}
