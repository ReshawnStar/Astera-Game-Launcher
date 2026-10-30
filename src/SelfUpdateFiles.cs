using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace Astera
{
    // Shared by the launcher and the small offline updater. No game operations.
    static class SelfUpdateFiles
    {
        internal static readonly JavaScriptSerializer Json = new JavaScriptSerializer
        {
            MaxJsonLength = 1024 * 1024
        };
        internal static string Hash(byte[] data)
        {
            using (var s = new MemoryStream(data))
                return Hash(s);
        }

        internal static string Hash(string path)
        {
            using (var s = File.OpenRead(path))
                return Hash(s);
        }

        static string Hash(Stream stream)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
        }

        internal static void Plain(string path)
        {
            string at = Path.GetFullPath(path);
            while (!String.IsNullOrEmpty(at))
            {
                if ((File.Exists(at) || Directory.Exists(at)) && (File.GetAttributes(at) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Redirected update path refused");
                var parent = Directory.GetParent(at);
                at = parent == null ? null : parent.FullName;
            }
        }

        internal static string Root(string path)
        {
            string root = Path.GetFullPath(path);
            if (Directory.GetParent(root) == null)
                throw new IOException("A drive root is not an application folder");
            Plain(root);
            return root.TrimEnd(Path.DirectorySeparatorChar);
        }

        internal static string Inside(string root, string relative)
        {
            if (String.IsNullOrWhiteSpace(relative) || relative.Contains("\\") || relative.Contains(":") || relative.StartsWith("/") || relative.Split('/').Any(s => s == ".." || s == "." || s.Length == 0 || s.EndsWith(" ") || s.EndsWith(".") || s.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || Regex.IsMatch(s, @"^(con|prn|aux|nul|com[1-9]|lpt[1-9])(?:\.|$)", RegexOptions.IgnoreCase)))
                throw new IOException("Unsafe update path");
            string full = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Update path escaped its workspace");
            Plain(full);
            return full;
        }

        internal static long HighestSequence(string data)
        {
            string path = Inside(Root(data), "preferences.json");
            if (!File.Exists(path))
                return 0;
            var file = new FileInfo(path);
            if (file.Length > 1024 * 1024)
                throw new IOException("Settings file is too large");
            var preferences = Json.Deserialize<System.Collections.Generic.Dictionary<string, object>>(File.ReadAllText(path));
            if (preferences == null)
                throw new IOException("Settings unreadable");
            object value;
            return preferences.TryGetValue("HighestAppSequence", out value) ? Convert.ToInt64(value) : 0;
        }

        internal static string Quote(string value)
        {
            if (value.IndexOfAny(new[] { '\0', '\r', '\n' }) >= 0)
                throw new IOException("Invalid process argument");
            return "\"" + Regex.Replace(value, @"(\\*)""", m => new string('\\', m.Groups[1].Length * 2) + "\\\"").TrimEnd('\\') + new string('\\', value.Reverse().TakeWhile(c => c == '\\').Count() * 2) + "\"";
        }
    }
}
