using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
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
            foreach(var size in new[]{new Size(1400,900),new Size(1100,760)}){form.Size=size;form.PerformLayout();Application.DoEvents();AuditButtons(form);Snapshot(form,Path.Combine(directory,"overview-"+size.Width+".png"));}checks.Add("Overview buttons fit default and minimum window widths");
            form.Size=new Size(1400,900);form.SelectLibraryTab(1);Application.DoEvents();AuditAssert(form.activityCard.Visible&&!form.overviewRight.Visible,"Updates tab did not show activity");AuditButtons(form);Snapshot(form,Path.Combine(directory,"updates.png"));checks.Add("Updates tab shows live update activity");
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
            form.Close();
        }
        File.WriteAllText(Path.Combine(directory,"result.json"),Engine.Json.Serialize(new{Passed=true,Checks=checks,LiveProfilesChanged=false,NetworkUsed=false}));return 0;
    }catch(Exception error){Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"failure.txt"),error.ToString());return 1;}}
}
}
