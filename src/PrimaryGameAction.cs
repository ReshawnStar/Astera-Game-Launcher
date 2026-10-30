using System;
using System.IO;
using System.Linq;
using MC2Pack;
namespace Astera {
sealed partial class LibraryForm {
    bool minecraftChannelBehind;
    async void PrimaryGameAction(){if(busy)return;if(IsCustomGame){if(CurrentCustom.ManagedProjectId==ManagedGames.Id&&(play.Text=="UPDATE"||play.Text=="INSTALL")){await InstallManagedProject(NebulaProject);return;}if(play.Text=="LOCATE GAME")EditCustomGame();else LaunchCustomGame();return;}if((play.Text=="PLAY"||play.Text=="RUNNING"||play.Text=="STARTING…")&&SelectedRunningGame!=null){int selected=GameKind;if(!await GuardBuiltinLaunch()||selected!=GameKind)return;}if(IsMultiverse){PrimaryMultiverse();return;}if(GameKind==1){OpenStarDash();return;}if(updateReady){if(plan==null)await Download();if(plan!=null)await Install();}else OpenMinecraft();}
    internal static bool NewerLocalComponents(UpdateFeed update,string profile){string mods=Path.Combine(Engine.Root(profile),"mods");if(!Directory.Exists(mods))return false;bool newer=false;var ids=new System.Collections.Generic.HashSet<string>();foreach(var file in Directory.GetFiles(mods,"*.jar")){var metadata=Engine.Mod(file);if(metadata==null||!metadata.ContainsKey("id"))continue;string id=Convert.ToString(metadata["id"]);if(!ids.Add(id))throw new Exception("Duplicate mod identity in the installation");var wanted=update.Release.Files.FirstOrDefault(f=>f.Kind=="mod"&&f.Id==id);if(wanted==null)continue;Version have,want;if(Version.TryParse(Convert.ToString(metadata["version"]).Split('+')[0],out have)&&Version.TryParse(wanted.Version.Split('+')[0],out want)&&have>want)newer=true;}return newer;}
}
}
