using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using MC2Pack;

namespace Astera {
public sealed class ManagedInstallRecord {
    public string ProjectId=ManagedGames.Id,Root="";
    public List<string> SaveLocations=new List<string>();
    public bool Installed;
}
sealed class ManagedUninstallResult {
    internal int Removed,Kept;
    internal readonly List<string> BusyFiles=new List<string>();
}
static class ManagedLibrary {
    internal static string DefaultRoot(string dataRoot) { return Path.Combine(dataRoot,"Games"); }
    internal static string Root(Preferences settings,string dataRoot) {
        return SelfUpdateFiles.Root(String.IsNullOrWhiteSpace(settings.ManagedLibraryRoot)
            ? DefaultRoot(dataRoot) : settings.ManagedLibraryRoot);
    }
    internal static bool Installed(string root) {
        if(String.IsNullOrWhiteSpace(root))return false;
        try {var state=ManagedGames.State(root);return state!=null&&state.Installed
            &&File.Exists(ManagedGames.DiskPath(Path.Combine(root,ManagedGames.Launcher)));}
        catch(IOException){return false;}catch(UnauthorizedAccessException){return false;}catch(ArgumentException){return false;}catch(NotSupportedException){return false;}
    }
    internal static List<string> Saves(string gameRoot,string roaming,string local) {
        return new List<string>{Path.Combine(roaming,"com.starsallies.nebula.stability","Local Store"),
            Path.Combine(local,"NebulaStability","Recovery"),Path.Combine(gameRoot,"Recovery")};
    }
    internal static void Remember(Preferences settings,string gameRoot,bool installed,string roaming,string local) {
        if(settings.ManagedInstalls==null)settings.ManagedInstalls=new List<ManagedInstallRecord>();
        string full=Path.GetFullPath(gameRoot);
        var record=settings.ManagedInstalls.FirstOrDefault(x=>x!=null&&x.ProjectId==ManagedGames.Id
            &&String.Equals(x.Root,full,StringComparison.OrdinalIgnoreCase));
        if(record==null){record=new ManagedInstallRecord{Root=full};settings.ManagedInstalls.Add(record);}
        record.Installed=installed;record.SaveLocations=Saves(full,roaming,local);
    }
    internal static IEnumerable<string> Candidates(Preferences settings,string dataRoot,string home,string documents) {
        var candidates=new List<string>();
        foreach(var game in (settings.CustomGames??new List<CustomGame>()).Where(g=>g!=null&&(g.ManagedProjectId==ManagedGames.Id
                ||String.Equals(g.Name,"Gacha Nebula +",StringComparison.OrdinalIgnoreCase))))
            if(!String.IsNullOrWhiteSpace(game.Executable))try{candidates.Add(Path.GetDirectoryName(game.Executable));}catch(ArgumentException){}catch(NotSupportedException){}
        if(settings.ManagedInstalls!=null)candidates.AddRange(settings.ManagedInstalls
            .Where(x=>x!=null&&x.ProjectId==ManagedGames.Id).Select(x=>x.Root));
        candidates.Add(Path.Combine(Root(settings,dataRoot),"Gacha Nebula +"));
        candidates.Add(Path.Combine(home,"Games","Gacha Nebula +"));
        candidates.Add(Path.Combine(Path.GetPathRoot(home),"Games","Gacha Nebula +"));
        candidates.Add(Path.Combine(documents,"Gacha Nebula +"));
        return candidates.Where(x=>!String.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).Take(64);
    }
    internal static bool VerifiedExisting(string root,ManagedGamePackage package) {
        try {
            ManagedGames.NoLinks(ManagedGames.DiskPath(root));var state=ManagedGames.State(root);
            if(state==null||!state.Installed||!String.Equals(state.ArchiveHash,package.Hash,StringComparison.OrdinalIgnoreCase))return false;
            string launcher=ManagedGames.DiskPath(Path.Combine(root,ManagedGames.Launcher));
            string swf=ManagedGames.DiskPath(Path.Combine(root,"game","GachaNebula.swf"));
            return File.Exists(launcher)&&File.Exists(swf)&&Engine.Hash(launcher).Equals(package.LauncherHash,StringComparison.OrdinalIgnoreCase)
                &&Engine.Hash(swf).Equals(package.GameHash,StringComparison.OrdinalIgnoreCase);
        }catch(IOException){return false;}catch(UnauthorizedAccessException){return false;}catch(ArgumentException){return false;}catch(NotSupportedException){return false;}
    }
    internal static string FindExisting(IEnumerable<string> candidates,ManagedGamePackage package) {
        ManagedGames.Validate(package);
        foreach(string root in candidates)if(VerifiedExisting(root,package))return root;
        return null;
    }
    static bool UserData(string relative) {
        var parts=relative.Split('/');
        return relative.EndsWith(".sol",StringComparison.OrdinalIgnoreCase)
            ||parts.Any(p=>p.Equals("Recovery",StringComparison.OrdinalIgnoreCase)
                ||p.Equals("saves",StringComparison.OrdinalIgnoreCase)||p.Equals("exports",StringComparison.OrdinalIgnoreCase)
                ||p.Equals("screenshots",StringComparison.OrdinalIgnoreCase));
    }
    internal static void WriteState(string root,ManagedGameState state) {
        string file=Path.Combine(root,ManagedGames.Marker);SelfUpdateFiles.Plain(file);string next=file+"."+Guid.NewGuid().ToString("N")+".next";
        try{File.WriteAllText(next,Engine.Json.Serialize(state));if(File.Exists(file))File.Replace(next,file,null);else File.Move(next,file);}
        finally{try{if(File.Exists(next))File.Delete(next);}catch(IOException error){AsteraDiagnostics.Write("Deferred installation-record cleanup",error);}catch(UnauthorizedAccessException error){AsteraDiagnostics.Write("Deferred installation-record cleanup",error);}}
    }
    // Uninstall only byte-identical manifest-owned files. User data and changed
    // package files are kept in place, including after interruption or a file lock.
    internal static ManagedUninstallResult Uninstall(string gameRoot,CancellationToken stop,
            Action<string,long,long> progress,Func<bool> running) {
        string root=SelfUpdateFiles.Root(ManagedGames.DiskPath(gameRoot));ManagedGames.NoLinks(root);
        using(var gate=new FileStream(root+".astera-install.lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None,4096,FileOptions.DeleteOnClose)){
            if(running!=null&&running())throw new IOException("Close the game and its tray helper before uninstalling");
            var state=ManagedGames.State(root);if(state==null)throw new IOException("This folder has no Astera installation record. Nothing was removed.");
            var files=ManagedGames.Walk(root).ToArray();var owned=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            var result=new ManagedUninstallResult();
            foreach(string file in files){stop.ThrowIfCancellationRequested();string relative=ManagedGames.Relative(file.Substring(root.Length).TrimStart('\\','/'));
                string expected;
                if(relative.Equals(ManagedGames.Marker,StringComparison.OrdinalIgnoreCase))continue;
                if(!UserData(relative)&&state.Files.TryGetValue(relative,out expected)
                        &&Engine.Hash(file).Equals(expected,StringComparison.OrdinalIgnoreCase))owned.Add(relative,expected);
                else result.Kept++;
            }
            stop.ThrowIfCancellationRequested();if(running!=null&&running())throw new IOException("The game started during preparation. Nothing was removed.");
            // Durable before deleting: interrupted removal remains repairable with
            // Install, never an apparently playable partial installation.
            state.Installed=false;state.Files=owned;WriteState(root,state);
            int done=0;string[] parents=owned.Keys.ToArray();
            try{foreach(var pair in owned.ToArray()){stop.ThrowIfCancellationRequested();
                if(running!=null&&running())throw new IOException("The game started. Removal stopped; your saves are preserved. Close it and retry.");
                string file=SelfUpdateFiles.Inside(root,pair.Key);
                try{if(!File.Exists(file))state.Files.Remove(pair.Key);
                    else if(Engine.Hash(file).Equals(pair.Value,StringComparison.OrdinalIgnoreCase)){File.Delete(file);result.Removed++;state.Files.Remove(pair.Key);}
                    else{state.Files.Remove(pair.Key);result.Kept++;}}
                catch(IOException){result.BusyFiles.Add(pair.Key);}catch(UnauthorizedAccessException){result.BusyFiles.Add(pair.Key);}
                if(progress!=null)progress("Removing game files; keeping saves",++done,parents.Length);
            }}finally{try{WriteState(root,state);}catch(IOException error){AsteraDiagnostics.Write("Partial uninstall record retained",error);}catch(UnauthorizedAccessException error){AsteraDiagnostics.Write("Partial uninstall record retained",error);}}
            RemoveEmptyOwnedParents(root,parents);
            return result;
        }
    }
    static void RemoveEmptyOwnedParents(string root,IEnumerable<string> files) {
        var directories=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(string file in files){string parent=Path.GetDirectoryName(SelfUpdateFiles.Inside(root,file));
            while(parent!=null&&parent.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)){
                directories.Add(parent);parent=Path.GetDirectoryName(parent);
            }
        }
        foreach(string folder in directories.OrderByDescending(p=>p.Length))try{
            ManagedGames.NoLinks(folder);if(Directory.Exists(folder)&&!Directory.EnumerateFileSystemEntries(folder).Any())Directory.Delete(folder);
        }catch(IOException){}catch(UnauthorizedAccessException){}
    }
}
}