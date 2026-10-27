using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using MC2Pack;
namespace Astera {
static partial class NetworkSelfTest {
    internal static int LockRun(string root){try{
        if(Directory.Exists(root))throw new Exception("Use a new lock audit directory");Directory.CreateDirectory(root);string source=Path.Combine(root,"source");Directory.CreateDirectory(source);Jar(source,"mods/ally.jar","stars_allies","0.7.173");Jar(source,"mods/fixes.jar","stars_general_fixes","1.7.206");var release=new Release{Schema=2,Sequence=10,PackId="astera-minecraft-squared",Name="lock-test",Minecraft="26.3",Loader="0.19.5",Files=new List<PackFile>(),Retired=new List<RetiredFile>(),Packs=new List<string>{"stars_general_fixes:stars_compat"}};
        foreach(string file in Directory.GetFiles(Path.Combine(source,"mods"))){var mod=Engine.Mod(file);release.Files.Add(new PackFile{Path="mods/"+Path.GetFileName(file),Hash=Engine.Hash(file),Size=new FileInfo(file).Length,Kind="mod",Policy="replace",Id=Convert.ToString(mod["id"]),Version=Convert.ToString(mod["version"])});}
        string zip=Path.Combine(root,"release.zip");using(var archive=ZipFile.Open(zip,ZipArchiveMode.Create)){using(var writer=new StreamWriter(archive.CreateEntry("mc2-release.json").Open()))writer.Write(Engine.Json.Serialize(release));foreach(var file in release.Files)archive.CreateEntryFromFile(Path.Combine(source,file.Path),file.Path);}
        Func<string,string> profile=delegate(string name){string path=Path.Combine(root,name);Directory.CreateDirectory(path);Put(path,"minecraftinstance.json","{\"baseModLoader\":{\"minecraftVersion\":\"26.3\",\"name\":\"fabric-0.19.5\",\"forgeVersion\":\"0.19.5\"}}");Jar(path,"mods/ally.jar","stars_allies","0.7.172");Put(path,"saves/protected/level.dat","world");return path;};var checks=new List<string>();
        string transient=profile("transient");var plan=Engine.Scan(transient,zip,true,false,false,delegate{});var handle=new FileStream(Path.Combine(transient,"mods/ally.jar"),FileMode.Open,FileAccess.Read,FileShare.Read);var releaseLock=new Thread(delegate(){Thread.Sleep(400);handle.Dispose();});releaseLock.Start();var timer=Stopwatch.StartNew();Engine.Apply(plan,delegate{},true);releaseLock.Join();Assert(timer.ElapsedMilliseconds>=250&&Engine.Scan(transient,zip,true,false,false,delegate{}).Edits.Count==0,"Transient sharing lock did not recover");checks.Add("A real temporary Windows rename/delete lock is retried and the update completes");
        string persistent=profile("persistent");var failed=Engine.Scan(persistent,zip,true,false,false,delegate{});string before=Engine.Hash(Path.Combine(persistent,"mods/ally.jar"));using(var locked=new FileStream(Path.Combine(persistent,"mods/ally.jar"),FileMode.Open,FileAccess.Read,FileShare.Read)){Reject(delegate{Engine.Apply(failed,delegate{},true);},"Persistent sharing lock was ignored");}Assert(Engine.Hash(Path.Combine(persistent,"mods/ally.jar"))==before&&File.ReadAllText(Path.Combine(persistent,"saves/protected/level.dat"))=="world","Failed locked install changed protected state");var journal=Directory.GetFiles(Path.Combine(persistent,"Astera-backups"),"transaction.json",SearchOption.AllDirectories).Single();Assert(File.ReadAllText(journal).Contains("restored"),"Persistent lock left an unresolved transaction");checks.Add("Persistent lock stops after a bounded wait and rollback preserves original mod/world without an unresolved transaction");
        int calls=0;Reject(delegate{Engine.RetrySharing(delegate{calls++;throw new IOException("access denied",unchecked((int)0x80070005));});},"Permanent IO error was ignored");Assert(calls==1,"Non-sharing failures were retried");checks.Add("Non-sharing IO errors fail immediately rather than being masked by retries");
        Assert(!Directory.GetFiles(transient,"*.mc2-*",SearchOption.AllDirectories).Any(),"Pending file remained in active folders");checks.Add("Atomic commit temporary files stay out of active mod/resource folders");File.WriteAllText(Path.Combine(root,"result.json"),Engine.Json.Serialize(new{Passed=true,Checks=checks,LiveProfileChanged=false}));return 0;
    }catch(Exception error){Directory.CreateDirectory(root);File.WriteAllText(Path.Combine(root,"failure.txt"),error.ToString());return 1;}}
}
}
