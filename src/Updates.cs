using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using MC2Pack;

namespace Astera {
public sealed class Channel { public string FeedUrl="",AppFeedUrl="",PublicKeyXml=""; }
public sealed class Preferences {public List<string> ThunderstoreRoots=new List<string>(); public string GameScanRoot="";public string ItchAppsRoot="";public bool CheckAppOnStart=true;public bool TrackPlaytime=true;public List<string> FavoriteGames=new List<string>(),HiddenGames=new List<string>();public List<LibraryFolder> LibraryFolders=new List<LibraryFolder>();public bool StartupFlavor=true;public bool AnimatedEffects=true; public List<CustomGame> CustomGames=new List<CustomGame>(); public string Profile="",FeedUrl="",StarDashExecutable="",BackgroundMinecraft="",BackgroundStarDash="",CharacterMinecraft="",CharacterStarDash="",ButtonAccent="Violet",FtlDirectory="",FtlWorkspace="",BackgroundFtl="",CharacterFtl="";public bool FtlRenegadeCollection=true;public bool FtlGeneratedGibs=true;public bool CheckOnStart=true,KeepInTray=true,ConfigurePacks=true,UseMinecraftSkin=true;public long HighestSequence,HighestAppSequence; }
public sealed class Envelope {public string Payload,Signature;}
public sealed class PayloadSource {public string Path,Url;}
public sealed class RequiredMod {public string Id,Version;}
public sealed class BundleSource {public string Url,Hash;public long Size;} public sealed class UpdateFeed {public BundleSource Bundle;public int Schema;public string GameId,Notes;public long ExpiresUtc;public Release Release;public List<PayloadSource> Sources;public List<RequiredMod> RequiredMods;}
public sealed class DownloadPlan {public UpdateFeed Feed;public Dictionary<string,string> Local=new Dictionary<string,string>();public Dictionary<string,string> Guards=new Dictionary<string,string>();public long DownloadBytes;public int DownloadFiles;}
public sealed class Store {
    public readonly string Root,Cache;public Preferences Settings;
    public Store(string root){Root=Engine.Root(root);Directory.CreateDirectory(Root);Cache=Engine.Inside(Root,"cache");Directory.CreateDirectory(Cache);var p=Engine.Inside(Root,"preferences.json");Settings=File.Exists(p)?Engine.Json.Deserialize<Preferences>(File.ReadAllText(p)):new Preferences();if(Settings==null)throw new Exception("Astera settings are unreadable.");}
    readonly object saveGate=new object(); public void Save(){lock(saveGate){var p=Engine.Inside(Root,"preferences.json");var next=Engine.Inside(Root,"preferences.next");File.WriteAllText(next,Engine.Json.Serialize(Settings),new UTF8Encoding(false));if(File.Exists(p))File.Replace(next,p,null);else File.Move(next,p);}}
}
public sealed class Transport {
    readonly bool testLoopback;
    public Transport():this(false){} internal Transport(bool allowTestLoopback){testLoopback=allowTestLoopback;}
    public Uri Address(string value){Uri uri;if(!Uri.TryCreate(value,UriKind.Absolute,out uri)||!String.IsNullOrEmpty(uri.UserInfo)||!String.IsNullOrEmpty(uri.Fragment)||!(uri.Scheme=="https"||testLoopback&&uri.Scheme=="http"&&uri.IsLoopback))throw new Exception("Update addresses must use HTTPS without embedded credentials.");return uri;}
    public void Get(string address,Stream output,long limit,long exact,CancellationToken cancel,Action<long> progress){
        var uri=Address(address);ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
        for(int redirects=0;redirects<6;redirects++){
            cancel.ThrowIfCancellationRequested();var request=(HttpWebRequest)WebRequest.Create(uri);request.AllowAutoRedirect=false;request.Timeout=30000;request.ReadWriteTimeout=30000;request.UserAgent="Astera/0.1";request.AutomaticDecompression=DecompressionMethods.None;
            using(cancel.Register(delegate{request.Abort();}))using(var response=(HttpWebResponse)request.GetResponse()){
                int code=(int)response.StatusCode;if(code>=300&&code<400){var location=response.Headers["Location"];if(String.IsNullOrEmpty(location))throw new Exception("Update redirect has no address.");uri=Address(new Uri(uri,location).AbsoluteUri);continue;}
                if(code!=200)throw new Exception("Update server returned "+code+".");
                if(response.ContentLength>limit||exact>=0&&response.ContentLength>=0&&response.ContentLength!=exact)throw new Exception("Unexpected download size.");
                long count=0;using(var input=response.GetResponseStream()){var buffer=new byte[65536];int read;while((read=input.Read(buffer,0,buffer.Length))>0){cancel.ThrowIfCancellationRequested();count+=read;if(count>limit)throw new Exception("Download exceeded its size limit.");output.Write(buffer,0,read);if(progress!=null)progress(count);}}
                if(exact>=0&&count!=exact)throw new Exception("Download was interrupted or has the wrong size.");return;
            }
        }throw new Exception("Too many update redirects.");
    }
    public byte[] Metadata(string url,CancellationToken cancel){using(var memory=new MemoryStream()){Get(url,memory,8*1024*1024,-1,cancel,null);return memory.ToArray();}}
}
public sealed partial class UpdateClient {
    readonly string key;readonly Transport transport;
    public UpdateClient(string publicKey):this(publicKey,new Transport()){}internal UpdateClient(string publicKey,Transport network){key=publicKey;transport=network;}
    public UpdateFeed Check(string url,CancellationToken cancel){return Verify(transport.Metadata(url,cancel));}
    public UpdateFeed Verify(byte[] envelope){
        if(envelope.Length>8*1024*1024)throw new Exception("Update notice is too large.");var signed=Engine.Json.Deserialize<Envelope>(Encoding.UTF8.GetString(envelope));if(signed==null)throw new Exception("Missing signed notice.");var bytes=Convert.FromBase64String(signed.Payload);if(bytes.Length>6*1024*1024)throw new Exception("Update details are too large.");
        using(var rsa=new RSACryptoServiceProvider(2048,new CspParameters(24))){rsa.PersistKeyInCsp=false;rsa.FromXmlString(key);if(!rsa.VerifyData(bytes,"SHA256",Convert.FromBase64String(signed.Signature)))throw new Exception("This update was not signed by Star. Nothing was installed.");}
        var feed=Engine.Json.Deserialize<UpdateFeed>(Encoding.UTF8.GetString(bytes));Validate(feed);return feed;
    }
    public void Validate(UpdateFeed feed){
        if(feed==null||(feed.Schema!=1&&feed.Schema!=2)||feed.GameId!="minecraft-squared"||feed.Sources==null||feed.RequiredMods==null||feed.Notes==null||feed.Notes.Length>32000)throw new Exception("Unsupported Astera update notice.");
        if(feed.ExpiresUtc<DateTimeOffset.UtcNow.ToUnixTimeSeconds())throw new Exception("This update notice has expired. Ask Star for the current channel.");
        Engine.Validate(feed.Release);if(feed.Schema==2){ValidateFullBundle(feed);return;}if(feed.Bundle!=null)throw new Exception("A component update cannot supply a full bundle.");var paths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);foreach(var source in feed.Sources){if(source==null||!paths.Add(source.Path)||!feed.Release.Files.Any(f=>f.Path==source.Path))throw new Exception("Invalid download mapping.");transport.Address(source.Url);}
        if(paths.Count!=feed.Release.Files.Count)throw new Exception("An update file has no download address.");
        var ids=new HashSet<string>();foreach(var required in feed.RequiredMods)if(required==null||String.IsNullOrWhiteSpace(required.Id)||String.IsNullOrWhiteSpace(required.Version)||!ids.Add(required.Id)||feed.Release.Files.Any(f=>f.Kind=="mod"&&f.Id==required.Id))throw new Exception("Invalid CurseForge requirements.");
        foreach(var f in feed.Release.Files){Engine.Inside(Path.Combine(Path.GetTempPath(),"Astera-path-check"),f.Path);if(f.Kind=="mod"&&!new[]{"stars_allies","stars_general_fixes","waystone_map_travel"}.Contains(f.Id))throw new Exception("This channel cannot replace CurseForge's third-party mods.");}
    }
    public DownloadPlan Prepare(UpdateFeed feed,string profile,string cache,bool snapshotDependencies=true){
        Validate(feed);var root=Engine.Root(profile);if(!Directory.Exists(root))throw new Exception("Select your CurseForge profile first.");
        Engine.Runtime(root,feed.Release,false,new List<string>());
        var installed=new Dictionary<string,string>();var installedPaths=new Dictionary<string,string>();string mods=Engine.Inside(root,"mods");if(Directory.Exists(mods))foreach(var path in Directory.GetFiles(mods,"*.jar")){Engine.Plain(path);var m=Engine.Mod(path);if(m!=null&&m.ContainsKey("id")&&m.ContainsKey("version")){string id=Convert.ToString(m["id"]);if(installed.ContainsKey(id))throw new Exception("Duplicate mod ID in profile: "+id);installed[id]=Convert.ToString(m["version"]);installedPaths[id]=path;}}
        var missing=feed.RequiredMods.Where(m=>!installed.ContainsKey(m.Id)||installed[m.Id]!=m.Version).Select(m=>m.Id+" "+m.Version).ToArray();if(missing.Length>0)throw new Exception("Update these in CurseForge first:\n"+String.Join("\n",missing.Take(12))+(missing.Length>12?"\n…and "+(missing.Length-12)+" more.":""));
        var plan=new DownloadPlan{Feed=feed};if(snapshotDependencies)foreach(var required in feed.RequiredMods){string path=installedPaths[required.Id];plan.Guards["mods/"+Path.GetFileName(path)]=Engine.Hash(path);}foreach(var f in feed.Release.Files){var local=Engine.Inside(root,f.Path);var cached=Engine.Inside(Engine.Root(cache),f.Hash+".bin");string use=Matches(local,f)?local:Matches(cached,f)?cached:null;if(use!=null)plan.Local[f.Path]=use;else{plan.DownloadFiles++;plan.DownloadBytes+=f.Size;}}if(feed.Schema==2){if(snapshotDependencies)foreach(var path in installedPaths.Values)plan.Guards["mods/"+Path.GetFileName(path)]=Engine.Hash(path);if(plan.DownloadFiles>0){plan.DownloadFiles=1;plan.DownloadBytes=feed.Bundle.Size;}}return plan;
    }
    static bool Matches(string path,PackFile file){return File.Exists(path)&&new FileInfo(path).Length==file.Size&&Engine.Hash(path).Equals(file.Hash,StringComparison.OrdinalIgnoreCase);}
    public string Materialize(DownloadPlan plan,string cache,CancellationToken cancel,Action<string> log,Action<string,long,long> progress=null){
        if(plan.Feed.Schema==2)return MaterializeFullBundle(plan,cache,cancel,log,progress);var root=Engine.Root(cache);Directory.CreateDirectory(root);long total=plan.Feed.Release.Files.Sum(f=>f.Size);if(new DriveInfo(Path.GetPathRoot(root)).AvailableFreeSpace<total+plan.DownloadBytes+64L*1024*1024)throw new Exception("Not enough space for downloads and local staging.");
        foreach(var file in plan.Feed.Release.Files){cancel.ThrowIfCancellationRequested();string path;if(plan.Local.TryGetValue(file.Path,out path)){if(!Matches(path,file))throw new Exception("A local file changed during the check. Check again.");continue;}
            path=Engine.Inside(root,file.Hash+".bin");var temp=Engine.Inside(root,file.Hash+".partial");log("Downloading: "+file.Path);try{using(var output=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)){transport.Get(plan.Feed.Sources.Single(s=>s.Path==file.Path).Url,output,file.Size,file.Size,cancel,null);}if(!Matches(temp,file))throw new Exception("Downloaded file failed verification: "+file.Path);if(File.Exists(path))File.Delete(path);File.Move(temp,path);plan.Local[file.Path]=path;log("Verified download: "+file.Path);}finally{if(File.Exists(temp))File.Delete(temp);}}
        var bundle=Engine.Inside(root,"prepared-"+Guid.NewGuid().ToString("N")+".zip");try{using(var zip=ZipFile.Open(bundle,ZipArchiveMode.Create)){using(var writer=new StreamWriter(zip.CreateEntry("mc2-release.json").Open(),new UTF8Encoding(false)))writer.Write(Engine.Json.Serialize(plan.Feed.Release));foreach(var f in plan.Feed.Release.Files){cancel.ThrowIfCancellationRequested();if(!Matches(plan.Local[f.Path],f))throw new Exception("Staging source changed: "+f.Path);zip.CreateEntryFromFile(plan.Local[f.Path],f.Path,CompressionLevel.NoCompression);}}Engine.Read(bundle);return bundle;}catch{if(File.Exists(bundle))File.Delete(bundle);throw;}
    }
}
}
