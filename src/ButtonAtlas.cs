using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.InteropServices;
namespace Astera {
// Original artist sheets remain unchanged. Source rectangles include padding
// around the outer glow; nine-slice drawing preserves corners at every width.
static class ButtonAtlas {
    static readonly Rectangle[] Frames={new Rectangle(30,38,2110,164),new Rectangle(35,212,1319,170),new Rectangle(1387,207,751,170),new Rectangle(34,398,690,148),new Rectangle(804,396,718,155),new Rectangle(1577,399,555,147),new Rectangle(280,564,424,126),new Rectangle(959,554,277,142),new Rectangle(1604,554,165,148)};
    static readonly Bitmap Art=Load("Buttons"),Mask=Load("Mask");
    static readonly Dictionary<string,Bitmap> Cache=new Dictionary<string,Bitmap>();
    static string accent="Violet";
    public static readonly string[] Accents={"Violet","Teal","Rose","Gold"};
    public static bool Available {get{return Art!=null&&Mask!=null;}}
    public static Color AccentColor {get{return accent=="Teal"?Color.FromArgb(52,218,209):accent=="Rose"?Color.FromArgb(240,85,166):accent=="Gold"?Color.FromArgb(235,180,63):Color.FromArgb(139,116,255);}}
    public static void SetAccent(string value){if(Array.IndexOf(Accents,value)<0)value="Violet";if(value==accent)return;accent=value;foreach(var item in Cache.Values)item.Dispose();Cache.Clear();}
    static Bitmap Load(string name){using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Astera.Art."+name)){if(stream==null)return null;using(var image=Image.FromStream(stream)){if(image.Width!=2164||image.Height!=727)throw new InvalidOperationException("Button artwork dimensions changed; update its frame map before building.");return new Bitmap(image);}}}
    public static bool Draw(Graphics g,Rectangle bounds,ButtonRole role,string state){
        if(!Available)return false;double ratio=bounds.Width/(double)bounds.Height,best=Double.MaxValue;int chosen=0;
        for(int i=0;i<Frames.Length;i++){double delta=Math.Abs(Math.Log((Frames[i].Width/(double)Frames[i].Height)/ratio));if(delta<best){best=delta;chosen=i;}}
        string key=chosen+":"+role+":"+state;Bitmap frame;if(!Cache.TryGetValue(key,out frame)){frame=CreateFrame(chosen,role,state);Cache.Add(key,frame);}
        DrawFrame(g,frame,bounds);return true;
    }
    static Bitmap CreateFrame(int index,ButtonRole role,string state){
        var rect=Frames[index];var frame=Art.Clone(rect,PixelFormat.Format32bppArgb);using(var mask=Mask.Clone(rect,PixelFormat.Format32bppArgb)){
            var data=frame.LockBits(new Rectangle(Point.Empty,frame.Size),ImageLockMode.ReadWrite,PixelFormat.Format32bppArgb);
            var maskData=mask.LockBits(new Rectangle(Point.Empty,mask.Size),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
            try{
                var pixels=new byte[data.Stride*data.Height];var tones=new byte[maskData.Stride*maskData.Height];Marshal.Copy(data.Scan0,pixels,0,pixels.Length);Marshal.Copy(maskData.Scan0,tones,0,tones.Length);
                Color tint=role==ButtonRole.Primary?AccentColor:role==ButtonRole.Secondary?Color.FromArgb(43,57,93):Color.FromArgb(29,39,65);
                for(int y=0;y<data.Height;y++)for(int x=0;x<data.Width;x++){
                    int at=y*data.Stride+x*4,mat=y*maskData.Stride+x*4;if(pixels[at+3]==0)continue;
                    float shade=(tones[mat]+tones[mat+1]+tones[mat+2])/3F,gray=(pixels[at]+pixels[at+1]+pixels[at+2])/3F;
                    bool original=role==ButtonRole.Primary&&accent=="Violet";
                    for(int channel=0;channel<3;channel++){
                        float value=pixels[at+channel];
                        if(!original){float color=channel==0?tint.B:channel==1?tint.G:tint.R;value=shade<=150?color*shade/150F:color+(255-color)*(shade-150)/105F;}
                        if(state=="hover")value=value*1.08F+5;else if(state=="pressed")value*=.76F;
                        if(state=="disabled")value=(gray*.7F+value*.3F)*.55F;
                        pixels[at+channel]=(byte)Math.Max(0,Math.Min(255,value));
                    }
                    // Use the blue sheet's alpha as the silhouette for all variants.
                }
                Marshal.Copy(pixels,0,data.Scan0,pixels.Length);
            }finally{frame.UnlockBits(data);mask.UnlockBits(maskData);}
        }return frame;
    }
    static void DrawFrame(Graphics g,Bitmap frame,Rectangle destination){
        const int inset=48;int corner=Math.Min(Math.Min(destination.Width,destination.Height)/2,(int)Math.Round(inset*destination.Height/(double)frame.Height));
        int[] sx={0,inset,frame.Width-inset,frame.Width},sy={0,inset,frame.Height-inset,frame.Height},dx={destination.Left,destination.Left+corner,destination.Right-corner,destination.Right},dy={destination.Top,destination.Top+corner,destination.Bottom-corner,destination.Bottom};
        var interpolation=g.InterpolationMode;var offset=g.PixelOffsetMode;g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.Half;
        using(var attributes=new ImageAttributes()){attributes.SetWrapMode(WrapMode.TileFlipXY);for(int y=0;y<3;y++)for(int x=0;x<3;x++)if(dx[x+1]>dx[x]&&dy[y+1]>dy[y])g.DrawImage(frame,new Rectangle(dx[x],dy[y],dx[x+1]-dx[x],dy[y+1]-dy[y]),sx[x],sy[y],sx[x+1]-sx[x],sy[y+1]-sy[y],GraphicsUnit.Pixel,attributes);}
        g.InterpolationMode=interpolation;g.PixelOffsetMode=offset;
    }
}
}
