using System;
using System.IO;
using System.Reflection;

[assembly: AssemblyTitle("Astera Update Helper")]
[assembly: AssemblyProduct("Astera Update Helper")]
[assembly: AssemblyCompany("Stars Games")]
[assembly: AssemblyDescription("Verifies and applies signed Astera updates after the launcher exits")]
namespace Astera
{
    public sealed class Channel
    {
        public string PublicKeyXml;
    }

    public sealed class Envelope
    {
        public string Payload, Signature;
    }

    static class UpdaterEntry
    {
        [STAThread]
        static int Main(string[] args)
        {
            AppContext.SetSwitch("Switch.System.IO.UseLegacyPathHandling", false);
            AppContext.SetSwitch("Switch.System.IO.BlockLongPaths", false);
            if (args.Length != 2 || args[0] != "--apply-self-update")
                return 2;
            try
            {
                return SelfUpdate.Apply(Path.GetFullPath(args[1]));
            }
            catch
            {
                return 1;
            }
        }
    }
}
