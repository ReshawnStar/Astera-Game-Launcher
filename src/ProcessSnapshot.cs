using System;
using System.Diagnostics;

namespace Astera
{
    // Own every Process wrapper returned by an enumeration, even when a search
    // finds its answer early or throws. Disposing a wrapper never stops a game.
    sealed class ProcessSnapshot : IDisposable
    {
        internal readonly Process[] Items;
        bool disposed;
        internal ProcessSnapshot(Process[] processes)
        {
            Items = processes;
        }

        internal static ProcessSnapshot Named(string name)
        {
            return new ProcessSnapshot(Process.GetProcessesByName(name));
        }

        internal static ProcessSnapshot All()
        {
            return new ProcessSnapshot(Process.GetProcesses());
        }

        internal static bool Exists(string name)
        {
            using (var snapshot = Named(name))
            {
                return snapshot.Items.Length != 0;
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            foreach (var process in Items)
            {
                process.Dispose();
            }
        }
    }
}
