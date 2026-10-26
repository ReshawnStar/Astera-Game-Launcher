using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace MC2Pack {
public sealed class PackFile { public string Path,Hash,Kind,Id,Version,Policy,Family; public long Size; }
public sealed class RetiredFile { public string Path,Hash; }
public sealed class Release { public int Schema; public long Sequence; public string PackId,Name,Minecraft,Loader; public List<PackFile> Files; public List<RetiredFile> Retired; public List<string> Packs; }
public sealed class Ledger { public string PackId,Release; public long Sequence; public Dictionary<string,string> Files=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase); }
public sealed class Edit { public string Path,Expected,Desired,Reason; public bool Existed; public byte[] Generated; }
public sealed class Journal { public string Profile,Release,Status; public List<Edit> Edits; }
public sealed class Plan { public string Profile,Bundle,BundleHash; public Release Release; public List<Edit> Edits=new List<Edit>(); public List<string> Warnings=new List<string>(); public Dictionary<string,string> Preconditions=new Dictionary<string,string>(); public Ledger State; public bool Unregistered; }

public static class Engine {
    public static readonly JavaScriptSerializer Json=new JavaScriptSerializer { MaxJsonLength=16*1024*1024 };
    const string PackId="astera-minecraft-squared";
    const string StateFile="Astera-minecraft-state.json";
    static readonly string[] Roots={"mods/","resourcepacks/","shaderpacks/","config/","defaultconfigs/"};
    static readonly UTF8Encoding Utf8=new UTF8Encoding(false);
    public static string Hash(Stream s){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(s)).Replace("-","");}
    public static string Hash(string p){using(var f=File.OpenRead(p))return Hash(f);}
    public static string Hash(byte[] b){using(var f=new MemoryStream(b))return Hash(f);}
    public static void Plain(string p){if((File.Exists(p)||Directory.Exists(p))&&(File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0)throw new Exception("Redirected path refused: "+p);}
    public static string Root(string path){var full=System.IO.Path.GetFullPath(path);if(Directory.GetParent(full)==null)throw new Exception("Choose a profile, not a drive root.");Plain(full);return full.TrimEnd(System.IO.Path.DirectorySeparatorChar);}
    public static string Inside(string root,string relative){
        if(String.IsNullOrWhiteSpace(relative)||relative.Contains("\\")||relative.Contains(":")||relative.StartsWith("/")||relative.Split('/').Any(s=>s==".."||s=="."||s.Length==0||s.EndsWith(" ")||s.EndsWith(".")))throw new Exception("Unsafe release path: "+relative);
        if(relative.Split('/').Any(s=>s.IndexOfAny(System.IO.Path.GetInvalidFileNameChars())>=0||Regex.IsMatch(s,@"^(con|prn|aux|nul|com[1-9]|lpt[1-9])(?:\.|$)",RegexOptions.IgnoreCase)))throw new Exception("Unsafe Windows filename.");
        var full=System.IO.Path.GetFullPath(System.IO.Path.Combine(root,relative.Replace('/',System.IO.Path.DirectorySeparatorChar)));
        if(!full.StartsWith(root+System.IO.Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new Exception("Path escaped profile.");
        var at=root;foreach(var part in relative.Split('/')){at=System.IO.Path.Combine(at,part);Plain(at);}return full;
    }
    static void Allowed(string p,bool metadata=false){
        if(metadata&&(p==StateFile||p=="options.txt"))return;
        if(!Roots.Any(r=>p.StartsWith(r,StringComparison.Ordinal))||p.Contains("\\")||p.Contains(":"))throw new Exception("Protected/unsupported target: "+p);
        string ext=System.IO.Path.GetExtension(p).ToLowerInvariant();
        if(p.StartsWith("mods/")&&(p.Split('/').Length!=2||ext!=".jar"))throw new Exception("Only root mod JARs are managed.");
        if((p.StartsWith("resourcepacks/")||p.StartsWith("shaderpacks/"))&&(p.Split('/').Length!=2||ext!=".zip"))throw new Exception("Only archive assets are managed.");
        if((p.StartsWith("config/")||p.StartsWith("defaultconfigs/"))&&!new[]{".json",".json5",".toml",".properties",".cfg"}.Contains(ext))throw new Exception("Unsupported config file type.");
    }
    static string EntryText(ZipArchiveEntry e){if(e==null||e.Length>16*1024*1024)throw new Exception("Missing/oversized release metadata.");using(var r=new StreamReader(e.Open()))return r.ReadToEnd();}
    public static Release Read(string bundle){using(var z=ZipFile.OpenRead(bundle))return Validate(Json.Deserialize<Release>(EntryText(z.GetEntry("mc2-release.json"))));}
    public static Release Validate(Release r){
        if(r==null||r.Schema!=2||r.Sequence<=0||r.PackId!=PackId||r.Files==null||r.Files.Count==0||r.Files.Count>5000||r.Packs==null||r.Retired==null)throw new Exception("Not a supported full Minecraft Squared release.");
        var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var ids=new HashSet<string>();long total=0;
        foreach(var f in r.Files){Allowed(f.Path);if(!names.Add(f.Path)||!Regex.IsMatch(f.Hash??"","^[A-Fa-f0-9]{64}$")||f.Size<0||f.Size>1024L*1024*1024)throw new Exception("Duplicate/invalid release file.");f.Hash=f.Hash.ToUpperInvariant();total+=f.Size;
            if(f.Kind=="mod"){if(!f.Path.StartsWith("mods/")||f.Policy!="replace"||String.IsNullOrWhiteSpace(f.Id)||!ids.Add(f.Id)||String.IsNullOrWhiteSpace(f.Version))throw new Exception("Invalid/duplicate mod identity.");}
            else if(f.Kind=="config"){if(!(f.Path.StartsWith("config/")||f.Path.StartsWith("defaultconfigs/"))||f.Policy!="seed")throw new Exception("Configs must preserve local changes.");}
            else if(f.Kind=="asset"){if(!(f.Path.StartsWith("resourcepacks/")||f.Path.StartsWith("shaderpacks/"))||!(f.Policy=="replace"||f.Policy=="seed"))throw new Exception("Invalid asset policy.");}
            else throw new Exception("Unknown file kind.");
        }
        if(total>4L*1024*1024*1024)throw new Exception("Release exceeds installer size limit.");
        foreach(var f in r.Retired){Allowed(f.Path);if(!f.Path.StartsWith("mods/")&&!f.Path.StartsWith("resourcepacks/"))throw new Exception("Retirement cannot remove configs/personal data.");if(!Regex.IsMatch(f.Hash??"","^[A-Fa-f0-9]{64}$"))throw new Exception("Invalid retired hash.");f.Hash=f.Hash.ToUpperInvariant();}
        foreach(var p in r.Packs)if(p!="stars_general_fixes:stars_compat"&&!(p=="stars_general_fixes:eating_compat"&&ids.Contains("stars_general_fixes")&&ids.Contains("eatinganimationid"))&&!(p=="eatinganimationid:supporteatinganimation"&&ids.Contains("eatinganimationid"))&&!(p=="punchy:punchy"&&ids.Contains("punchy"))&&!(p.StartsWith("file/")&&names.Contains("resourcepacks/"+p.Substring(5))))throw new Exception("Recommended pack not in release.");
        return r;
    }
    public static Dictionary<string,object> Mod(string file){try{using(var z=ZipFile.OpenRead(file)){var e=z.GetEntry("fabric.mod.json");return e==null?null:Json.Deserialize<Dictionary<string,object>>(EntryText(e));}}catch(InvalidDataException){return null;}}
    public static void Runtime(string profile,Release r,bool allow,List<string> warnings){
        string meta=Inside(profile,"minecraftinstance.json");
        if(!File.Exists(meta)){if(!allow)throw new Exception("No CurseForge profile metadata. Choose a CurseForge-created profile, or explicitly enable content-folder mode after configuring Minecraft "+r.Minecraft+" / Fabric "+r.Loader+" yourself.");warnings.Add("Content-folder mode: launcher version was NOT verified; no CurseForge profile will be registered.");return;}
        var obj=Json.Deserialize<Dictionary<string,object>>(File.ReadAllText(meta));object raw;
        if(!obj.TryGetValue("baseModLoader",out raw)||!(raw is Dictionary<string,object>))throw new Exception("CurseForge loader metadata is incomplete.");
        var loader=(Dictionary<string,object>)raw;
        if(!loader.ContainsKey("minecraftVersion")||(string)loader["minecraftVersion"]!=r.Minecraft||!loader.ContainsKey("name")||!((string)loader["name"]).StartsWith("fabric-"))throw new Exception("Use CurseForge to select Minecraft "+r.Minecraft+" and Fabric "+r.Loader+" first. Launcher metadata is never rewritten.");
        Version actual,minimum;if(!Version.TryParse(Convert.ToString(loader["forgeVersion"]),out actual)||!Version.TryParse(r.Loader,out minimum)||actual<minimum)throw new Exception("Fabric loader is too old; update it in CurseForge to "+r.Loader+" or newer first.");
    }
    static void VerifyPayload(string bundle,Release r,Action<string> log){using(var zip=ZipFile.OpenRead(bundle)){
        var duplicate=zip.Entries.GroupBy(e=>e.FullName,StringComparer.OrdinalIgnoreCase).Any(g=>g.Count()>1);if(duplicate)throw new Exception("Duplicate ZIP entries refused.");
        foreach(var f in r.Files){var e=zip.GetEntry(f.Path);if(e==null||e.Length!=f.Size)throw new Exception("Missing/size-mismatched payload: "+f.Path);using(var input=e.Open())if(!Hash(input).Equals(f.Hash,StringComparison.OrdinalIgnoreCase))throw new Exception("Payload SHA mismatch: "+f.Path);
            if(f.Kind=="mod")using(var input=e.Open())using(var memory=new MemoryStream()){input.CopyTo(memory);memory.Position=0;using(var inner=new ZipArchive(memory,ZipArchiveMode.Read)){var m=Json.Deserialize<Dictionary<string,object>>(EntryText(inner.GetEntry("fabric.mod.json")));if((string)m["id"]!=f.Id||(string)m["version"]!=f.Version)throw new Exception("JAR identity/version mismatch: "+f.Path);}}
        }log("Verified "+r.Files.Count+" release files.");
    }}
    static Ledger State(string root){string p=Inside(root,StateFile);if(!File.Exists(p))return new Ledger{PackId=PackId};var s=Json.Deserialize<Ledger>(File.ReadAllText(p));if(s.PackId!=PackId||s.Files==null)throw new Exception("Unrecognized local pack ledger.");foreach(var f in s.Files){Allowed(f.Key);if(!Regex.IsMatch(f.Value,"^[A-Fa-f0-9]{64}$"))throw new Exception("Invalid ledger hash.");}return s;}
    static Edit Change(string root,string path,string hash,string reason,byte[] generated=null){Allowed(path,true);var full=Inside(root,path);return new Edit{Path=path,Expected=File.Exists(full)?Hash(full):null,Desired=hash,Existed=File.Exists(full),Reason=reason,Generated=generated};}
    static void Add(Plan p,Edit edit){if(p.Edits.Any(e=>e.Path.Equals(edit.Path,StringComparison.OrdinalIgnoreCase)))throw new Exception("Conflicting edits: "+edit.Path);p.Edits.Add(edit);}
    public static Plan Scan(string profile,string bundle,bool configure,bool cleanExtras,bool unregistered,Action<string> log){
        var root=Root(profile);if(!Directory.Exists(root))throw new Exception("Select an existing CurseForge-created profile or content folder.");
        var r=Read(bundle);var p=new Plan{Profile=root,Bundle=System.IO.Path.GetFullPath(bundle),BundleHash=Hash(bundle),Release=r,Unregistered=unregistered};Runtime(root,r,unregistered,p.Warnings);VerifyPayload(bundle,r,log);
        var old=State(root);if(old.Sequence>r.Sequence)throw new Exception("Release downgrade refused. Use Restore for an intentional rollback.");p.State=new Ledger{PackId=PackId,Release=r.Name,Sequence=r.Sequence};
        var desiredMods=r.Files.Where(f=>f.Kind=="mod").ToDictionary(f=>f.Id);
        string mods=Inside(root,"mods");var existing=new Dictionary<string,List<string>>();var unknown=new List<string>();
        if(Directory.Exists(mods))foreach(var file in Directory.GetFiles(mods,"*.jar")){Plain(file);var m=Mod(file);string id=m!=null&&m.ContainsKey("id")?(string)m["id"]:null;
            if(id==null||!desiredMods.ContainsKey(id)){unknown.Add("mods/"+System.IO.Path.GetFileName(file));continue;}if(!existing.ContainsKey(id))existing[id]=new List<string>();existing[id].Add("mods/"+System.IO.Path.GetFileName(file));}
        var desiredPaths=new HashSet<string>(r.Files.Select(f=>f.Path),StringComparer.OrdinalIgnoreCase);
        foreach(var f in r.Files){string full=Inside(root,f.Path),current=File.Exists(full)?Hash(full):null;string previous;old.Files.TryGetValue(f.Path,out previous);
            if(f.Kind=="mod"&&current!=null){var identity=Mod(full);if(identity==null||!identity.ContainsKey("id")||(string)identity["id"]!=f.Id)throw new Exception("Unrelated JAR occupies release filename; preserved: "+f.Path);}
            if(f.Kind=="mod"&&existing.ContainsKey(f.Id)){
                if(f.Id=="stars_allies"||f.Id=="stars_general_fixes")foreach(var path in existing[f.Id]){var info=Mod(Inside(root,path));Version have,want;if(Version.TryParse(((string)info["version"]).Split('+')[0],out have)&&Version.TryParse(f.Version.Split('+')[0],out want)&&have>want)throw new Exception("Newer Stars component present; use a newer full release instead of downgrading.");}
                foreach(var path in existing[f.Id].Where(x=>!x.Equals(f.Path,StringComparison.OrdinalIgnoreCase)))Add(p,Change(root,path,null,"Replace older/duplicate release mod "+f.Id));
            }
            if(f.Policy=="seed"&&current!=null&&!current.Equals(f.Hash,StringComparison.OrdinalIgnoreCase)&&!current.Equals(previous,StringComparison.OrdinalIgnoreCase)){p.Warnings.Add("Kept local settings/asset: "+f.Path);if(previous!=null)p.State.Files[f.Path]=previous;continue;}
            if(current==null||!current.Equals(f.Hash,StringComparison.OrdinalIgnoreCase))Add(p,Change(root,f.Path,f.Hash,current==null?"Install missing file":"Update release file"));
            p.State.Files[f.Path]=f.Hash;
        }
        foreach(var retired in r.Retired){if(desiredPaths.Contains(retired.Path)||p.Edits.Any(e=>e.Path.Equals(retired.Path,StringComparison.OrdinalIgnoreCase)))continue;var full=Inside(root,retired.Path);if(File.Exists(full)&&Hash(full).Equals(retired.Hash,StringComparison.OrdinalIgnoreCase))Add(p,Change(root,retired.Path,null,"Retire checksum-verified old release file"));}
        foreach(var prior in old.Files.Where(f=>!desiredPaths.Contains(f.Key)&&(f.Key.StartsWith("mods/")||f.Key.StartsWith("resourcepacks/")))){if(p.Edits.Any(e=>e.Path.Equals(prior.Key,StringComparison.OrdinalIgnoreCase)))continue;var full=Inside(root,prior.Key);if(File.Exists(full)&&Hash(full).Equals(prior.Value,StringComparison.OrdinalIgnoreCase))Add(p,Change(root,prior.Key,null,"Retire unchanged previously managed file"));}
        foreach(var path in unknown){if(p.Edits.Any(e=>e.Path.Equals(path,StringComparison.OrdinalIgnoreCase)))continue;if(cleanExtras)Add(p,Change(root,path,null,"Move extra mod to rollback backup (explicit clean-set choice)"));else p.Warnings.Add("Extra mod retained; exact release set not guaranteed: "+path);}
        if(configure){string opt=Inside(root,"options.txt"),before=File.Exists(opt)?File.ReadAllText(opt):"";var lines=before.Split('\n').ToList();int index=lines.FindIndex(x=>x.StartsWith("resourcePacks:"));
            var packs=index<0?new List<string>():Json.Deserialize<List<string>>(lines[index].Substring(14).Trim());
            var known=r.Packs.Where(x=>x!="stars_general_fixes:stars_compat").ToList();
            packs=packs.Where(x=>x!="stars_general_fixes:stars_compat"&&!known.Contains(x)&&!(x.StartsWith("file/")&&p.Edits.Any(e=>e.Desired==null&&e.Path.Equals("resourcepacks/"+x.Substring(5),StringComparison.OrdinalIgnoreCase)))).Distinct().ToList();if(!packs.Contains("vanilla"))packs.Insert(0,"vanilla");packs.AddRange(known);packs.Add("stars_general_fixes:stars_compat");
            string selected="resourcePacks:"+Json.Serialize(packs);if(index<0)lines.Add(selected);else lines[index]=selected;byte[] data=Utf8.GetBytes(String.Join("\n",lines));if(!File.Exists(opt)||!Hash(data).Equals(Hash(opt),StringComparison.OrdinalIgnoreCase))Add(p,Change(root,"options.txt",Hash(data),"Set supplied resource-pack selection only",data));
        }
        var ledger=Utf8.GetBytes(Json.Serialize(p.State));string state=Inside(root,StateFile);if(!File.Exists(state)||Hash(state)!=Hash(ledger))Add(p,Change(root,StateFile,Hash(ledger),"Record release ownership (not personal data)",ledger));
        return p;
    }
    public static void Closed(){if(Process.GetProcessesByName("java").Length>0||Process.GetProcessesByName("javaw").Length>0)throw new Exception("Close Minecraft and Java launchers/builds before applying. Preview is still safe.");}
    static void Unchanged(Plan p){if(Hash(p.Bundle)!=p.BundleHash)throw new Exception("Update package changed; scan again.");foreach(var guard in p.Preconditions){var full=Inside(p.Profile,guard.Key);if(!File.Exists(full)||Hash(full)!=guard.Value)throw new Exception("CurseForge dependency changed after preview; check again: "+guard.Key);}foreach(var e in p.Edits){var full=Inside(p.Profile,e.Path);if(File.Exists(full)!=e.Existed||(e.Existed&&Hash(full)!=e.Expected))throw new Exception("Target changed after preview; scan again: "+e.Path);}}
    static void WriteJournal(string folder,Journal j){string path=Inside(folder,"transaction.json"),temp=Inside(folder,"transaction-next.json");File.WriteAllText(temp,Json.Serialize(j),Utf8);if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}
    static FileStream ProfileLock(string profile){var root=Root(profile);return new FileStream(Inside(root,".Astera-update.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}
    public static string Apply(Plan p,Action<string> log,bool test=false,int failAfter=-1){using(var held=ProfileLock(p.Profile))return ApplyLocked(p,log,test,failAfter);}
    static string ApplyLocked(Plan p,Action<string> log,bool test,int failAfter){
        if(!test)Closed();Unchanged(p);if(p.Edits.Count==0)return "Already matches the release. No changes.";
        var backupBase=Inside(p.Profile,"Astera-backups");if(Directory.Exists(backupBase))foreach(var d in Directory.GetDirectories(backupBase)){Plain(d);string f=System.IO.Path.Combine(d,"transaction.json");if(File.Exists(f)){var j=Json.Deserialize<Journal>(File.ReadAllText(f));if(j.Status=="applying"||j.Status=="prepared")throw new Exception("Unfinished update found. Restore it first: "+d);}}
        long need=p.Edits.Where(e=>e.Existed).Sum(e=>new FileInfo(Inside(p.Profile,e.Path)).Length)+p.Release.Files.Where(f=>p.Edits.Any(e=>e.Path==f.Path&&e.Desired!=null)).Sum(f=>f.Size)+64L*1024*1024;
        if(new DriveInfo(System.IO.Path.GetPathRoot(p.Profile)).AvailableFreeSpace<need)throw new Exception("Not enough space for staging plus rollback; need about "+(need/1048576)+" MiB.");
        var backup=Inside(p.Profile,"Astera-backups/"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,8));Directory.CreateDirectory(backup);
        var journal=new Journal{Profile=p.Profile,Release=p.Release.Name,Status="prepared",Edits=p.Edits};
        // Stage and snapshot every path before touching any installed content.
        using(var z=ZipFile.OpenRead(p.Bundle))foreach(var e in p.Edits){
            if(e.Existed){string target=Inside(backup,"old/"+e.Path);Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target));File.Copy(Inside(p.Profile,e.Path),target);if(Hash(target)!=e.Expected)throw new Exception("Rollback verification failed.");}
            if(e.Desired!=null){string stage=Inside(backup,"new/"+e.Path);Directory.CreateDirectory(System.IO.Path.GetDirectoryName(stage));if(e.Generated!=null)File.WriteAllBytes(stage,e.Generated);else using(var input=z.GetEntry(e.Path).Open())using(var output=File.Create(stage))input.CopyTo(output);if(Hash(stage)!=e.Desired)throw new Exception("Staged hash mismatch.");}
        }
        // Do not serialize full generated bytes into journals.
        foreach(var e in journal.Edits)e.Generated=null;if(!test)Closed();Unchanged(p);WriteJournal(backup,journal);journal.Status="applying";WriteJournal(backup,journal);
        try{int writes=0;foreach(var e in p.Edits){string full=Inside(p.Profile,e.Path);if(e.Desired!=null){Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full));string pending=Inside(p.Profile,e.Path+".mc2-"+Guid.NewGuid().ToString("N"));try{File.Copy(Inside(backup,"new/"+e.Path),pending);if(Hash(pending)!=e.Desired)throw new Exception("Installed hash mismatch.");if(File.Exists(full))File.Replace(pending,full,null);else File.Move(pending,full);}finally{if(File.Exists(pending))File.Delete(pending);}}else if(File.Exists(full))File.Delete(full);log(e.Reason+": "+e.Path);if(++writes==failAfter)throw new Exception("Synthetic transaction interruption");}
            journal.Status="complete";WriteJournal(backup,journal);
            foreach(var e in p.Edits.Where(x=>x.Desired!=null))try{File.Delete(Inside(backup,"new/"+e.Path));}catch(IOException){log("Temporary staging copy retained in rollback folder: "+e.Path);}
            return "Installed "+p.Release.Name+". Rollback: "+backup;
        }catch(Exception failure){try{RestoreLocked(p.Profile,backup,log,true,true);}catch(Exception restore){throw new Exception("Update failed and rollback needs attention. Backups retained at "+backup,new AggregateException(failure,restore));}throw;}
    }
    public static void Restore(string profile,string folder,Action<string> log,bool test=false,bool interrupted=false){using(var held=ProfileLock(profile))RestoreLocked(profile,folder,log,test,interrupted);}
    static void RestoreLocked(string profile,string folder,Action<string> log,bool test,bool interrupted){
        if(!test)Closed();var root=Root(profile);var backup=Root(folder);var baseDir=Inside(root,"Astera-backups");if(!backup.StartsWith(baseDir+System.IO.Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new Exception("Choose a rollback folder belonging to this profile.");
        var j=Json.Deserialize<Journal>(File.ReadAllText(Inside(backup,"transaction.json")));if(j.Profile!=root||j.Status=="restored")throw new Exception("Rollback does not match this profile or was already restored.");
        bool incomplete=j.Status=="prepared"||j.Status=="applying";
        foreach(var e in j.Edits){Allowed(e.Path,true);var full=Inside(root,e.Path);if(e.Existed){var old=Inside(backup,"old/"+e.Path);if(!File.Exists(old)||Hash(old)!=e.Expected)throw new Exception("Rollback snapshot is missing/changed.");}if(File.Exists(full)){string actual=Hash(full);if(actual!=e.Desired&&actual!=e.Expected&&!interrupted)throw new Exception("File changed since update; preserved instead of overwriting: "+e.Path);}else if(e.Desired!=null&&!interrupted&&!incomplete)throw new Exception("File removed since update; review before rollback: "+e.Path);}
        foreach(var e in j.Edits.AsEnumerable().Reverse()){var full=Inside(root,e.Path);if(File.Exists(full))File.Delete(full);if(e.Existed){Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full));File.Copy(Inside(backup,"old/"+e.Path),full);}log("Restored: "+e.Path);}j.Status="restored";WriteJournal(backup,j);
    }
}
}
