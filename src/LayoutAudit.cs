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
    static void AuditAssert(bool value,string message){if(!value)throw new Exception(message);}
    static void AuditButtons(Control parent){foreach(Control child in parent.Controls){if(child is AsteraButton&&child.Visible)AuditAssert(parent.ClientRectangle.Contains(child.Bounds),"Button clipped in "+parent.GetType().Name+": "+child.Text+" "+child.Bounds+" / "+parent.ClientRectangle);AuditButtons(child);}}
    static void Snapshot(Form form,string path){form.PerformLayout();Application.DoEvents();using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(0,0,form.Width,form.Height));bitmap.Save(path,ImageFormat.Png);}}
    public static int AuditLayout(string directory){var checks=new List<string>();try{
        directory=Path.GetFullPath(directory);if(Directory.Exists(directory))throw new Exception("Use a new isolated layout audit directory.");Directory.CreateDirectory(directory);
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        var store=new Store(Path.Combine(directory,"preferences"));store.Settings.CheckOnStart=false;store.Settings.KeepInTray=false;
        using(var form=new LibraryForm(store,new Channel(),true)){
            form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-30000,-30000);form.Show();
            var edgePoint=form.PointToScreen(new Point(1,1));long hit=(long)(ushort)edgePoint.X|((long)(ushort)edgePoint.Y<<16);AuditAssert(form.FormBorderStyle==FormBorderStyle.None&&SendMessage(form.Handle,0x84,IntPtr.Zero,new IntPtr(hit)).ToInt32()==13,"Borderless corner resizing did not expose its native hit target");checks.Add("Borderless frame exposes native corner-resize hit testing");
            foreach(var size in new[]{new Size(1400,900),new Size(1100,760)}){form.Size=size;form.PerformLayout();Application.DoEvents();AuditButtons(form);AuditAssert(!form.banner.Controls.OfType<TableLayoutPanel>().First().Bounds.IntersectsWith(form.banner.Controls.OfType<FlowLayoutPanel>().First().Bounds),"Hero text panel overlaps and natively clips the action buttons");Snapshot(form,Path.Combine(directory,"overview-"+size.Width+".png"));}checks.Add("Overview buttons fit default and minimum window widths");checks.Add("Hero text never overlaps the native action-button windows");
            form.Size=new Size(1400,900);form.SelectLibraryTab(1);Application.DoEvents();AuditAssert(form.activityCard.Visible&&!form.overviewRight.Visible,"Updates tab did not show activity");AuditButtons(form);Snapshot(form,Path.Combine(directory,"updates.png"));checks.Add("Updates tab shows live update activity");
            for(int cycle=0;cycle<12;cycle++){form.moreMenu.Show(form,new Point(10,10));form.moreMenu.Close();AuditAssert(!form.moreMenu.IsDisposed,"Overflow menu was disposed on close");}
            form.moreMenu.Items[2].PerformClick();AuditAssert(form.selectedTab==1,"Overflow action stopped working after reopen");checks.Add("Overflow menu survives twelve close/reopen cycles and action selection");
            form.SelectLibraryTab(2);Application.DoEvents();AuditAssert(form.backgroundPage.Visible&&!form.overviewPage.Visible,"Artwork tab did not switch");AuditButtons(form);Snapshot(form,Path.Combine(directory,"backgrounds.png"));checks.Add("Background management tab is reachable");
            form.SelectLibraryTab(3);Application.DoEvents();AuditAssert(form.preferencesPage.Visible,"Settings tab did not switch");AuditButtons(form);checks.Add("Settings page exposes the real settings dialog");
            form.games.SelectedIndex=1;AuditAssert(!form.check.Enabled&&!form.install.Enabled&&form.heroTitle.Text=="Star Dash","StarDash inherited Minecraft actions");checks.Add("StarDash keeps unconfigured update actions disabled");
            form.games.SelectedIndex=0;form.SelectLibraryTab(0);
            string background=Path.Combine(directory,"test-background.png"),character=Path.Combine(directory,"test-character.png");
            using(var image=new Bitmap(640,360)){using(var g=Graphics.FromImage(image))g.Clear(Color.DarkBlue);image.Save(background,ImageFormat.Png);}
            using(var image=new Bitmap(180,480)){using(var g=Graphics.FromImage(image)){g.Clear(Color.Transparent);g.FillRectangle(Brushes.Red,0,0,180,100);g.FillRectangle(Brushes.Cyan,30,100,120,380);}image.Save(character,ImageFormat.Png);}
            store.Settings.BackgroundMinecraft=background;store.Settings.CharacterMinecraft=character;store.Save();form.UpdateLayoutView();
            using(var image=new Bitmap(form.banner.Width,form.banner.Height)){form.banner.DrawToBitmap(image,form.banner.ClientRectangle);AuditAssert(image.GetPixel(form.banner.Width-65,10).R>220,"Character did not extend above the background");}checks.Add("Independent transparent character layer extends above scene");
            Snapshot(form,Path.Combine(directory,"layer-fixture.png"));var reloaded=new Store(store.Root);AuditAssert(reloaded.Settings.CharacterMinecraft==character&&reloaded.Settings.BackgroundMinecraft==background&&String.IsNullOrEmpty(reloaded.Settings.CharacterStarDash),"Per-game artwork preferences did not persist");checks.Add("Per-game artwork choices survive reload independently");
            form.games.SelectedIndex=1;AuditAssert(form.artLabel.Text.Contains("Default starfield"),"Minecraft artwork leaked into StarDash");form.games.SelectedIndex=0;
            form.notes.Text=new string('X',5000);AuditAssert(form.notes.Multiline&&form.notes.ScrollBars==ScrollBars.Vertical,"Long update notes are not scrollable");checks.Add("Long update notes retain a scrolling text area");
            form.notes.Text=String.Join(Environment.NewLine,Enumerable.Range(0,80).Select(i=>"Scrollable line "+i));Application.DoEvents();AuditAssert(form.notes.Maximum>0,"Textured scrollbar has no scroll range");form.notes.MoveTo(form.notes.Maximum);AuditAssert(form.notes.FirstLine>0,"Textured scrollbar failed to move text");form.notes.MoveTo(0);AuditAssert(form.notes.FirstLine==0,"Textured scrollbar failed to return to top");checks.Add("Textured scrollbar moves the native text viewport and returns to the top");
            form.Close();
        }
        AsteraDiagnostics.Initialize(Path.Combine(directory,"diagnostics"));AsteraDiagnostics.Write("Diagnostic fixture",new InvalidOperationException("outer fixture",new ArgumentException("inner fixture")));string diagnostic=File.ReadAllText(AsteraDiagnostics.SessionLog);AuditAssert(diagnostic.Contains("outer fixture")&&diagnostic.Contains("inner fixture")&&diagnostic.Contains("Astera "),"Exception details or build version were lost");checks.Add("Astera session log retains exception details, inner cause and build version");
        string fixture=Path.Combine(directory,"game-fixture"),crashes=Path.Combine(fixture,"crash-reports"),runtime=Path.Combine(fixture,"logs"),godot=Path.Combine(directory,"godot-fixture");Directory.CreateDirectory(crashes);Directory.CreateDirectory(runtime);Directory.CreateDirectory(godot);string crash=Path.Combine(crashes,"crash-fixture.txt");File.WriteAllText(crash,"unique game crash evidence");File.WriteAllText(Path.Combine(runtime,"latest.log"),"runtime fixture");File.WriteAllText(Path.Combine(godot,"godot.log"),"Godot fixture");string originalHash=Engine.Hash(crash);
        AuditAssert(GameDiagnostics.Collect(AsteraDiagnostics.Root,"minecraft",fixture,godot,false)==1,"Automatic crash collection missed report");AuditAssert(GameDiagnostics.Collect(AsteraDiagnostics.Root,"minecraft",fixture,godot,false)==0,"Unchanged game report was duplicated");AuditAssert(Engine.Hash(crash)==originalHash,"Original crash report was modified");checks.Add("Automatic Minecraft crash collection preserves originals and deduplicates");
        AuditAssert(GameDiagnostics.Collect(AsteraDiagnostics.Root,"minecraft",fixture,godot,true)==1&&GameDiagnostics.Collect(AsteraDiagnostics.Root,"stardash",fixture,godot,true)==1,"Explicit runtime log collection failed");checks.Add("Explicit collection includes Minecraft and StarDash runtime logs separately");
        File.WriteAllText(Path.Combine(directory,"result.json"),Engine.Json.Serialize(new{Passed=true,Checks=checks,LiveProfilesChanged=false,NetworkUsed=false}));return 0;
    }catch(Exception error){Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"failure.txt"),error.ToString());return 1;}}
}
}
