using System;
using System.Collections.Generic;
using System.IO;
using MC2Pack;

namespace Astera
{
    static class FolderPickerAudit
    {
        internal static int Run(string root)
        {
            try
            {
                if (Directory.Exists(root))
                    throw new Exception("Use a new folder picker audit directory");
                Directory.CreateDirectory(root);
                string folder = Path.Combine(root, "Folder with spaces");
                Directory.CreateDirectory(folder);
                var checks = new List<string>();
                if (!String.Equals(ExplorerFolderDialog.Probe(folder), Path.GetFullPath(folder), StringComparison.OrdinalIgnoreCase))
                    throw new Exception("Explorer did not start in the supplied folder");
                checks.Add("Real Windows Common Item Dialog accepts folder-selection flags and starts at the supplied path with spaces");
                if (!String.Equals(ExplorerFolderDialog.InitialFolder("\"" + folder + "\""), Path.GetFullPath(folder), StringComparison.OrdinalIgnoreCase))
                    throw new Exception("Quoted pasted path failed");
                checks.Add("Quoted pasted paths are accepted");
                if (!String.Equals(ExplorerFolderDialog.Probe(Path.Combine(folder, "not-created")), Path.GetFullPath(folder), StringComparison.OrdinalIgnoreCase))
                    throw new Exception("Missing folder did not start at its nearest existing parent");
                checks.Add("Unavailable locations fall back to an existing parent instead of the program folder");
                File.WriteAllText(Path.Combine(root, "result.json"), Engine.Json.Serialize(new
                {
                    Passed = true,
                    Checks = checks,
                    LiveProfileChanged = false
                }));
                return 0;
            }
            catch (Exception error)
            {
                Directory.CreateDirectory(root);
                File.WriteAllText(Path.Combine(root, "failure.txt"), error.ToString());
                return 1;
            }
        }
    }
}
