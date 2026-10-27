using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
namespace Astera {
sealed partial class LibraryForm {
    readonly Dictionary<string,ArtworkAnimation> iconAnimations=new Dictionary<string,ArtworkAnimation>(StringComparer.OrdinalIgnoreCase);
    readonly Timer iconAnimationClock=new Timer{Interval=15};bool iconAnimationsDisposed;
    string IconAnimationSource(CustomGame game){if(game==null)return null;if(!String.IsNullOrEmpty(game.Icon))return game.Icon;string path;if(itchArt.TryGetValue(game.Executable,out path))return path;var art=CustomSteam(game);return art==null?null:art.Icon;}
    Image AnimatedLibraryIcon(CustomGame game){string path=IconAnimationSource(game);ArtworkAnimation animation;return path!=null&&iconAnimations.TryGetValue(path,out animation)?animation.Image:null;}
    void RegisterIconAnimation(string path){if(String.IsNullOrEmpty(path)||iconAnimations.ContainsKey(path))return;try{if(!ArtworkAnimation.IsGif(path))return;var image=ArtworkAnimation.Open(path);if(image.Animated)iconAnimations[path]=image;else image.Dispose();var used=new HashSet<string>(store.Settings.CustomGames.Select(g=>IconAnimationSource(g)).Where(x=>x!=null),StringComparer.OrdinalIgnoreCase);foreach(string old in iconAnimations.Keys.Where(x=>!used.Contains(x)).ToArray()){iconAnimations[old].Dispose();iconAnimations.Remove(old);}UpdateIconAnimationPlayback();}catch(IOException){}catch(ArgumentException){}catch(UnauthorizedAccessException){}}
    IEnumerable<KeyValuePair<int,string>> VisibleIconSources(){int first=games.Items.Count==0?0:games.TopIndex,last=Math.Min(games.Items.Count,first+games.ClientSize.Height/Math.Max(1,games.ItemHeight)+1);for(int i=first;i<last;i++){var row=games.Items[i] as LibraryEntry;if(row==null||row.Custom==null)continue;string path=IconAnimationSource(row.Custom);if(path!=null)yield return new KeyValuePair<int,string>(i,path);}}
    void UpdateIconAnimationPlayback(){if(iconAnimationsDisposed)return;bool allowed=UiEffects.Enabled&&Visible&&IsHandleCreated&&WindowState!=FormWindowState.Minimized&&games.Visible;var visible=new HashSet<string>(allowed?VisibleIconSources().Select(p=>p.Value):Enumerable.Empty<string>(),StringComparer.OrdinalIgnoreCase);bool any=false;foreach(var pair in iconAnimations){bool play=visible.Contains(pair.Key)&&pair.Value.WantsPlayback;pair.Value.SetPlaying(play);any|=play;}iconAnimationClock.Enabled=any;}
    void IconPlaybackChanged(object sender,EventArgs e){UpdateIconAnimationPlayback();}
    void StartIconAnimations(){iconAnimationClock.Tick+=delegate{if(!Visible||WindowState==FormWindowState.Minimized||!UiEffects.Enabled){UpdateIconAnimationPlayback();return;}var advanced=new Dictionary<string,bool>(StringComparer.OrdinalIgnoreCase);bool any=false;foreach(var row in VisibleIconSources()){ArtworkAnimation asset;if(!iconAnimations.TryGetValue(row.Value,out asset))continue;bool changed;if(!advanced.TryGetValue(row.Value,out changed)){changed=asset.Advance();advanced[row.Value]=changed;}if(changed)games.Invalidate(games.GetItemRectangle(row.Key));any|=asset.WantsPlayback;}if(!any)UpdateIconAnimationPlayback();};VisibleChanged+=IconPlaybackChanged;Resize+=IconPlaybackChanged;UiEffects.Changed+=IconPlaybackChanged;Disposed+=delegate{iconAnimationsDisposed=true;UiEffects.Changed-=IconPlaybackChanged;iconAnimationClock.Dispose();foreach(var asset in iconAnimations.Values)asset.Dispose();iconAnimations.Clear();};UpdateIconAnimationPlayback();}
}
}
