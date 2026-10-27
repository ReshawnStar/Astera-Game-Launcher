using System;
using System.IO;
using System.Linq;
using System.Threading;
using MC2Pack;
namespace Astera {
public sealed partial class UpdateClient {
    void ValidateFullBundle(UpdateFeed feed){
        if(feed.Bundle==null||feed.Sources.Count!=0||feed.RequiredMods.Count!=0||feed.Bundle.Size<=0||feed.Bundle.Size>4L*1024*1024*1024||!System.Text.RegularExpressions.Regex.IsMatch(feed.Bundle.Hash??"","^[A-Fa-f0-9]{64}$"))throw new Exception("Invalid full-pack update notice.");
        transport.Address(feed.Bundle.Url);
        if(!feed.Release.Files.Any(f=>f.Id=="stars_allies")||!feed.Release.Files.Any(f=>f.Id=="stars_general_fixes"))throw new Exception("Full pack is missing its Stars components.");
    }
    string MaterializeFullBundle(DownloadPlan plan,string cache,CancellationToken cancel,Action<string> log){
        Validate(plan.Feed);var root=Engine.Root(cache);Directory.CreateDirectory(root);
        var info=plan.Feed.Bundle;var cached=Engine.Inside(root,info.Hash.ToUpperInvariant()+"-full.zip");var partial=cached+".partial";
        long required=info.Size+plan.Feed.Release.Files.Sum(f=>f.Size)+64L*1024*1024;
        if(new DriveInfo(Path.GetPathRoot(root)).AvailableFreeSpace<required)throw new Exception("Not enough space to stage the full-pack update.");
        Func<bool> matches=()=>File.Exists(cached)&&new FileInfo(cached).Length==info.Size&&Engine.Hash(cached).Equals(info.Hash,StringComparison.OrdinalIgnoreCase);
        if(!matches()){
            log("Downloading Minecraft² pack…");
            try{using(var output=new FileStream(partial,FileMode.Create,FileAccess.Write,FileShare.None)){transport.Get(info.Url,output,info.Size,info.Size,cancel,null);}cancel.ThrowIfCancellationRequested();if(new FileInfo(partial).Length!=info.Size||!Engine.Hash(partial).Equals(info.Hash,StringComparison.OrdinalIgnoreCase))throw new Exception("Full-pack download failed verification.");if(File.Exists(cached))File.Delete(cached);File.Move(partial,cached);}finally{if(File.Exists(partial))File.Delete(partial);}
        }else log("Using verified cached Minecraft² pack.");
        cancel.ThrowIfCancellationRequested();
        var release=Engine.Read(cached);
        if(Engine.Json.Serialize(release)!=Engine.Json.Serialize(plan.Feed.Release))throw new Exception("The ZIP contents do not match the signed update notice.");
        Engine.VerifyPayload(cached,release,log);cancel.ThrowIfCancellationRequested();
        var prepared=Engine.Inside(root,"prepared-full-"+Guid.NewGuid().ToString("N")+".zip");
        try{File.Copy(cached,prepared);cancel.ThrowIfCancellationRequested();return prepared;}catch{if(File.Exists(prepared))File.Delete(prepared);throw;}
    }
}
}