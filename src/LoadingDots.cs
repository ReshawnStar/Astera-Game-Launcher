using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
namespace Astera {
static class LoadingDots {
    internal static void Draw(Graphics g,Rectangle bounds,double seconds,bool animated){
        var saved=g.Save();try{g.SmoothingMode=SmoothingMode.AntiAlias;float cx=bounds.Left+bounds.Width/2F,cy=bounds.Top+bounds.Height/2F;
            for(int i=0;i<6;i++){double angle=-Math.PI/2+i*Math.PI/3;double wave=animated?Math.Pow((1+Math.Cos(i*Math.PI/3-seconds*Math.PI*2/1.2))/2,2):.35;int alpha=45+(int)Math.Round(210*wave);using(var dot=new SolidBrush(Color.FromArgb(alpha,ButtonAtlas.AccentColor)))g.FillEllipse(dot,cx+(float)Math.Cos(angle)*19-3.5F,cy+(float)Math.Sin(angle)*19-3.5F,7,7);}
        }finally{g.Restore(saved);}
    }
    static Bitmap Frame(double seconds,bool animated){var bitmap=new Bitmap(64,64);using(var g=Graphics.FromImage(bitmap)){g.Clear(Color.Transparent);Draw(g,new Rectangle(0,0,64,64),seconds,animated);}return bitmap;}
    static int Groups(Bitmap image){var seen=new bool[image.Width,image.Height];int groups=0;for(int y=0;y<image.Height;y++)for(int x=0;x<image.Width;x++){if(seen[x,y]||image.GetPixel(x,y).A==0)continue;groups++;var queue=new Queue<Point>();queue.Enqueue(new Point(x,y));seen[x,y]=true;while(queue.Count>0){var at=queue.Dequeue();for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++){int px=at.X+dx,py=at.Y+dy;if(px<0||py<0||px>=image.Width||py>=image.Height||seen[px,py]||image.GetPixel(px,py).A==0)continue;seen[px,py]=true;queue.Enqueue(new Point(px,py));}}}return groups;}
    internal static void Audit(string root,List<string> checks){using(var a=Frame(0,true))using(var b=Frame(.2,true))using(var still=Frame(0,false))using(var later=Frame(9,false)){
        if(Groups(a)!=6||Groups(b)!=6||Groups(still)!=6)throw new IOException("Loading indicator must retain exactly six separated dots");bool changed=false;for(int y=0;y<64;y++)for(int x=0;x<64;x++){changed|=a.GetPixel(x,y)!=b.GetPixel(x,y);if(still.GetPixel(x,y)!=later.GetPixel(x,y))throw new IOException("Reduced-motion dots changed over time");}if(!changed)throw new IOException("Loading dot opacity did not advance");a.Save(Path.Combine(root,"loading-dots-first.png"));b.Save(Path.Combine(root,"loading-dots-next.png"));checks.Add("Six separated loading dots fade around the ring; effects off renders a stable six-dot indicator");
    }}
}
}