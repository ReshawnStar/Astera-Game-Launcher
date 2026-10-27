using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
namespace Astera {
static class ItchMetadata {
    internal static ItchReceiptGame Read(string folder){
        string path=Path.Combine(folder,".itch","receipt.json.gz");if(!File.Exists(path)||new FileInfo(path).Length>512*1024)return null;
        using(var source=File.OpenRead(path))using(var gzip=new GZipStream(source,CompressionMode.Decompress))using(var output=new MemoryStream()){var bytes=new byte[4096];int count;while((count=gzip.Read(bytes,0,bytes.Length))>0){if(output.Length+count>1024*1024)throw new IOException("Receipt too large");output.Write(bytes,0,count);}var receipt=new System.Web.Script.Serialization.JavaScriptSerializer{MaxJsonLength=1024*1024}.Deserialize<ItchReceipt>(Encoding.UTF8.GetString(output.ToArray()));var game=receipt==null?null:receipt.game;return game!=null&&PlatformLibraries.Id(game.id.ToString())&&!String.IsNullOrWhiteSpace(game.title)?game:null;}
    }
    internal static ItchReceiptGame Find(CustomGame game){if(game.Platform=="steam")return null;string folder=String.IsNullOrEmpty(game.Platform)?Path.GetDirectoryName(game.Executable):game.Executable;for(int i=0;i<7&&!String.IsNullOrEmpty(folder);i++){var found=Read(folder);if(found!=null)return game.Platform=="itch"&&found.id.ToString()!=game.PlatformId?null:found;folder=Path.GetDirectoryName(folder.TrimEnd(Path.DirectorySeparatorChar));}return null;}
}
static class ItchArtwork {
    internal static bool Address(string value){Uri uri;return Uri.TryCreate(value,UriKind.Absolute,out uri)&&uri.Scheme=="https"&&uri.Host.Equals("img.itch.zone",StringComparison.OrdinalIgnoreCase)&&String.IsNullOrEmpty(uri.UserInfo)&&String.IsNullOrEmpty(uri.Fragment)&&uri.IsDefaultPort;}
    internal static string Cached(string root,ItchReceiptGame game){if(game==null||!PlatformLibraries.Id(game.id.ToString())||!Address(game.coverUrl))return null;string hash;using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(game.coverUrl))).Replace("-","").Substring(0,20);return Path.Combine(root,game.id+"-"+hash+".png");}
    internal static string Resolve(CustomGame game,string root,CancellationToken cancel){var metadata=ItchMetadata.Find(game);string path=Cached(root,metadata);if(path==null)return null;if(File.Exists(path)){try{using(var image=Banner.ReadArtwork(path)){}return path;}catch(ArgumentException){}catch(IOException){}}
        Directory.CreateDirectory(root);byte[] bytes=Download(metadata.coverUrl,cancel);string temp=path+"."+Guid.NewGuid().ToString("N")+".partial";
        try{using(var memory=new MemoryStream(bytes))using(var image=Image.FromStream(memory,true,true)){if(image.Width<8||image.Height<8||image.Width>4096||image.Height>4096)throw new IOException("Unsupported itch cover dimensions");double scale=Math.Min(1,1200D/Math.Max(image.Width,image.Height));using(var bitmap=new Bitmap(Math.Max(1,(int)(image.Width*scale)),Math.Max(1,(int)(image.Height*scale)),PixelFormat.Format32bppArgb)){using(var graphics=Graphics.FromImage(bitmap)){graphics.Clear(Color.Transparent);graphics.DrawImage(image,0,0,bitmap.Width,bitmap.Height);}bitmap.Save(temp,ImageFormat.Png);}}cancel.ThrowIfCancellationRequested();if(File.Exists(path))File.Delete(path);File.Move(temp,path);return path;}finally{if(File.Exists(temp))File.Delete(temp);}
    }
    static byte[] Download(string address,CancellationToken cancel){ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;var uri=new Uri(address);for(int attempt=0;attempt<5;attempt++){
        if(!Address(uri.AbsoluteUri))throw new IOException("Untrusted itch image address");cancel.ThrowIfCancellationRequested();var request=(HttpWebRequest)WebRequest.Create(uri);request.AllowAutoRedirect=false;request.Timeout=request.ReadWriteTimeout=15000;request.UserAgent="Astera/0.1";
        using(cancel.Register(delegate{request.Abort();}))using(var response=(HttpWebResponse)request.GetResponse()){int code=(int)response.StatusCode;if(code>=300&&code<400){uri=new Uri(uri,response.Headers["Location"]);continue;}if(code!=200||response.ContentLength>8*1024*1024)throw new IOException("Unexpected itch image response");using(var input=response.GetResponseStream())using(var output=new MemoryStream()){byte[] buffer=new byte[16384];int n;while((n=input.Read(buffer,0,buffer.Length))>0){cancel.ThrowIfCancellationRequested();if(output.Length+n>8*1024*1024)throw new IOException("itch cover is too large");output.Write(buffer,0,n);}return output.ToArray();}}
    }throw new IOException("Too many itch image redirects");}
}
sealed partial class LibraryForm {
    readonly Dictionary<string,string> itchArt=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);readonly Dictionary<string,Image> itchIcons=new Dictionary<string,Image>(StringComparer.OrdinalIgnoreCase);readonly HashSet<string> itchPending=new HashSet<string>(StringComparer.OrdinalIgnoreCase);readonly Dictionary<string,DateTime> itchChecked=new Dictionary<string,DateTime>(StringComparer.OrdinalIgnoreCase);readonly SemaphoreSlim itchArtworkGate=new SemaphoreSlim(2);readonly CancellationTokenSource itchArtworkStop=new CancellationTokenSource();
    string AutomaticGameBackground(CustomGame game){string steam=AutomaticSteamBackground(game),cover;return !String.IsNullOrEmpty(steam)?steam:itchArt.TryGetValue(game.Executable,out cover)?cover:null;}
    Image AutomaticGameIcon(CustomGame game){Image steam=AutomaticSteamIcon(game),image;return steam??(itchIcons.TryGetValue(game.Executable,out image)?image:null);}
    void ApplyItchArtwork(CustomGame game,string path){string old;if(itchArt.TryGetValue(game.Executable,out old)&&old==path)return;if(String.IsNullOrEmpty(path))return;var image=ReadCustomIcon(path);Image prior;if(itchIcons.TryGetValue(game.Executable,out prior))prior.Dispose();itchIcons[game.Executable]=image;itchArt[game.Executable]=path;games.Invalidate();if(IsCustomGame&&CurrentCustom.Id==game.Id)RefreshCustomView();}
    void QueuePlatformArtwork(CustomGame game,bool force=false){QueueSteamArtwork(game,force);QueueItchArtwork(game);}
    async void QueueItchArtwork(CustomGame game){if(previewMode||game==null||game.Platform=="steam"||String.IsNullOrWhiteSpace(game.Executable))return;DateTime last;if(itchPending.Contains(game.Executable)||itchChecked.TryGetValue(game.Executable,out last)&&DateTime.UtcNow-last<TimeSpan.FromMinutes(5))return;itchPending.Add(game.Executable);itchChecked[game.Executable]=DateTime.UtcNow;bool acquired=false;try{await itchArtworkGate.WaitAsync(itchArtworkStop.Token);acquired=true;string image=await Task.Run(()=>ItchArtwork.Resolve(game,Path.Combine(store.Root,"artwork","itch"),itchArtworkStop.Token));if(!IsDisposed&&!Disposing)ApplyItchArtwork(game,image);}catch(OperationCanceledException){}catch(Exception error){if(!IsDisposed)AsteraDiagnostics.Write("itch cover unavailable",new IOException(error.GetType().Name));}finally{if(acquired)itchArtworkGate.Release();itchPending.Remove(game.Executable);}}
    void StopItchArtwork(){itchArtworkStop.Cancel();foreach(var image in itchIcons.Values)image.Dispose();itchIcons.Clear();}
}
}
