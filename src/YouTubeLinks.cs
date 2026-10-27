using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
namespace Astera {
static class YouTubeLinks {
    internal static string Id(string text){Uri uri;if(!Uri.TryCreate(text,UriKind.Absolute,out uri)||uri.Scheme!="https"||!uri.IsDefaultPort||!String.IsNullOrEmpty(uri.UserInfo))return null;string host=uri.Host.ToLowerInvariant(),id=null;var parts=uri.AbsolutePath.Trim('/').Split('/');if(host=="youtu.be"){if(parts.Length==1)id=parts[0];}else if(host=="youtube.com"||host=="www.youtube.com"||host=="m.youtube.com"||host=="www.youtube-nocookie.com"||host=="youtube-nocookie.com"){if(parts.Length==2&&(parts[0]=="embed"||parts[0]=="shorts"||parts[0]=="live"))id=parts[1];else if(uri.AbsolutePath=="/watch")foreach(string field in uri.Query.TrimStart('?').Split('&')){var pair=field.Split(new[]{'='},2);if(pair.Length==2&&pair[0]=="v")id=Uri.UnescapeDataString(pair[1]);}}return id!=null&&Regex.IsMatch(id,@"^[A-Za-z0-9_-]{11}$")?id:null;}
    internal static string[] Find(string text){return Regex.Matches(text??"",@"https://[^\s<>\[\]""']{1,2000}").Cast<Match>().Select(m=>Id(m.Value.TrimEnd('.',',',')',';','!'))).Where(id=>id!=null).Distinct().Take(4).ToArray();}
    internal static string SteamTags(string text){return Regex.Replace(text??"",@"\[previewyoutube=([A-Za-z0-9_-]{11})(?:;[^\]]*)?\].*?\[/previewyoutube\]",m=>Environment.NewLine+"https://www.youtube.com/watch?v="+m.Groups[1].Value+Environment.NewLine,RegexOptions.IgnoreCase|RegexOptions.Singleline);}
}
sealed partial class LibraryForm {
    FlowLayoutPanel updateVideos;string videoLinksKey="";
    void BuildUpdateVideos(Control notesHost){updateVideos=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=43,WrapContents=true,Visible=false};notesHost.Controls.Add(updateVideos);updateVideos.SendToBack();notes.TextChanged+=delegate{UpdateVideoLinks();};UpdateVideoLinks();}
    void UpdateVideoLinks(){if(updateVideos==null)return;var ids=YouTubeLinks.Find(notes.Text);string key=String.Join("|",ids);if(key==videoLinksKey)return;videoLinksKey=key;while(updateVideos.Controls.Count>0){var c=updateVideos.Controls[0];updateVideos.Controls.Remove(c);c.Dispose();}updateVideos.Height=ids.Length>2?82:43;for(int i=0;i<ids.Length;i++){string id=ids[i];var button=Button(ids.Length==1?"Watch video":"Video "+(i+1),delegate{YouTubePlayer.Open(this,store.Root,id);});button.Margin=new Padding(0,3,8,0);button.MinimumSize=new System.Drawing.Size(115,34);((AsteraButton)button).Symbol=ButtonSymbol.Play;updateVideos.Controls.Add(button);}updateVideos.Visible=ids.Length>0;}
}
}
