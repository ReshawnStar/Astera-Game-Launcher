using System;
using System.IO;
using System.IO.Compression;
using System.Text;
namespace Astera {
static class ItchMetadata {
    internal static ItchReceiptGame Read(string folder){try{
        string gzip=Path.Combine(folder,".itch","receipt.json.gz"),plain=Path.Combine(folder,".itch","receipt.json"),json;
        if(File.Exists(gzip)){if(new FileInfo(gzip).Length>512*1024)return null;using(var source=File.OpenRead(gzip))using(var input=new GZipStream(source,CompressionMode.Decompress))using(var output=new MemoryStream()){byte[] block=new byte[4096];int n;while((n=input.Read(block,0,block.Length))>0){if(output.Length+n>1024*1024)return null;output.Write(block,0,n);}json=Encoding.UTF8.GetString(output.ToArray());}}
        else if(File.Exists(plain)&&new FileInfo(plain).Length<=1024*1024)json=File.ReadAllText(plain);else return null;
        var receipt=new System.Web.Script.Serialization.JavaScriptSerializer{MaxJsonLength=1024*1024}.Deserialize<ItchReceipt>(json);var game=receipt==null?null:receipt.game;if(game==null||game.id<=0||!PlatformLibraries.Id(game.id.ToString()))return null;
        if(String.IsNullOrWhiteSpace(game.title))game.title=Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar));return game;
    }catch(IOException){return null;}catch(UnauthorizedAccessException){return null;}catch(ArgumentException){return null;}catch(InvalidOperationException){return null;}}
    internal static ItchReceiptGame Find(CustomGame game){if(game.Platform=="steam"||game.Platform=="roblox")return null;string folder=String.IsNullOrEmpty(game.Platform)?Path.GetDirectoryName(game.Executable):game.Executable;for(int i=0;i<7&&!String.IsNullOrEmpty(folder);i++){var found=Read(folder);if(found!=null)return game.Platform=="itch"&&found.id.ToString()!=game.PlatformId?null:found;folder=Path.GetDirectoryName(folder.TrimEnd(Path.DirectorySeparatorChar));}return null;}
    internal static string Cover(ItchReceiptGame game,bool preferStill=false){if(game==null)return null;foreach(string url in (preferStill?new[]{game.stillCoverUrl,game.still_cover_url,game.coverUrl,game.cover_url}:new[]{game.coverUrl,game.cover_url,game.stillCoverUrl,game.still_cover_url}))if(ItchArtwork.Address(url))return url;return null;}
}
}
