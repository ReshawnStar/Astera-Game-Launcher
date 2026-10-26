using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MC2Pack;
namespace Astera {
public sealed class GameProfile {public string Path,Name;public bool LegacyFolder;public override string ToString(){return Name+" — "+System.IO.Path.GetFileName(Path)+(LegacyFolder?" (legacy folder name)":"");}}
public static class Profiles {
    public static string DefaultRoot {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"curseforge","minecraft","Instances");}}
    static string Normal(string name){return (name??"").ToLowerInvariant().Replace("²","squared").Replace(" ","").Replace("-","").Replace("_","");}
    public static GameProfile Read(string path){var root=Engine.Root(path);var metadata=Engine.Inside(root,"minecraftinstance.json");if(!File.Exists(metadata))throw new Exception("Choose a profile created in CurseForge, not its Instances parent folder.");var data=Engine.Json.Deserialize<Dictionary<string,object>>(File.ReadAllText(metadata));object value;string name=data.TryGetValue("name",out value)?Convert.ToString(value):Path.GetFileName(root);return new GameProfile{Path=root,Name=name,LegacyFolder=Normal(Path.GetFileName(root))=="butagain"};}
    public static List<GameProfile> Discover(string directory){var found=new List<GameProfile>();if(!Directory.Exists(directory))return found;Engine.Plain(directory);foreach(var path in Directory.GetDirectories(directory)){try{var profile=Read(path);var name=Normal(profile.Name);if(name=="minecraftsquared"||name=="minecraft2"||profile.LegacyFolder&&Directory.Exists(Engine.Inside(profile.Path,"mods"))&&Directory.GetFiles(Engine.Inside(profile.Path,"mods"),"stars-allies-*.jar").Length>0)found.Add(profile);}catch(IOException){}catch(UnauthorizedAccessException){}catch(ArgumentException){}catch(Exception){}}return found.OrderBy(p=>p.Name).ThenBy(p=>p.Path).ToList();}
}
}
