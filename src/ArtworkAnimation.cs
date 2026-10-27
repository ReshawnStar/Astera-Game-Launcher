using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
namespace Astera {
// The original stream remains alive until the decoded image is disposed.
sealed class ArtworkAnimation:IDisposable {
    MemoryStream stream;Image image;readonly Stopwatch clock=new Stopwatch();readonly long[] ends;readonly long cycle;readonly int cycles;int frame;bool completed;
    internal Image Image {get{return image;}}
    internal int CurrentFrame {get{return frame;}}
    internal int FrameCount {get{return ends.Length;}}
    internal bool Animated {get{return FrameCount>1;}}
    internal bool WantsPlayback {get{return Animated&&!completed;}}
    internal bool Playing {get{return clock.IsRunning;}}
    internal static bool IsGif(string path){using(var file=File.OpenRead(path)){var bytes=new byte[6];return file.Read(bytes,0,6)==6&&bytes[0]=='G'&&bytes[1]=='I'&&bytes[2]=='F'&&bytes[3]=='8'&&(bytes[4]=='7'||bytes[4]=='9')&&bytes[5]=='a';}}
    internal static ArtworkAnimation Open(string path){using(var file=File.OpenRead(path))using(var output=new MemoryStream()){if(file.Length>25*1024*1024)throw new IOException("Use artwork smaller than25 MB.");var block=new byte[16384];int n;while((n=file.Read(block,0,block.Length))>0){if(output.Length+n>25*1024*1024)throw new IOException("Artwork too large.");output.Write(block,0,n);}return FromBytes(output.ToArray());}}
    internal static ArtworkAnimation FromBytes(byte[] bytes){var memory=new MemoryStream(bytes,false);Image decoded=null;try{decoded=Image.FromStream(memory,true,true);return new ArtworkAnimation(memory,decoded);}catch{if(decoded!=null)decoded.Dispose();memory.Dispose();throw;}}
    ArtworkAnimation(MemoryStream source,Image decoded){stream=source;image=decoded;if(image.Width<1||image.Height<1||(long)image.Width*image.Height>20000000)throw new IOException("Use artwork below20 megapixels.");int count=image.RawFormat.Guid==ImageFormat.Gif.Guid?image.GetFrameCount(FrameDimension.Time):1;if(count>256||count>1&&(long)image.Width*image.Height*count>16000000)throw new IOException("Use a smaller GIF: up to256 frames and16 million pixels across all frames.");ends=new long[count];byte[] delays=null;int repeat=0;bool hasRepeat=false;
        if(count>1){try{delays=image.GetPropertyItem(0x5100).Value;}catch(ArgumentException){}try{var loop=image.GetPropertyItem(0x5101).Value;if(loop.Length>=2){repeat=BitConverter.ToUInt16(loop,0);hasRepeat=true;}}catch(ArgumentException){}}
        long total=0;for(int i=0;i<count;i++){int duration=100;if(delays!=null&&delays.Length>=(i+1)*4){int raw=BitConverter.ToInt32(delays,i*4);duration=raw<=0?100:(int)Math.Min(60000L,Math.Max(20L,(long)raw*10));}total+=duration;ends[i]=total;}cycle=total;cycles=hasRepeat?(repeat==0?0:repeat+1):1;
    }
    internal void SetPlaying(bool play){if(play&&WantsPlayback)clock.Start();else clock.Stop();}
    internal int NextFrameDelay {get{return DelayAt(clock.ElapsedMilliseconds);}}
    internal int DelayAt(long elapsed){if(!WantsPlayback)return 60000;long position=Math.Max(0,elapsed)%cycle;int index=Array.BinarySearch(ends,position+1);if(index<0)index=~index;return (int)Math.Max(1,Math.Min(60000,ends[Math.Min(FrameCount-1,index)]-position));}
    internal bool Advance(){return AdvanceAt(clock.ElapsedMilliseconds);}
    internal bool AdvanceAt(long elapsed){if(!Animated||image==null)return false;long position=Math.Max(0,elapsed);int next;if(cycles>0&&position>=cycle*cycles){next=FrameCount-1;completed=true;clock.Stop();}else{position%=cycle;next=Array.BinarySearch(ends,position+1);if(next<0)next=~next;next=Math.Min(FrameCount-1,next);}if(next==frame)return false;image.SelectActiveFrame(FrameDimension.Time,next);frame=next;return true;}
    public void Dispose(){clock.Stop();if(image!=null){image.Dispose();image=null;}if(stream!=null){stream.Dispose();stream=null;}}
}
sealed partial class Banner {
    ArtworkAnimation sceneAnimation,characterAnimation;
    internal ArtworkAnimation BackgroundAnimation {get{return sceneAnimation;}}
    internal bool ArtworkPlayback {get{return rigClock.Enabled&&(sceneAnimation!=null&&sceneAnimation.Playing||characterAnimation!=null&&characterAnimation.Playing);}}
    bool HasAnimatedArtwork {get{return sceneAnimation!=null&&sceneAnimation.WantsPlayback||characterAnimation!=null&&characterAnimation.WantsPlayback;}}
    void SetArtworkPlayback(bool enabled){if(sceneAnimation!=null)sceneAnimation.SetPlaying(enabled);if(characterAnimation!=null)characterAnimation.SetPlaying(enabled);}
    int ArtworkFrameDelay {get{int delay=60000;if(sceneAnimation!=null&&sceneAnimation.WantsPlayback)delay=Math.Min(delay,sceneAnimation.NextFrameDelay);if(characterAnimation!=null&&characterAnimation.WantsPlayback)delay=Math.Min(delay,characterAnimation.NextFrameDelay);return Math.Max(15,delay);}}
    void TickArtworkAndRig(){bool changed=false;if(sceneAnimation!=null)changed|=sceneAnimation.Advance();if(characterAnimation!=null)changed|=characterAnimation.Advance();if(changed){frameDirty=true;RenderFrame();Invalidate();}else if(liveRig!=null){RenderFrame();Invalidate(new Rectangle((int)(Width*.60F),0,Width-(int)(Width*.60F),Height),false);}if(liveRig==null)UpdateRigClock();}
}
}
