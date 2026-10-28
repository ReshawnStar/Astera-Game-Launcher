using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
namespace Astera {
sealed partial class LibraryForm {
    internal static void AuditCoveredPreparation(string root,List<string> checks){
        Directory.CreateDirectory(root);AsteraDiagnostics.Initialize(root);var store=new Store(Path.Combine(root,"state"));store.Settings.CheckOnStart=false;store.Settings.KeepInTray=false;store.Settings.UseMinecraftSkin=false;store.Settings.AnimatedEffects=true;
        var game=new CustomGame{Name="Prepared page fixture",Executable=Path.Combine(root,"fixture.exe")};store.Settings.CustomGames.Add(game);
        using(var form=new LibraryForm(store,new Channel(),true)){
            form.StartPosition=FormStartPosition.Manual;form.Location=Screen.PrimaryScreen.WorkingArea.Location;form.TopMost=true;form.Show();MotionAudit.Pump(80);
            form.steamArtPending.Add(game.Executable);form.ReloadGames(game);var cover=form.gameTransition;AuditAssert(cover!=null,"Visible selection did not install an opaque transition sibling");
            MotionAudit.Pump(90);form.CaptureVisibleUi(root,"01-fade-out");MotionAudit.Pump(1150);
            AuditAssert(form.gameTransition==cover&&cover.Preparing,"Page was exposed while its selected artwork request was pending beyond one second");AuditAssert(form.heroTitle.Text==game.Name,"New page was not assembled behind the cover");
            using(var sample=CaptureLibrary(cover,new Rectangle(30,30,80,40))){for(int y=0;y<sample.Height;y+=4)for(int x=0;x<sample.Width;x+=4)AuditAssert(sample.GetPixel(x,y).ToArgb()==form.gameViewport.BackColor.ToArgb(),"An unfinished child control painted through the loading cover");}
            form.CaptureVisibleUi(root,"02-pending-covered");
            // Simulate completion of both image and notes work before the final capture.
            var png=Path.Combine(root,"prepared.png");using(var bitmap=new Bitmap(500,260)){using(var g=Graphics.FromImage(bitmap))g.Clear(Color.FromArgb(18,72,102));bitmap.Save(png);}game.Background=png;form.RefreshView();form.notes.Text="Prepared notes: no partial text should appear.";form.steamArtPending.Remove(game.Executable);
            var waiting=System.Diagnostics.Stopwatch.StartNew();while(form.gameTransition==cover&&cover.Preparing&&waiting.ElapsedMilliseconds<5000)MotionAudit.Pump(5);AuditAssert(cover.RevealStarted,"Ready page did not begin its entrance");form.CaptureVisibleUi(root,"03-prepared-entrance");MotionAudit.Pump(500);AuditAssert(form.gameTransition==null&&form.heroTitle.Text==game.Name&&form.notes.Text.StartsWith("Prepared notes:"),"Completed entrance did not reveal the prepared content");form.CaptureVisibleUi(root,"04-complete");
            File.WriteAllText(Path.Combine(root,"native-paint-timing.json"),MC2Pack.Engine.Json.Serialize(new{Paints=cover.PaintCount,MeanPaintMilliseconds=cover.PaintCount==0?0:cover.PaintTicks*1000.0/System.Diagnostics.Stopwatch.Frequency/cover.PaintCount,MaximumPaintMilliseconds=cover.MaxPaintTicks*1000.0/System.Diagnostics.Stopwatch.Frequency}));checks.Add("Actual page remains fully covered beyond one second while selected resources are pending; prepared artwork and notes are captured before entrance");
            // Rapid A/B selection: the cancelled surface cannot finish over a newer one.
            form.GameKind=1;var old=form.gameTransition;form.ReloadGames(game);var newest=form.gameTransition;AuditAssert(newest!=old&&old.IsDisposed,"Rapid selection retained the superseded transition");MotionAudit.Pump(1700);AuditAssert(form.gameTransition==null&&form.heroTitle.Text==game.Name,"Superseded selection exposed the wrong game");checks.Add("Rapid selection disposes the old transition and reveals only the latest page");
            form.SwitchGame();MotionAudit.Pump(400);form.Size=new Size(1180,780);MotionAudit.Pump(1400);AuditAssert(form.gameTransition==null&&form.heroTitle.Text==game.Name,"Resize left the page covered or stale");checks.Add("Resize restages the prepared surface instead of exposing partially laid out controls");
            using(var oldBitmap=new Bitmap(96,64))using(var newBitmap=new Bitmap(96,64)){
                using(var g=Graphics.FromImage(oldBitmap))g.Clear(Color.Red);using(var g=Graphics.FromImage(newBitmap)){g.Clear(Color.Blue);using(var pen=new Pen(Color.White))for(int x=0;x<96;x+=2)g.DrawLine(pen,x,0,x,63);}
                using(var surface=new GameTransition(new Bitmap(oldBitmap),null)){surface.Start(new Bitmap(newBitmap));MotionAudit.Pump(1100);using(var frame=new Bitmap(96,112)){using(var g=Graphics.FromImage(frame))surface.DrawSurface(g,1,.5,1);for(int y=0;y<frame.Height;y++)for(int x=0;x<frame.Width;x++){var color=frame.GetPixel(x,y);AuditAssert(color.R<220||color.G>0,"Old red page bled into the new blue entrance");}using(var g=Graphics.FromImage(frame))surface.DrawSurface(g,1,1,1);for(int y=0;y<64;y++)for(int x=0;x<96;x++)AuditAssert(frame.GetPixel(x,y).ToArgb()==newBitmap.GetPixel(x,y).ToArgb(),"Final prepared surface was resampled or blurred");frame.Save(Path.Combine(root,"pixel-sharp-complete.png"));}}
            }checks.Add("Entrance composites only against a solid cover; final pixels exactly match the prepared surface without resampling");form.Close();
        }
    }
}
}