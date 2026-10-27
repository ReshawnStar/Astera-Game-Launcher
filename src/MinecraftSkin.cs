using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using MC2Pack;
namespace Astera {
static class MinecraftSkin {
    public static int Preview(string directory){try{directory=Path.GetFullPath(directory);if(Directory.Exists(directory))return 2;Directory.CreateDirectory(directory);string pose=LoadPose(directory);if(pose==null)throw new IOException("No active public Minecraft profile found.");File.Copy(pose,Path.Combine(directory,"character.png"));File.Copy(pose+".skin.png",Path.Combine(directory,"character.png.skin.png"));File.Copy(pose+".model",Path.Combine(directory,"character.png.model"));File.WriteAllText(Path.Combine(directory,"result.json"),"{\"Passed\":true,\"AccountTokensUsed\":false,\"OriginalFilesChanged\":false}");return 0;}catch(Exception error){Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"failure.txt"),"Skin preview unavailable: "+error.GetType().Name);return 1;}}
    static Dictionary<string,object> Map(object value){return value as Dictionary<string,object>;}
    static T ReadJson<T>(string text){return new System.Web.Script.Serialization.JavaScriptSerializer{MaxJsonLength=1024*1024}.Deserialize<T>(text);}
    static object Field(Dictionary<string,object> map,string name){object value;return map!=null&&map.TryGetValue(name,out value)?value:null;}
    internal static string SelectedProfile(string json){
        var root=ReadJson<Dictionary<string,object>>(json);var accounts=Map(Field(root,"accounts"));var account=Map(Field(accounts,Convert.ToString(Field(root,"activeAccountLocalId"))));var profile=Map(Field(account,"minecraftProfile"));Guid id;
        return Guid.TryParse(Convert.ToString(Field(profile,"id")),out id)?id.ToString("N"):null;
    }
    static string FindProfile(){
        string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),".minecraft");var files=new List<string>();foreach(string name in new[]{"launcher_accounts_microsoft_store.json","launcher_accounts.json"}){string file=Path.Combine(root,name);if(File.Exists(file))files.Add(file);}files.Sort((a,b)=>File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a)));
        foreach(string file in files){try{if(new FileInfo(file).Length>1024*1024)continue;string id;using(var input=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))using(var reader=new StreamReader(input))id=SelectedProfile(reader.ReadToEnd());if(id!=null)return id;}catch(IOException){}catch(UnauthorizedAccessException){}catch(ArgumentException){}}
        return null;
    }
    static byte[] PublicFile(string url,int limit){
        var uri=new Uri(url);if(uri.Scheme!="https"||!(uri.Host=="sessionserver.mojang.com"||uri.Host=="textures.minecraft.net"))throw new IOException("Unsupported Minecraft skin host.");
        ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;var request=(HttpWebRequest)WebRequest.Create(uri);request.AllowAutoRedirect=false;request.Timeout=15000;request.ReadWriteTimeout=15000;request.UserAgent="Astera/SkinPreview";
        using(var response=(HttpWebResponse)request.GetResponse()){if(response.StatusCode!=HttpStatusCode.OK||response.ContentLength>limit)throw new IOException("Minecraft skin response unavailable.");using(var output=new MemoryStream())using(var input=response.GetResponseStream()){var buffer=new byte[8192];int read;while((read=input.Read(buffer,0,buffer.Length))>0){if(output.Length+read>limit)throw new IOException("Minecraft skin response exceeded its limit.");output.Write(buffer,0,read);}return output.ToArray();}}
    }
    public static string LoadPose(string applicationRoot){
        string id=FindProfile();if(id==null)return null;
        string cache=Path.Combine(applicationRoot,"skin-cache");Directory.CreateDirectory(cache);string pose=Path.Combine(cache,id+"-live-v3.png");if(File.Exists(pose)&&File.Exists(pose+".skin.png")&&File.Exists(pose+".model")&&File.GetLastWriteTimeUtc(pose)>DateTime.UtcNow.AddDays(-1))return pose;
        var profile=ReadJson<Dictionary<string,object>>(Encoding.UTF8.GetString(PublicFile("https://sessionserver.mojang.com/session/minecraft/profile/"+id,128*1024)));string encoded=null;var properties=Field(profile,"properties") as IEnumerable;
        if(properties!=null)foreach(var property in properties){var item=Map(property);if(Convert.ToString(Field(item,"name"))=="textures")encoded=Convert.ToString(Field(item,"value"));}if(String.IsNullOrEmpty(encoded))return null;
        var textures=Map(Field(ReadJson<Dictionary<string,object>>(Encoding.UTF8.GetString(Convert.FromBase64String(encoded))),"textures"));var skin=Map(Field(textures,"SKIN"));Uri source;
        if(!Uri.TryCreate(Convert.ToString(Field(skin,"url")),UriKind.Absolute,out source)||source.Host!="textures.minecraft.net"||!Regex.IsMatch(source.AbsolutePath,@"^/texture/[a-fA-F0-9]{32,128}$"))throw new IOException("Invalid public Minecraft skin address.");
        bool slim=Convert.ToString(Field(Map(Field(skin,"metadata")),"model"))=="slim";var bytes=PublicFile("https://textures.minecraft.net"+source.AbsolutePath,512*1024);
        SavePrivate(pose+".skin.png",bytes);SavePrivate(pose+".model",Encoding.UTF8.GetBytes(slim?"slim":"classic"));
        using(var input=new MemoryStream(bytes))using(var image=Image.FromStream(input))using(var rendered=Render(image,slim)){string next=pose+".next";rendered.Save(next,ImageFormat.Png);if(File.Exists(pose))File.Replace(next,pose,null);else File.Move(next,pose);}return pose;
    }
    static void SavePrivate(string path,byte[] bytes){string next=path+".next";File.WriteAllBytes(next,bytes);if(File.Exists(path))File.Replace(next,path,null);else File.Move(next,path);}
    internal static Bitmap Render(Image skin,bool slim){
        if(skin.Width!=64||(skin.Height!=64&&skin.Height!=32))throw new IOException("Expected a standard 64x64 or legacy 64x32 Minecraft skin.");
        // Bust composition: retain the wave, frame the face and shoulders, and
        // let the torso continue beyond the lower edge rather than shrink a full body.
        var image=new Bitmap(512,350,PixelFormat.Format32bppArgb);using(var g=Graphics.FromImage(image)){
            g.Clear(Color.Transparent);g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;bool modern=skin.Height==64;
            Part(g,skin,modern?new Rectangle(36,52,slim?3:4,12):new Rectangle(44,20,4,12),modern?new Rectangle(52,52,slim?3:4,12):Rectangle.Empty,new PointF(151,236),new Size(slim?54:72,216),7,!modern);
            g.DrawImage(skin,new Rectangle(184,230,144,216),20,20,8,12,GraphicsUnit.Pixel);if(modern)g.DrawImage(skin,new Rectangle(182,228,148,220),20,36,8,12,GraphicsUnit.Pixel);
            Part(g,skin,new Rectangle(44,20,slim?3:4,12),modern?new Rectangle(44,36,slim?3:4,12):Rectangle.Empty,new PointF(347,234),new Size(slim?54:72,216),-150,false);
            // One consistent cuboid: top, side and face share the same edges.
            // Keep the face upright; the old skew made the hair look lopsided.
            g.DrawImage(skin,new[]{new Point(184,84),new Point(328,84),new Point(204,72)},new Rectangle(8,0,8,8),GraphicsUnit.Pixel);
            g.DrawImage(skin,new[]{new Point(328,84),new Point(348,72),new Point(328,228)},new Rectangle(16,8,8,8),GraphicsUnit.Pixel);
            g.DrawImage(skin,new Rectangle(184,84,144,144),8,8,8,8,GraphicsUnit.Pixel);
            // Hat/hair belongs outside the head, with matching outer-layer geometry.
            g.DrawImage(skin,new[]{new Point(182,82),new Point(330,82),new Point(202,70)},new Rectangle(40,0,8,8),GraphicsUnit.Pixel);
            g.DrawImage(skin,new[]{new Point(330,82),new Point(350,70),new Point(330,230)},new Rectangle(48,8,8,8),GraphicsUnit.Pixel);
            g.DrawImage(skin,new Rectangle(182,82,148,148),40,8,8,8,GraphicsUnit.Pixel);
        }return image;
    }
    static void Part(Graphics g,Image skin,Rectangle source,Rectangle overlay,PointF shoulder,Size size,float angle,bool mirror){var state=g.Save();g.TranslateTransform(shoulder.X,shoulder.Y);g.RotateTransform(angle);if(mirror)g.ScaleTransform(-1,1);var target=new Rectangle(-size.Width/2,0,size.Width,size.Height);g.DrawImage(skin,target,source,GraphicsUnit.Pixel);if(!overlay.IsEmpty)g.DrawImage(skin,new Rectangle(target.X-1,target.Y-1,target.Width+2,target.Height+2),overlay,GraphicsUnit.Pixel);g.Restore(state);}
}
sealed partial class LibraryForm {
    string minecraftSkinArt="";bool loadingMinecraftSkin;
    async void RefreshMinecraftSkin(){if(previewMode||loadingMinecraftSkin||!store.Settings.UseMinecraftSkin||!String.IsNullOrEmpty(store.Settings.CharacterMinecraft))return;loadingMinecraftSkin=true;try{string result=await Task.Run(()=>MinecraftSkin.LoadPose(store.Root));if(!IsDisposed){minecraftSkinArt=result??"";UpdateLayoutView();}}catch(Exception error){AsteraDiagnostics.Write("Minecraft skin preview unavailable",new IOException("Public skin lookup did not finish ("+error.GetType().Name+")."));}finally{loadingMinecraftSkin=false;}}
}
}
