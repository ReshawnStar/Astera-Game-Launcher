using System;
using System.IO;
using MC2Pack;

namespace Astera
{
    public static class LocalStarDash
    {
        public static string DefaultAt(string documents)
        {
            string path = Path.Combine(documents, "Stardash", "StarDash.exe");
            return IsValid(path) ? path : "";
        }

        public static bool IsValid(string executable)
        {
            try
            {
                Validate(executable);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void Validate(string executable)
        {
            if (String.IsNullOrWhiteSpace(executable) || !Path.GetFileName(executable).Equals("StarDash.exe", StringComparison.OrdinalIgnoreCase))
                throw new Exception("Choose the exported StarDash.exe.");
            var root = Engine.Root(Path.GetDirectoryName(Path.GetFullPath(executable)));
            var exe = Engine.Inside(root, Path.GetFileName(executable));
            var pack = Engine.Inside(root, "StarDash.pck");
            if (!File.Exists(exe) || !File.Exists(pack))
                throw new Exception("Keep StarDash.exe and StarDash.pck together in the same folder.");
            using (var stream = File.OpenRead(exe))
            {
                if (stream.ReadByte() != 77 || stream.ReadByte() != 90)
                    throw new Exception("The selected file is not a Windows executable.");
            }

            using (var stream = File.OpenRead(pack))
            {
                if (stream.ReadByte() != 71 || stream.ReadByte() != 68 || stream.ReadByte() != 80 || stream.ReadByte() != 67)
                    throw new Exception("StarDash.pck is not a recognised Godot export pack.");
            }
        }
    }

    public static class UiText
    {
        public static string WindowsLines(string text)
        {
            return (text ?? "").Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");
        }
    }
}
