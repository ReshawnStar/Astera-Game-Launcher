using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using MC2Pack;
namespace Astera {
sealed partial class LibraryForm {
    static IEnumerable<Control> Children(Control root){foreach(Control child in root.Controls){yield return child;foreach(var nested in Children(child))yield return nested;}}
    static void CaptureOwnedRectangle(Rectangle rectangle,string path){using(var image=new Bitmap(rectangle.Width,rectangle.Height))using(var graphics=Graphics.FromImage(image)){graphics.CopyFromScreen(rectangle.Location,Point.Empty,rectangle.Size,CopyPixelOperation.SourceCopy);image.Save(path,ImageFormat.Png);}}
    public static int AuditDialogs(string directory){if(Directory.Exists(directory))return 2;Directory.CreateDirectory(directory);Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);var state=new Store(Path.Combine(directory,"preferences"));state.Settings.CheckOnStart=false;state.Settings.KeepInTray=false;state.Settings.Profile="C:/Astera-fixture-unavailable";state.Settings.StarDashExecutable="C:/Astera-fixture-unavailable/StarDash.exe";using(var form=new LibraryForm(state,new Channel(),true))using(var timer=new System.Windows.Forms.Timer{Interval=650}){int step=0;Exception failed=null;SettingsForm dialog=null;form.TopMost=true;form.StartPosition=FormStartPosition.CenterScreen;Action<int> show=delegate(int scope){dialog=new SettingsForm(state.Settings,scope);dialog.TopMost=true;dialog.StartPosition=FormStartPosition.CenterScreen;dialog.Show(form);dialog.Activate();};timer.Tick+=delegate{try{switch(step++){
        case 0:show(-1);break;
        case 1:AuditAssert(dialog.FormBorderStyle==FormBorderStyle.None&&dialog.Icon!=null&&!Children(dialog).OfType<TextBox>().Any(),"Launcher settings leaked game fields or retained Windows chrome");AuditButtons(dialog);CaptureOwnedRectangle(dialog.Bounds,Path.Combine(directory,"launcher-settings.png"));Children(dialog).OfType<Button>().Single(b=>b.Text=="Save").PerformClick();AuditAssert(dialog.Result!=null&&dialog.Result.Profile==state.Settings.Profile&&dialog.Result.StarDashExecutable==state.Settings.StarDashExecutable,"Global Save validated/changed unrelated game paths");dialog.Dispose();dialog=null;show(0);break;
        case 2:AuditAssert(Children(dialog).OfType<TextBox>().Count()==2,"Minecraft settings did not contain only its profile and channel fields");AuditButtons(dialog);CaptureOwnedRectangle(dialog.Bounds,Path.Combine(directory,"minecraft-settings.png"));dialog.Close();dialog.Dispose();dialog=null;show(1);break;
        case 3:AuditAssert(Children(dialog).OfType<TextBox>().Count()==1,"StarDash settings leaked Minecraft fields");AuditButtons(dialog);CaptureOwnedRectangle(dialog.Bounds,Path.Combine(directory,"stardash-settings.png"));dialog.Close();dialog.Dispose();dialog=null;form.GameKind=2;form.SelectLibraryTab(3);break;
        case 4:AuditAssert(!form.gameSettingsDescription.Text.Contains("CurseForge")&&!form.gameSettingsDescription.Text.Contains("StarDash"),"FTL settings page still describes unrelated games");form.CaptureVisibleUi(directory,"ftl-game-settings");form.moreMenu.Show(form,new Point(300,160));break;
        case 5:CaptureOwnedRectangle(form.moreMenu.Bounds,Path.Combine(directory,"overflow-menu.png"));form.moreMenu.Close();form.GameKind=0;form.SelectLibraryTab(0);form.busy=true;form.cancellation=new System.Threading.CancellationTokenSource();form.RefreshView();form.SetWorkProgress("Downloading",308L*1048576,616L*1048576);break;
        case 6:AuditAssert(form.cancel.Visible&&!form.download.Visible,"Cancel did not replace Download");form.CaptureVisibleUi(directory,"cancel-slot");form.busy=false;form.cancellation.Dispose();form.cancellation=null;form.RefreshView();File.WriteAllText(Path.Combine(directory,"result.json"),Engine.Json.Serialize(new{Passed=true,Frames=6,LiveSettingsChanged=false,GlobalFieldsSeparated=true,OwnedDialogsBorderless=true}));timer.Stop();form.Close();break;
    }}catch(Exception error){failed=error;timer.Stop();if(dialog!=null)dialog.Dispose();form.busy=false;if(form.cancellation!=null){form.cancellation.Dispose();form.cancellation=null;}File.WriteAllText(Path.Combine(directory,"failure.txt"),error.ToString());form.Close();}};form.Shown+=delegate{timer.Start();};Application.Run(form);return failed==null?0:1;}}
}
}
