using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace Astera {
sealed class LaunchFlavor {
 readonly StartupFlavorPool pool;DateTime next;internal string Text="";
 internal LaunchFlavor(string root,bool minecraft){pool=StartupFlavorPool.Load(root).ForLaunch(minecraft);}
 internal string At(DateTime start,bool enabled){if(!enabled||DateTime.UtcNow-start<TimeSpan.FromSeconds(6))return "";if(DateTime.UtcNow>=next){Text=pool.Next();next=DateTime.UtcNow.AddSeconds(5);}return Text;}
}
sealed partial class LibraryForm {
 sealed class GameLaunchWait {internal string Id,Activity,Name;internal CustomGame Game;internal DateTime Started;internal bool TimedOut,Dismissed,OverlayDismissed;internal LaunchFlavor Flavor;internal HashSet<IntPtr> ExistingWindows;}
 readonly Dictionary<string,GameLaunchWait> gameLaunchWaits=new Dictionary<string,GameLaunchWait>();
 readonly Timer gameWaitClock=new Timer{Interval=500};bool gameWaitReady,gameWaitPolling;DateTime nextGameWaitProbe;
 static string LaunchRunningId(string key,CustomGame game){return key=="builtin:0"?"builtin-minecraft:"+MC2Pack.Engine.Hash(System.Text.Encoding.UTF8.GetBytes(game.Executable.ToLowerInvariant())).Substring(0,16):key=="builtin:1"?"builtin-stardash":key=="builtin:2"?"builtin-ftl":game.Id;}
 GameLaunchWait SelectedLaunchWait {get{string key=SelectedActivityKey;return gameLaunchWaits.Values.FirstOrDefault(w=>!w.Dismissed&&w.Activity==key&&(key!="builtin:0"||String.Equals(w.Game.Executable,store.Settings.Profile,StringComparison.OrdinalIgnoreCase)));}}
 void BeginGameLaunchWait(string key,CustomGame game,bool audit=false){if((previewMode&&!audit)||game==null||key=="builtin:0"&&minecraftLaunch!=null)return;string id=LaunchRunningId(key,game);var copy=new CustomGame{Id=id,Executable=game.Executable,Platform=game.Platform,PlatformId=game.PlatformId,Name=game.Name};
  var wait=new GameLaunchWait{Id=id,Activity=key,Game=copy,Name=!String.IsNullOrWhiteSpace(game.Name)?game.Name:key=="builtin:0"?"Minecraft":key=="builtin:1"?"Star Dash":"FTL: Multiverse",Started=DateTime.UtcNow,ExistingWindows=game.Platform=="minecraft"?MinecraftWindows.Visible():null,Flavor=new LaunchFlavor(store.Root,key=="builtin:0")};gameLaunchWaits[id]=wait;startingGames[id]=DateTime.UtcNow.AddMinutes(2);
  if(!gameWaitReady){gameWaitReady=true;gameWaitClock.Tick+=async delegate{await PollGameLaunchWaits();};Disposed+=delegate{gameWaitClock.Dispose();};}nextGameWaitProbe=DateTime.MinValue;gameWaitClock.Start();UpdateWorkProgress();
 }
 bool ObserveGameLaunch(GameLaunchWait wait,bool detected){GameLaunchWait current;if(!gameLaunchWaits.TryGetValue(wait.Id,out current)||!Object.ReferenceEquals(current,wait))return false;
  if(detected){gameLaunchWaits.Remove(wait.Id);startingGames.Remove(wait.Id);runningGames[wait.Id]=true;LogActivity(wait.Activity,wait.Game.Platform=="roblox"?"Roblox client detected. Joining the experience is managed by Roblox.":wait.Name+" process detected. Further loading is handled by the game.");return true;}
  else if(!wait.TimedOut&&DateTime.UtcNow-wait.Started>=TimeSpan.FromMinutes(2)){wait.TimedOut=true;startingGames.Remove(wait.Id);LogActivity(wait.Activity,"Still waiting for "+wait.Name+". Check its launcher if needed.");return true;}return false;
 }
 async Task PollGameLaunchWaits(){if(gameWaitPolling||IsDisposed)return;UpdateWorkProgress();if(DateTime.UtcNow<nextGameWaitProbe)return;var waits=gameLaunchWaits.Values.Where(w=>!w.TimedOut).ToArray();if(waits.Length==0){gameWaitClock.Stop();return;}gameWaitPolling=true;nextGameWaitProbe=DateTime.UtcNow.AddSeconds(2.5);
  try{var found=await Task.Run(()=>waits.Select(w=>{try{return w.Game.Platform=="minecraft"?MinecraftWindows.Ready(w.Game,w.ExistingWindows):PlaytimeProbe.Running(w.Game);}catch(Exception){return false;}}).ToArray());if(IsDisposed)return;bool changed=false;for(int i=0;i<waits.Length;i++)changed|=ObserveGameLaunch(waits[i],found[i]);if(changed)RefreshView();if(!gameLaunchWaits.Values.Any(w=>!w.TimedOut))gameWaitClock.Stop();}finally{gameWaitPolling=false;}
 }
 void ClearLateLaunchNotices(){bool running;if(minecraftLaunch!=null&&minecraftLaunch.TimedOut&&minecraftLaunch.GameId!=null&&runningGames.TryGetValue(minecraftLaunch.GameId,out running)&&running)StopMinecraftLaunch();foreach(var wait in gameLaunchWaits.Values.Where(w=>w.TimedOut).ToArray())if(runningGames.TryGetValue(wait.Id,out running)&&running)ObserveGameLaunch(wait,true);}
 void DismissGameLaunchWait(){var wait=SelectedLaunchWait;if(wait==null)return;if(wait.TimedOut)gameLaunchWaits.Remove(wait.Id);else wait.Dismissed=true;UpdateWorkProgress();}
 void PresentGameLaunchWait(GameLaunchWait wait){workPanel.Visible=true;releaseLabel.Visible=false;workHeartbeat.Stop();if(gameBody!=null)gameBody.RowStyles[3].Height=80;workMeter.Fraction=wait.TimedOut||launchScreen!=null?0:-1;workLabel.Text=(wait.TimedOut?"Still waiting for ":"Starting ")+wait.Name+(wait.TimedOut?". Check its launcher if needed.":"… · "+(DateTime.UtcNow-wait.Started).ToString(@"mm\:ss"));workFlavor.Text=wait.TimedOut?"This does not mean the game failed. You can check its launcher or dismiss this notice.":wait.Flavor.At(wait.Started,store.Settings.StartupFlavor);workFlavor.Visible=true;workCancel.Text=workCancel.AccessibleName="Dismiss";workCancel.Visible=workCancel.Enabled=true;workCancel.Parent.Width=95;workCancel.Width=95;}
}
}
