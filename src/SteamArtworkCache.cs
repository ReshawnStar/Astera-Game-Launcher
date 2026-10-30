using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Astera
{
    static class SteamArtworkCache
    {
        internal const long Budget = 64L * 1024 * 1024;
        // Only files written by this artwork cache are eligible; never recurse.
        internal static void Trim(string root, string keep, long reserve = 0, long budget = Budget, string pattern = @"^[0-9]{1,12}-(background|portrait)\.png$")
        {
            if (!Directory.Exists(root))
                return;
            var directory = new DirectoryInfo(root);
            if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Artwork cache is a reparse point");
            var files = directory.EnumerateFiles("*.png", SearchOption.TopDirectoryOnly).Where(f => (f.Attributes & FileAttributes.ReparsePoint) == 0 && Regex.IsMatch(f.Name, pattern)).OrderBy(f => f.LastWriteTimeUtc).ToArray();
            long size = files.Sum(f => f.Length) + reserve;
            foreach (var file in files)
            {
                if (size <= budget)
                    break;
                if (String.Equals(file.FullName, keep, StringComparison.OrdinalIgnoreCase))
                    continue;
                try
                {
                    long length = file.Length;
                    file.Delete();
                    size -= length;
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
    }
}
