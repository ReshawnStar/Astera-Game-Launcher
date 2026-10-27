using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
namespace Astera {
static class PatchNotes {
    internal static string PlayerText(string text){text=(text??"").Replace("\r\n","\n");text=Regex.Replace(text,@"(?m)^#{1,6}\s+","");text=text.Replace("**","").Replace("`","");text=Regex.Replace(text,@"(?m)^\*([^*]+)\*$","$1");return UiText.WindowsLines(text);}
    internal static string Minecraft(UpdateFeed feed){bool match=feed==null||feed.Release.Files.Any(f=>f.Id=="stars_general_fixes"&&f.Version.Split('+')[0]=="1.7.205");if(match)using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Astera.Notes.Minecraft205"))if(stream!=null)using(var r=new StreamReader(stream))return PlayerText(r.ReadToEnd());return PlayerText(feed==null?"No patch notes available yet.":feed.Notes);}
    internal const string FtlPlaceholder="FTL: Multiverse — What's New\n\nNo patch notes available yet.\n\nNew content, balance changes, improvements, and bug fixes will appear here as updates are released.";
}
}
