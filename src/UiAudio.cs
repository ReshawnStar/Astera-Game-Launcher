using System;
using System.Collections.Generic;
using System.IO;
using System.Media;
using System.Reflection;
namespace Astera {
static class UiAudio {
    sealed class Clip:IDisposable {internal MemoryStream Stream;internal SoundPlayer Player;public void Dispose(){Player.Stop();Player.Dispose();Stream.Dispose();}}
    static readonly Dictionary<string,Clip> Clips=new Dictionary<string,Clip>();static bool enabled;static double volume=.7;static long hoverTick,selectTick;
    internal static void Configure(bool active,double gain){gain=Math.Max(0,Math.Min(1,gain));if(Math.Abs(gain-volume)>.001){foreach(var clip in Clips.Values)clip.Dispose();Clips.Clear();}volume=gain;enabled=active;}
    internal static byte[] Wave(string name,double gain){using(var input=Assembly.GetExecutingAssembly().GetManifestResourceStream("Astera.Sounds."+name)){
        if(input==null)throw new IOException("UI sound unavailable");using(var memory=new MemoryStream()){input.CopyTo(memory);var bytes=memory.ToArray();int at=12;while(at+8<=bytes.Length){int size=BitConverter.ToInt32(bytes,at+4);if(size<0||at+8L+size>bytes.Length)throw new IOException("Invalid UI WAV");if(System.Text.Encoding.ASCII.GetString(bytes,at,4)=="data"){for(int i=at+8;i+1<at+8+size;i+=2){short sample=BitConverter.ToInt16(bytes,i);short scaled=(short)Math.Max(Int16.MinValue,Math.Min(Int16.MaxValue,Math.Round(sample*gain)));bytes[i]=(byte)scaled;bytes[i+1]=(byte)(scaled>>8);}return bytes;}at+=8+size+(size&1);}throw new IOException("UI WAV has no samples");}
    }}
    static void Play(string name){if(!enabled||volume<=0)return;try{Clip clip;if(!Clips.TryGetValue(name,out clip)){var stream=new MemoryStream(Wave(name,volume),false);var player=new SoundPlayer(stream);player.Load();clip=new Clip{Stream=stream,Player=player};Clips.Add(name,clip);}clip.Player.Play();}catch(Exception error){AsteraDiagnostics.Write("UI audio unavailable",error);}}
    internal static void Hover(){long now=Environment.TickCount&Int32.MaxValue;if(now-hoverTick<90)return;hoverTick=now;Play("hover");}
    internal static void Select(){long now=Environment.TickCount&Int32.MaxValue;if(now-selectTick<60)return;selectTick=now;Play("select");}
}
}
