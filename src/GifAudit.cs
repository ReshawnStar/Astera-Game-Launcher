using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Windows.Forms;
namespace Astera {
sealed partial class LibraryForm {
    internal static int AuditGif(string root){bool effects=UiEffects.Enabled;try{
        if(Directory.Exists(root))throw new IOException("Use a new GIF audit directory");Directory.CreateDirectory(root);var checks=new List<string>();
        byte[] bytes=Convert.FromBase64String("R0lGODlhEAAQAIEAAP8AAAAAAAAAAAAAACH/C05FVFNDQVBFMi4wAwEAAAAh+QQACgAAACwAAAAAEAAQAAAIHQABCBxIsKDBgwgTKlzIsKHDhxAjSpxIsaLFgQEBACH5BAEKAAEALAAAAAAQABAAgQAA/wAAAAAAAAAAAAgdAAEIHEiwoMGDCBMqXMiwocOHECNKnEixosWBAQEAOw==");string gif=Path.Combine(root,"animated.gif");File.WriteAllBytes(gif,bytes);
        using(var asset=ArtworkAnimation.Open(gif)){AuditAssert(asset.FrameCount==2,"GIF lost its frames");using(var first=new Bitmap(asset.Image))AuditAssert(first.GetPixel(4,4).R>200,"First GIF frame was not red");AuditAssert(asset.AdvanceAt(100),"GIF did not advance at its frame delay");using(var second=new Bitmap(asset.Image))AuditAssert(second.GetPixel(4,4).B>200,"Second GIF frame was not blue");AuditAssert(asset.AdvanceAt(200),"Looping GIF did not restart");using(var again=new Bitmap(asset.Image))AuditAssert(again.GetPixel(4,4).R>200,"GIF loop returned the wrong frame");asset.SetPlaying(true);AuditAssert(asset.Playing,"GIF clock did not start");asset.SetPlaying(false);AuditAssert(!asset.Playing,"GIF clock did not pause");}
        using(var lockProbe=File.Open(gif,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){}checks.Add("GIF frame timing, looping, pause and stream ownership work without locking the original file");
        bool bad=false;try{using(var asset=ArtworkAnimation.FromBytes(new byte[]{1,2,3,4})) {}}catch(ArgumentException){bad=true;}AuditAssert(bad,"Invalid image content was accepted");checks.Add("Invalid GIF/image bytes fail safely before playback");
        string cached=Path.Combine(root,"cached.image");ItchArtwork.WriteCover(bytes,cached);using(var asset=ArtworkAnimation.Open(cached))AuditAssert(asset.Animated&&asset.FrameCount==2,"itch cache flattened the animation");checks.Add("itch cover cache preserves GIF frames independently of the filename extension");
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);var state=new Store(Path.Combine(root,"state"));state.Settings.CheckOnStart=false;state.Settings.KeepInTray=false;state.Settings.UseMinecraftSkin=false;var game=new CustomGame{Name="Animated artwork test",Executable=Path.Combine(root,"fixture.exe"),Background=gif,Character=gif,Icon=gif};File.WriteAllText(game.Executable,"Fixture only; never run");state.Settings.CustomGames.Add(game);UiEffects.Enabled=true;
        using(var form=new LibraryForm(state,new Channel(),true))using(var timer=new System.Windows.Forms.Timer{Interval=35}){
            form.TopMost=true;form.StartPosition=FormStartPosition.Manual;form.Location=Screen.PrimaryScreen.WorkingArea.Location;form.ReloadGames(game);form.CacheCustomIcon(game);var seen=new HashSet<int>();int ticks=0,stage=0,pauseFrame=0,code=1;ArtworkAnimation prior=null;
            timer.Tick+=delegate{try{
                if(++ticks>90)throw new IOException("GIF native playback timed out");
                var asset=form.banner.BackgroundAnimation;AuditAssert(asset!=null&&asset.Animated,"Banner did not retain animated artwork");
                if(stage==0){if(seen.Add(asset.CurrentFrame))form.CaptureVisibleUi(root,"gif-frame-"+asset.CurrentFrame);if(seen.Count<2)return;AuditAssert(form.banner.ArtworkPlayback&&form.AnimatedLibraryIcon(game)!=null&&form.iconAnimationClock.Enabled,"Visible background or icon animation was not playing");checks.Add("Real WinForms background, transparent character layer and visible library icon play animated GIF frames");UiEffects.Enabled=false;pauseFrame=asset.CurrentFrame;stage=1;return;}
                if(stage==1){AuditAssert(!form.banner.ArtworkPlayback&&!form.iconAnimationClock.Enabled&&asset.CurrentFrame==pauseFrame,"Disabling animated effects did not pause GIF playback");UiEffects.Enabled=true;form.Hide();AuditAssert(!form.banner.ArtworkPlayback&&!form.iconAnimationClock.Enabled,"Hiding Astera did not pause GIF timers");form.Show();form.WindowState=FormWindowState.Minimized;AuditAssert(!form.banner.ArtworkPlayback&&!form.iconAnimationClock.Enabled,"Minimizing Astera did not pause GIF timers");form.WindowState=FormWindowState.Normal;AuditAssert(form.banner.ArtworkPlayback&&form.iconAnimationClock.Enabled,"Restoring Astera did not resume GIF playback");checks.Add("Effects off, tray-style hide and minimization stop GIF timers; restoring resumes playback");prior=asset;game.Background="";game.Character="";form.ApplyItchArtwork(game,cached);form.UpdateCustomLayout();AuditAssert(prior.Image==null&&form.banner.BackgroundAnimation!=null&&form.banner.BackgroundAnimation.Animated,"Switching to an itch GIF did not release and replace prior artwork");checks.Add("Animated itch covers reach the banner through normal artwork selection and release the previous GIF");stage=2;return;}
                if(stage==2){var old=form.banner.BackgroundAnimation;form.GameKind=2;AuditAssert(old.Image==null&&!form.banner.ArtworkPlayback,"Changing games retained the previous banner animation");checks.Add("Switching to another game disposes the previous GIF and stops banner playback");File.WriteAllText(Path.Combine(root,"result.json"),MC2Pack.Engine.Json.Serialize(new{Passed=true,Checks=checks,GamesExecuted=false,LivePreferencesChanged=false}));code=0;timer.Stop();form.Close();}
            }catch(Exception error){timer.Stop();File.WriteAllText(Path.Combine(root,"failure.txt"),error.ToString());form.Close();}};
            form.Shown+=delegate{timer.Start();};Application.Run(form);return code;
        }
    }catch(Exception error){Directory.CreateDirectory(root);File.WriteAllText(Path.Combine(root,"failure.txt"),error.ToString());return 1;}finally{UiEffects.Enabled=effects;}}
}
}
