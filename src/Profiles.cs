using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MC2Pack;
namespace Astera {
public sealed class GameProfile {
 public string Path,Name,Minecraft="",Loader="",LoaderKind="",Guid="",Icon="",LauncherRoot="";public bool LegacyFolder,IsPrism,IsSquared;
 public override string ToString(){return Name+" — "+(IsPrism?"Prism":"CurseForge")+(String.IsNullOrWhiteSpace(Minecraft)?"":" · "+Minecraft);}
}
public static class Profiles {
 public static string DefaultRoot {get{return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"curseforge","minecraft","Instances");}}
 static string Normal(string value){return (value??"").ToLowerInvariant().Replace("²","squared").Replace(" ","").Replace("-","").Replace("_","");}
 public static GameProfile Read(string path){var root=Engine.Root(path);var metadata=Engine.Inside(root,"minecraftinstance.json");GameProfile result;
  if(!File.Exists(metadata)){var prism=PrismProfiles.Read(root);result=new GameProfile{Path=prism.GameRoot,Name=prism.Name,IsPrism=true,Minecraft=prism.Minecraft,Loader=prism.Loader??"",LoaderKind=prism.LoaderKind};string icon=System.IO.Path.Combine(prism.Root,"icon.png");if(!File.Exists(icon))icon=System.IO.Path.Combine(prism.GameRoot,"icon.png");if(File.Exists(icon))result.Icon=icon;}
  else{if(new FileInfo(metadata).Length>8*1024*1024)throw new IOException("Launcher instance metadata is too large");var data=Engine.Json.Deserialize<Dictionary<string,object>>(File.ReadAllText(metadata));if(data==null)throw new IOException("Invalid CurseForge profile metadata");object raw;result=new GameProfile{Path=root,Name=data.TryGetValue("name",out raw)?Convert.ToString(raw):System.IO.Path.GetFileName(root),LegacyFolder=Normal(System.IO.Path.GetFileName(root))=="butagain"};if(data.TryGetValue("guid",out raw))result.Guid=Convert.ToString(raw);if(data.TryGetValue("gameVersion",out raw))result.Minecraft=Convert.ToString(raw);if(data.TryGetValue("baseModLoader",out raw)){var loader=raw as Dictionary<string,object>;if(loader!=null){if(loader.TryGetValue("minecraftVersion",out raw))result.Minecraft=Convert.ToString(raw);if(loader.TryGetValue("forgeVersion",out raw))result.Loader=Convert.ToString(raw);if(loader.TryGetValue("name",out raw)){string name=Convert.ToString(raw);result.LoaderKind=name.StartsWith("fabric-")?"fabric":name.StartsWith("quilt-")?"quilt":name.StartsWith("neoforge-")?"neoforge":"forge";}}}if(String.IsNullOrWhiteSpace(result.LoaderKind))result.LoaderKind="vanilla";if(data.TryGetValue("profileImagePath",out raw)){string icon=Convert.ToString(raw);if(!String.IsNullOrWhiteSpace(icon)&&System.IO.Path.IsPathRooted(icon)&&File.Exists(icon))result.Icon=icon;}}
  string marker=System.IO.Path.Combine(result.Path,"Astera-minecraft-state.json");bool owned=false;if(File.Exists(marker)){Engine.Plain(marker);if(new FileInfo(marker).Length<=4*1024*1024){var ledger=Engine.Json.Deserialize<Dictionary<string,object>>(File.ReadAllText(marker));object id;owned=ledger!=null&&ledger.TryGetValue("PackId",out id)&&Convert.ToString(id)=="astera-minecraft-squared";}}
  string normal=Normal(result.Name);result.IsSquared=owned||normal=="minecraftsquared"||normal=="minecraft2"||result.LegacyFolder&&Directory.Exists(System.IO.Path.Combine(result.Path,"mods"))&&Directory.GetFiles(System.IO.Path.Combine(result.Path,"mods"),"stars-allies-*.jar").Length>0;return result;
 }
 public static List<GameProfile> All(string directory){var found=new List<GameProfile>();if(!Directory.Exists(directory))return found;PrismProfiles.SafePath(directory);foreach(string path in Directory.GetDirectories(directory).Take(1000))try{found.Add(Read(path));}catch(IOException){}catch(UnauthorizedAccessException){}catch(ArgumentException){}catch(InvalidOperationException){}catch(FormatException){}return found.OrderBy(p=>p.Name,StringComparer.OrdinalIgnoreCase).ThenBy(p=>p.Path).ToList();}
 public static List<GameProfile> Discover(string directory){return All(directory).Where(p=>p.IsSquared).ToList();}
}
}
