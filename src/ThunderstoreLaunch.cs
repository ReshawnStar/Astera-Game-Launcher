using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using MC2Pack;
namespace Astera {
static class ThunderstoreLaunch {
    internal static string Arguments(ThunderGame game,string profile){
        if(game==null||!PlatformLibraries.Id(game.SteamId)||game.Loader!="bepinex")throw new IOException("This profile's loader is not supported for direct launch yet");
        if(profile==null)return "-applaunch "+game.SteamId+" --doorstop-enabled false --doorstop-enable false";
        profile=SelfUpdateFiles.Root(profile);string preloader=new[]{"BepInEx/core/BepInEx.Preloader.dll","BepInEx/core/BepInEx.Unity.Mono.Preloader.dll"}.Select(p=>SelfUpdateFiles.Inside(profile,p)).FirstOrDefault(File.Exists);
        if(preloader==null)throw new IOException("This profile needs a supported Mono BepInEx loader. Open it in its manager first.");
        string marker=Path.Combine(profile,".doorstop_version");bool modern=false;if(File.Exists(marker)){if(new FileInfo(marker).Length>128)throw new IOException("Invalid loader version marker");var match=System.Text.RegularExpressions.Regex.Match(File.ReadAllText(marker),@"\d+");modern=match.Success&&Int32.Parse(match.Value)>=4;}
        return "-applaunch "+game.SteamId+(modern?" --doorstop-enabled true --doorstop-target-assembly ":" --doorstop-enable true --doorstop-target ")+SelfUpdateFiles.Quote(preloader);
    }
    internal static void Link(string gameFolder,string profile,string backupRoot){
        gameFolder=SelfUpdateFiles.Root(gameFolder);profile=SelfUpdateFiles.Root(profile);if(!Directory.Exists(gameFolder))throw new IOException("Game installation is missing");
        string backup=null;
        foreach(string name in new[]{"winhttp.dll","doorstop_config.ini",".doorstop_version"}){
            string source=SelfUpdateFiles.Inside(profile,name),target=SelfUpdateFiles.Inside(gameFolder,name);if(!File.Exists(source)){if(name=="winhttp.dll")throw new IOException("The selected profile's Windows loader is missing");continue;}
            if(new FileInfo(source).Length>32*1024*1024)throw new IOException("Unexpected loader file size");if(File.Exists(target)&&Engine.Hash(target)==Engine.Hash(source))continue;
            if(File.Exists(target)){if(backup==null){backup=SelfUpdateFiles.Inside(SelfUpdateFiles.Root(backupRoot),DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(backup);}File.Copy(target,SelfUpdateFiles.Inside(backup,name));}
            string pending=target+".astera-"+Guid.NewGuid().ToString("N");try{File.Copy(source,pending);if(Engine.Hash(pending)!=Engine.Hash(source))throw new IOException("Loader copy verification failed");if(File.Exists(target))File.Replace(pending,target,null);else File.Move(pending,target);}finally{if(File.Exists(pending))File.Delete(pending);}
        }
    }
    internal static ProcessStartInfo Start(ThunderGame game,string profile,string gameFolder,string backupRoot,bool link=true){
        string args=Arguments(game,profile);string steam=SteamArtwork.Roots().Select(r=>Path.Combine(r,"Steam.exe")).FirstOrDefault(File.Exists);if(steam==null)throw new IOException("Steam.exe could not be found. Open Steam, then try again.");if(profile!=null&&link)Link(gameFolder,profile,backupRoot);
        return new ProcessStartInfo(steam,args){UseShellExecute=false,WorkingDirectory=Path.GetDirectoryName(steam)};
    }
}
}
