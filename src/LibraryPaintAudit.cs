using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using MC2Pack;
namespace Astera {
sealed partial class LibraryForm {
    [DllImport("user32.dll",EntryPoint="SendMessageW")]static extern IntPtr AuditListMessage(IntPtr window,int message,IntPtr w,IntPtr l);
    static Bitmap CaptureLibrary(Control control,Rectangle region){var bitmap=new Bitmap(region.Width,region.Height);using(var g=Graphics.FromImage(bitmap))g.CopyFromScreen(control.PointToScreen(region.Location),Point.Empty,region.Size);return bitmap;}
    static long PixelStamp(Bitmap bitmap){unchecked{long value=17;for(int y=0;y<bitmap.Height;y++)for(int x=0;x<bitmap.Width;x++)value=value*31+bitmap.GetPixel(x,y).ToArgb();return value;}}
    internal static int AuditLibraryPaint(string root){bool effects=UiEffects.Enabled;try{
        if(Directory.Exists(root))throw new IOException("Use a fresh sidebar audit directory");Directory.CreateDirectory(root);var checks=new List<string>();
        var bytes=Convert.FromBase64String("R0lGODlhEAAQAIEAAP8AAAAAAAAAAAAAACH/C05FVFNDQVBFMi4wAwEAAAAh+QQACgAAACwAAAAAEAAQAAAIHQABCBxIsKDBgwgTKlzIsKHDhxAjSpxIsaLFgQEBACH5BAEKAAEALAAAAAAQABAAgQAA/wAAAAAAAAAAAAgdAAEIHEiwoMGDCBMqXMiwocOHECNKnEixosWBAQEAOw==");var gif=Path.Combine(root,"animated.gif");File.WriteAllBytes(gif,bytes);var png=Path.Combine(root,"still.png");using(var b=new Bitmap(16,16)){using(var g=Graphics.FromImage(b))g.Clear(Color.Lime);b.Save(png);}
        using(var asset=ArtworkAnimation.Open(gif)){AuditAssert(asset.DelayAt(0)==100&&asset.DelayAt(75)==25&&asset.DelayAt(100)==100,"GIF next-frame deadline is incorrect");AuditAssert(!asset.AdvanceAt(75)&&asset.AdvanceAt(100)&&!asset.AdvanceAt(150),"GIF changed between its frame deadlines");}checks.Add("GIF deadlines wait for the recorded frame time instead of polling every 15 milliseconds");
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);var state=new Store(Path.Combine(root,"state"));state.Settings.CheckOnStart=false;state.Settings.KeepInTray=false;state.Settings.UseMinecraftSkin=false;
        var game=new CustomGame{Name="00 Animated icon",Executable=Path.Combine(root,"not-a-game.exe"),Icon=gif};state.Settings.CustomGames.Add(game);state.Settings.CustomGames.Add(new CustomGame{Name="01 Still icon",Executable=game.Executable,Icon=png});for(int i=2;i<35;i++)state.Settings.CustomGames.Add(new CustomGame{Name=i.ToString("00")+" Static row",Executable=game.Executable});
        using(var form=new LibraryForm(state,new Channel(),true))using(var other=new Form{Text="Astera focus fixture",Size=new Size(180,90),ShowInTaskbar=false,StartPosition=FormStartPosition.Manual})using(var tick=new Timer{Interval=20}){
            form.TopMost=true;form.StartPosition=FormStartPosition.Manual;form.Location=Screen.PrimaryScreen.WorkingArea.Location;form.ReloadGames(game);form.CacheCustomIcon(game);foreach(var entry in state.Settings.CustomGames)form.CacheCustomIcon(entry);
            int count=0,code=1,iconPaints=0,widePaints=0,animatedPaints=0,animatedWidePaints=0;long? textStamp=null,stillStamp=null;var colors=new HashSet<int>();bool recording=false;var sampleClock=new System.Diagnostics.Stopwatch();
            form.games.FramePresented=delegate(Rectangle bounds){if(recording){if(bounds.Width<=46&&bounds.Height<=46)iconPaints++;else widePaints++;}};
            tick.Tick+=delegate{try{
                count++;if(count==10){sampleClock.Start();recording=true;form.games.Update();}
                if(count>=10&&count<=90){AuditListMessage(form.games.Handle,0x200,IntPtr.Zero,new IntPtr((30<<16)|30));if(count%8==0){AuditListMessage(form.games.Handle,0x2A1,IntPtr.Zero,IntPtr.Zero);AuditListMessage(form.games.Handle,0x2A3,IntPtr.Zero,IntPtr.Zero);}
                    using(var text=CaptureLibrary(form.games,new Rectangle(64,9,120,50))){long stamp=PixelStamp(text);if(textStamp.HasValue)AuditAssert(textStamp.Value==stamp,"Library text flashed or changed during hover/GIF/focus activity");else textStamp=stamp;}
                    using(var still=CaptureLibrary(form.games,new Rectangle(8,76+13,46,46))){long stamp=PixelStamp(still);if(stillStamp.HasValue)AuditAssert(stillStamp.Value==stamp,"A static icon changed while another GIF animated");else stillStamp=stamp;}
                    using(var icon=CaptureLibrary(form.games,new Rectangle(8,13,46,46))){var pixel=icon.GetPixel(23,23);AuditAssert(pixel.R>200||pixel.B>200,"GIF icon exposed an empty/erased frame");if(colors.Add(pixel.R>200?0:1))icon.Save(Path.Combine(root,"icon-frame-"+(pixel.R>200?0:1)+".png"));}
                    AuditAssert(!form.busy&&!form.timer.Enabled,"Hover started a scheduled update in the offline preview");
                    if(count==30){other.Location=new Point(form.Right-200,form.Top+90);other.Show(form);other.Activate();}if(count==50){other.Hide();form.Activate();}
                }
                if(count==91){recording=false;animatedPaints=iconPaints;animatedWidePaints=widePaints;AuditAssert(colors.Count==2,"Visible sidebar GIF did not present both frames");AuditAssert(iconPaints>=5&&iconPaints<=sampleClock.ElapsedMilliseconds/100+3,"GIF paint cadence is not bounded by its frame rate: icons="+iconPaints+", wide="+widePaints);AuditAssert(widePaints<8,"The entire sidebar kept repainting while hovering");checks.Add("Native screen samples retain both GIF frames with no blank frames and stable row text");checks.Add("A neighboring static icon stays pixel-identical through repeated hover and focus changes");checks.Add("GIF updates invalidate only the 46-pixel icon area; repeated hover does not repaint the whole list");checks.Add("Hover does not start update checks or downloads");form.CaptureVisibleUi(root,"sidebar-buffered");UiEffects.Enabled=false;}
                if(count==94){form.games.Update();iconPaints=widePaints=0;recording=true;}
                if(count>94&&count<110)AuditListMessage(form.games.Handle,0x200,IntPtr.Zero,new IntPtr((30<<16)|30));
                if(count==110){recording=false;AuditAssert(iconPaints==0&&widePaints==0&&!form.iconAnimationClock.Enabled,"Paused/static sidebar kept repainting during hover");checks.Add("Paused GIFs leave the sidebar idle under continuous hover");form.libraryScrollbar.MoveTo(form.libraryScrollbar.Maximum);form.games.Update();AuditAssert(form.games.TopIndex>0,"Buffered list did not scroll");form.libraryScrollbar.MoveTo(0);form.games.Update();
                    form.reloadingLibrary=true;try{form.games.SelectedIndex=1;form.games.SelectedIndex=0;}finally{form.reloadingLibrary=false;}form.games.Update();
                    using(var restored=CaptureLibrary(form.games,new Rectangle(64,9,120,50)))AuditAssert(PixelStamp(restored)==textStamp.Value,"Selected row lost its title/status after scrolling out/back and native selection repaint");
                    checks.Add("Selected row title and status survive offscreen scrolling and native selection-only repaint");
                    form.reloadingLibrary=true;try{form.games.SelectedIndex=1;}finally{form.reloadingLibrary=false;}form.games.Invalidate();form.games.Update();
                    long offsetStamp;using(var selected=CaptureLibrary(form.games,new Rectangle(64,85,120,50)))offsetStamp=PixelStamp(selected);
                    form.libraryScrollbar.MoveTo(form.libraryScrollbar.Maximum);form.games.Update();form.libraryScrollbar.MoveTo(0);form.games.Update();
                    form.reloadingLibrary=true;try{form.games.SelectedIndex=2;form.games.SelectedIndex=1;}finally{form.reloadingLibrary=false;}form.games.Update();
                    using(var restored=CaptureLibrary(form.games,new Rectangle(64,85,120,50)))AuditAssert(PixelStamp(restored)==offsetStamp,"Offset selected row lost text during native row-only repaint");
                    checks.Add("Selected rows below the first row retain text during clipped native redraw and scrolling");

                    form.librarySearch.Text="nothing matches this title";form.games.Update();using(var empty=CaptureLibrary(form.games,new Rectangle(8,8,30,30)))AuditAssert(empty.GetPixel(15,15).ToArgb()==form.games.BackColor.ToArgb(),"Filtering to an empty list left ghost rows");form.librarySearch.Clear();form.games.Update();AuditAssert(form.games.Items.Count>30,"Clearing search lost rows");checks.Add("Scrolling and filtering repaint the buffer correctly without leaving ghost rows");File.WriteAllText(Path.Combine(root,"result.json"),Engine.Json.Serialize(new{Passed=true,Checks=checks,GamesExecuted=false,LivePreferencesChanged=false,IconPresentations=animatedPaints,WholeListPresentations=animatedWidePaints,SampleMilliseconds=sampleClock.ElapsedMilliseconds}));code=0;tick.Stop();form.Close();}
            }catch(Exception error){tick.Stop();File.WriteAllText(Path.Combine(root,"failure.txt"),error.ToString());form.Close();}};
            form.Shown+=delegate{tick.Start();};Application.Run(form);return code;
        }
    }catch(Exception error){Directory.CreateDirectory(root);File.WriteAllText(Path.Combine(root,"failure.txt"),error.ToString());return 1;}finally{UiEffects.Enabled=effects;}}
}
}
