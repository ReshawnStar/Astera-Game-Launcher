using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace Astera
{
    // CurseForge and Prism can both register curseforge://. Keep the user's
    // defaults intact and dispatch to the installed Overwolf host when present.
    static class CurseForgeLaunch
    {
        internal const string AppId = "cchhcaiapeikjbdbpfplgmpobbcdkdaphclbmkbj";
        internal static string UriFor(string profile)
        {
            var selected = Profiles.Read(profile);
            Guid id;
            if (selected.IsPrism || !Guid.TryParse(selected.Guid, out id))
                throw new IOException("This profile has no valid CurseForge instance ID");
            return "curseforge://launch-game?instanceId=" + id.ToString() + "&gameId=432";
        }

        internal static ProcessStartInfo Native(string profile, string executable)
        {
            string uri = UriFor(profile);
            if (!File.Exists(executable) || !Path.GetFileName(executable).Equals("OverwolfLauncher.exe", StringComparison.OrdinalIgnoreCase))
                throw new IOException("CurseForge's Overwolf launcher could not be found");
            PrismProfiles.SafePath(executable);
            // Overwolf already escapes this parameter. Quotes are retained literally,
            // and escaping it here would double-encode it. The generated URI contains
            // only the validated GUID and fixed constants, and is never sent to a shell.
            return new ProcessStartInfo(executable)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Minimized,
                WorkingDirectory = Path.GetDirectoryName(executable),
                Arguments = "-launchapp " + AppId + " --origin=urlscheme --parameters=" + uri
            };
        }

        internal static string FindOverwolf()
        {
            string extensions = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Overwolf", "Extensions", AppId);
            if (!Directory.Exists(extensions))
                return "";
            foreach (string root in new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            }

            )
            {
                if (String.IsNullOrWhiteSpace(root))
                    continue;
                string path = Path.Combine(root, "Overwolf", "OverwolfLauncher.exe");
                if (File.Exists(path))
                    return path;
            }

            return "";
        }

        internal static ProcessStartInfo Resolve(string profile)
        {
            string uri = UriFor(profile), host = FindOverwolf();
            if (host.Length > 0)
                return Native(profile, host);
            using (var key = Registry.ClassesRoot.OpenSubKey(@"curseforge\shell\open\command"))
            {
                string command = key == null ? "" : Convert.ToString(key.GetValue(""));
                if (command.IndexOf("prismlauncher.exe", StringComparison.OrdinalIgnoreCase) >= 0)
                    throw new IOException("Windows currently opens CurseForge links in Prism. Open this profile with CurseForge's Play button, or choose a Prism instance in Astera.");
            }

            return new ProcessStartInfo(uri)
            {
                UseShellExecute = true
            };
        }
    }
}
