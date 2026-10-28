using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
namespace Astera {
public sealed class ItchUpload {public ItchLaunchTarget[] launchTargets;}
public sealed class ItchLaunchTarget {public string path,flavor,arch;}
static class ItchLaunch {
    static bool Helper(string path){string name=Path.GetFileNameWithoutExtension(path).ToLowerInvariant();return name.Contains("crashhandler")||name.StartsWith("unins")||name=="python"||name=="pythonw"||name=="updater"||name=="setup"||name=="install"||name=="uninstall"||name=="dxsetup"||name.StartsWith("vcredist")||name.StartsWith("vc_redist")||name=="notification_helper"||name=="cef_helper"||name=="unitycrashhandler64";}
    internal static string SafeExecutable(string root,string relative){try{return ValidateExecutable(root,relative);}catch(ArgumentException){return null;}catch(IOException){return null;}catch(UnauthorizedAccessException){return null;}catch(NotSupportedException){return null;}}
    static string ValidateExecutable(string root,string relative){
        if(String.IsNullOrWhiteSpace(relative)||Path.IsPathRooted(relative)||relative.Contains(":")||relative.Split('/','\\').Any(p=>p==".."))return null;
        root=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);string full=Path.GetFullPath(Path.Combine(root,relative.Replace('/',Path.DirectorySeparatorChar)));
        if(!full.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||!full.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)||!File.Exists(full)||Helper(full))return null;
        for(string part=full;part!=null&&part.Length>=root.Length;part=Path.GetDirectoryName(part))if((File.GetAttributes(part)&FileAttributes.ReparsePoint)!=0)return null;
        try{return CustomGames.Validate(full);}catch(IOException){return null;}catch(UnauthorizedAccessException){return null;}
    }
    internal static string[] Candidates(CustomGame game){
        var receipt=ItchMetadata.ReadReceipt(game.Executable);if(receipt==null||receipt.game==null||receipt.game.id.ToString()!=game.PlatformId)throw new IOException("This folder does not contain the matching itch.io installation. Choose its game folder again.");
        // itch owns manifest actions, prerequisites, sandbox/API launch settings.
        // Do not silently discard those options by guessing a raw executable.
        if(File.Exists(Path.Combine(game.Executable,".itch.toml")))return new string[0];
        var targets=receipt.upload==null?null:receipt.upload.launchTargets;
        var paths=targets!=null&&targets.Length>0?targets.Where(t=>t!=null&&t.flavor=="windows"&&(String.IsNullOrEmpty(t.arch)||t.arch=="amd64"||t.arch=="386"||t.arch=="x86")).Select(t=>t.path):receipt.files??new string[0];
        return paths.Take(20000).Select(p=>SafeExecutable(game.Executable,p)).Where(p=>p!=null).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p=>p,StringComparer.OrdinalIgnoreCase).ToArray();
    }
    internal static ProcessStartInfo Start(CustomGame game,string executable){string full=SafeExecutable(game.Executable,Relative(game.Executable,executable));if(full==null)throw new IOException("The selected itch.io game executable is no longer available. Choose the game again.");return new ProcessStartInfo(full){UseShellExecute=true,WorkingDirectory=Path.GetDirectoryName(full)};}
    internal static string Relative(string root,string executable){root=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)+Path.DirectorySeparatorChar;executable=Path.GetFullPath(executable);if(!executable.StartsWith(root,StringComparison.OrdinalIgnoreCase))throw new IOException("The launch target must be inside this game's installation.");return executable.Substring(root.Length);}
}
sealed partial class LibraryForm {
    ProcessStartInfo ItchStart(CustomGame game){
        var candidates=ItchLaunch.Candidates(game);if(candidates.Length==0)return PlatformLibraries.Start(game);
        string chosen=ItchLaunch.SafeExecutable(game.Executable,game.ItchLaunchTarget);
        if(chosen==null||!candidates.Contains(chosen,StringComparer.OrdinalIgnoreCase))chosen=candidates.Length==1?candidates[0]:ChooseItchExecutable(game,candidates);
        if(chosen==null)return null;string relative=ItchLaunch.Relative(game.Executable,chosen);if(game.ItchLaunchTarget!=relative){string old=game.ItchLaunchTarget;game.ItchLaunchTarget=relative;try{store.Save();}catch{game.ItchLaunchTarget=old;throw;}}
        return ItchLaunch.Start(game,chosen);
    }
    string ChooseItchExecutable(CustomGame game,string[] candidates){using(var dialog=new AsteraDialog{Text="Choose how to launch "+game.Name,Size=new Size(640,390),StartPosition=FormStartPosition.CenterParent}){var label=new Label{Text="This installation has several game applications. Astera will remember your choice.",Dock=DockStyle.Top,Height=66,Padding=new Padding(16,16,16,0)};var list=new ListBox{Dock=DockStyle.Fill,BackColor=Base,ForeColor=Color.White,BorderStyle=BorderStyle.None};foreach(var path in candidates)list.Items.Add(ItchLaunch.Relative(game.Executable,path));if(list.Items.Count>0)list.SelectedIndex=0;var footer=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=60,Padding=new Padding(14)};var playChoice=Button("Play",delegate{if(list.SelectedIndex>=0)dialog.DialogResult=DialogResult.OK;});var cancelChoice=Button("Cancel",delegate{dialog.DialogResult=DialogResult.Cancel;});footer.Controls.Add(playChoice);footer.Controls.Add(cancelChoice);dialog.Controls.Add(list);dialog.Controls.Add(label);dialog.Controls.Add(footer);dialog.AcceptButton=playChoice;dialog.CancelButton=cancelChoice;return dialog.ShowDialog(this)==DialogResult.OK?candidates[list.SelectedIndex]:null;}}
}
}
