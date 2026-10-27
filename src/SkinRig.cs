using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
namespace Astera {
// A small textured cuboid rig for the launcher, independent of the Minecraft runtime.
sealed class SkinRig:IDisposable {
    readonly Dictionary<int,int[]> shaded=new Dictionary<int,int[]>();Bitmap raster;int[] texels,pixels,owners;float[] depths;int rasterWidth,rasterHeight;
    readonly Bitmap skin;readonly bool slim;double lookYaw,lookPitch;
    struct V {internal double X,Y,Z;internal V(double x,double y,double z){X=x;Y=y;Z=z;}}
    sealed class Face {internal PointF[] Corners;internal Rectangle Texture;internal double Depth;internal float Light;internal double Z0,Z1,Z2;internal bool FlipV;internal int Order;}
    internal SkinRig(string pose){string raw=pose+".skin.png";using(var loaded=Banner.ReadArtwork(raw)){if(loaded.Width!=64||(loaded.Height!=64&&loaded.Height!=32))throw new IOException("Unsupported skin canvas");skin=new Bitmap(loaded);}slim=File.ReadAllText(pose+".model")=="slim";texels=new int[skin.Width*skin.Height];var data=skin.LockBits(new Rectangle(0,0,skin.Width,skin.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);try{System.Runtime.InteropServices.Marshal.Copy(data.Scan0,texels,0,texels.Length);}finally{skin.UnlockBits(data);}}
    static V Rotate(V p,double x,double y,double z){double sx=Math.Sin(x),cx=Math.Cos(x),sy=Math.Sin(y),cy=Math.Cos(y),sz=Math.Sin(z),cz=Math.Cos(z);var a=new V(p.X,p.Y*cx-p.Z*sx,p.Y*sx+p.Z*cx);var b=new V(a.X*cy+a.Z*sy,a.Y,-a.X*sy+a.Z*cy);return new V(b.X*cz-b.Y*sz,b.X*sz+b.Y*cz,b.Z);}
    static double R(double degrees){return degrees*Math.PI/180;}
    internal PointF LookDirection {get{var direction=Rotate(Rotate(new V(0,0,-1),lookPitch,lookYaw,0),R(-7),R(14),0);return new PointF((float)direction.X,(float)-direction.Y);}}
    internal void Draw(Graphics graphics,Rectangle area,Point pointer,double seconds){
        double scale=Math.Min(area.Width/27.0,area.Height/22.0),cx=area.X+area.Width*.46,cy=area.Bottom+scale*3;
        double aimX=Math.Max(-1,Math.Min(1,(pointer.X-cx)/Math.Max(100,area.Width*.65))),aimY=Math.Max(-1,Math.Min(1,(pointer.Y-(area.Top+area.Height*.4))/Math.Max(100,area.Height*.7)));
        lookYaw+=(-R(14)-aimX*R(24)-lookYaw)*.14;lookPitch+=(R(7)-aimY*R(14)-lookPitch)*.14;
        double bob=Math.Sin(seconds*1.4)*.28,drift=Math.Sin(seconds*.7)*.35,waveTime=seconds%12;
        double lift=waveTime<.4?waveTime/.4:waveTime<2.3?1:waveTime<3?(3-waveTime)/.7:0;lift=lift*lift*(3-2*lift);double wave=lift*(R(145)+Math.Sin(seconds*7)*R(8));
        var faces=new List<Face>();
        Cube(faces,new V(0,0,0),8,12,4,16,16,0,0,0,0,scale,cx,cy,bob,drift);
        if(skin.Height==64)Cube(faces,new V(0,0,0),8,12,4,16,32,.24,0,0,0,scale,cx,cy,bob,drift);
        double arm=slim?3:4,shoulder=4+arm/2;
        Cube(faces,new V(-shoulder,11.7,0),arm,12,4,skin.Height==64?32:40,skin.Height==64?48:16,0,0,0,R(-4)+Math.Sin(seconds*1.5)*R(2),scale,cx,cy,bob,drift,true);
        if(skin.Height==64)Cube(faces,new V(-shoulder,11.7,0),arm,12,4,48,48,.24,0,0,R(-4)+Math.Sin(seconds*1.5)*R(2),scale,cx,cy,bob,drift,true);
        Cube(faces,new V(shoulder,11.7,0),arm,12,4,40,16,0,0,0,wave+R(4),scale,cx,cy,bob,drift,true);
        if(skin.Height==64)Cube(faces,new V(shoulder,11.7,0),arm,12,4,40,32,.24,0,0,wave+R(4),scale,cx,cy,bob,drift,true);
        Cube(faces,new V(0,12,0),8,8,8,0,0,0,lookPitch,lookYaw,0,scale,cx,cy,bob,drift);
        Cube(faces,new V(0,12,0),8,8,8,32,0,.25,lookPitch,lookYaw,0,scale,cx,cy,bob,drift);
        Rasterize(graphics,faces,area);
    }
    int[] ShadedTexels(float light){int key=(int)(light*256);int[] result;if(shaded.TryGetValue(key,out result))return result;result=new int[texels.Length];for(int i=0;i<result.Length;i++){int color=texels[i];result[i]=(color&unchecked((int)0xff000000))|(((((color>>16)&255)*key)>>8)<<16)|(((((color>>8)&255)*key)>>8)<<8)|(((color&255)*key)>>8);}shaded.Add(key,result);return result;}
    unsafe void Rasterize(Graphics graphics,List<Face> faces,Rectangle area){
        if(raster==null||rasterWidth!=area.Width||rasterHeight!=area.Height){
            if(raster!=null)raster.Dispose();rasterWidth=area.Width;rasterHeight=area.Height;
            raster=new Bitmap(rasterWidth,rasterHeight,PixelFormat.Format32bppArgb);pixels=new int[rasterWidth*rasterHeight];depths=new float[pixels.Length];owners=new int[pixels.Length];
        }
        Array.Clear(pixels,0,pixels.Length);
        faces.Sort((a,b)=>{int d=b.Depth.CompareTo(a.Depth);return d==0?a.Order.CompareTo(b.Order):d;});
        fixed(int* output=pixels)fixed(float* depth=depths)fixed(int* owner=owners){
            for(int i=0;i<pixels.Length;i++)depth[i]=Single.PositiveInfinity;
            foreach(var face in faces){
                var a=face.Corners[0];var b=face.Corners[1];var c=face.Corners[2];float sx=b.X-a.X,sy=b.Y-a.Y,tx=c.X-a.X,ty=c.Y-a.Y,det=sx*ty-sy*tx;if(Math.Abs(det)<.001F)continue;
                float fourthX=b.X+c.X-a.X,fourthY=b.Y+c.Y-a.Y;
                int left=Math.Max(0,(int)Math.Floor(Math.Min(Math.Min(a.X,b.X),Math.Min(c.X,fourthX)))-area.X),right=Math.Min(rasterWidth-1,(int)Math.Ceiling(Math.Max(Math.Max(a.X,b.X),Math.Max(c.X,fourthX)))-area.X);
                int top=Math.Max(0,(int)Math.Floor(Math.Min(Math.Min(a.Y,b.Y),Math.Min(c.Y,fourthY)))-area.Y),bottom=Math.Min(rasterHeight-1,(int)Math.Ceiling(Math.Max(Math.Max(a.Y,b.Y),Math.Max(c.Y,fourthY)))-area.Y);
                float ds=ty/det,dt=-sy/det,dzS=(float)(face.Z1-face.Z0),dzT=(float)(face.Z2-face.Z0),dz=ds*dzS+dt*dzT;int tw=face.Texture.Width,th=face.Texture.Height,textureOffset=face.Texture.Y*skin.Width+face.Texture.X,stride=skin.Width,order=face.Order;bool flip=face.FlipV;
                var source=ShadedTexels(face.Light);
                fixed(int* texture=source)for(int y=top;y<=bottom;y++){
                    float dx=left+area.X+.5F-a.X,dy=y+area.Y+.5F-a.Y,s=(dx*ty-dy*tx)/det,t=(sx*dy-sy*dx)/det,z=(float)face.Z0+s*dzS+t*dzT;int at=y*rasterWidth+left;
                    for(int x=left;x<=right;x++,at++,s+=ds,t+=dt,z+=dz){
                        if(s<0||s>=1||t<0||t>=1||z>depth[at]+.0001F)continue;
                        if(z>=depth[at]-.0001F&&order<owner[at])continue;
                        int u=(int)(s*tw),v=(int)((flip?1-t:t)*th);if(v>=th)v=th-1;
                        int color=texture[textureOffset+v*stride+u],alpha=(int)((uint)color>>24);if(alpha==0)continue;
                        if(alpha==255)output[at]=color;
                        else{int old=output[at],oldAlpha=(int)((uint)old>>24),outAlpha=alpha+oldAlpha*(255-alpha)/255,weight=oldAlpha*(255-alpha)/255;output[at]=(outAlpha<<24)|((((((color>>16)&255)*alpha)+((old>>16)&255)*weight)/outAlpha)<<16)|((((((color>>8)&255)*alpha)+((old>>8)&255)*weight)/outAlpha)<<8)|(((color&255)*alpha+(old&255)*weight)/outAlpha);}
                        depth[at]=z;owner[at]=order;
                    }
                }
            }
        }
        var data=raster.LockBits(new Rectangle(0,0,rasterWidth,rasterHeight),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);try{System.Runtime.InteropServices.Marshal.Copy(pixels,0,data.Scan0,pixels.Length);}finally{raster.UnlockBits(data);}
        graphics.DrawImageUnscaled(raster,area.X,area.Y);
    }
    internal void VerifyCrossingFaces(){
        var corners=new[]{new PointF(0,0),new PointF(32,0),new PointF(0,16)};
        var red=new Face{Corners=corners,Texture=new Rectangle(16,20,8,12),Z0=0,Z1=4,Z2=0,Depth=2,Light=1};
        var white=new Face{Corners=corners,Texture=new Rectangle(8,8,8,8),Z0=4,Z1=0,Z2=4,Depth=2,Light=1};
        using(var image=new Bitmap(32,16))using(var g=Graphics.FromImage(image)){
            Rasterize(g,new List<Face>{red,white},new Rectangle(0,0,32,16));var expected=(int[])pixels.Clone();
            if((expected[8*32+2]&0xffffff)!=0xff0000||(expected[8*32+29]&0xffffff)!=0xffffff)throw new Exception("Crossing surfaces did not choose the nearest texel");
            Rasterize(g,new List<Face>{white,red},new Rectangle(0,0,32,16));for(int i=0;i<pixels.Length;i++)if(pixels[i]!=expected[i])throw new Exception("Opaque depth visibility changed with face order");
        }
    }
    void Cube(List<Face> faces,V origin,double w,double h,double d,int u,int v,double inflate,double rx,double ry,double rz,double scale,double cx,double cy,double bob,double drift,bool arm=false){
        double left=-w/2-inflate,right=w/2+inflate,bottom=(arm?-h:0)-inflate,top=(arm?0:h)+inflate,front=-d/2-inflate,back=d/2+inflate;
        V[] p={new V(left,top,front),new V(right,top,front),new V(left,bottom,front),new V(right,bottom,front),new V(left,top,back),new V(right,top,back),new V(left,bottom,back),new V(right,bottom,back)};
        for(int i=0;i<p.Length;i++){var q=Rotate(p[i],rx,ry,rz);q=new V(q.X+origin.X+drift,q.Y+origin.Y+bob,q.Z+origin.Z);p[i]=Rotate(q,R(-7),R(14),0);}
        int iw=(int)w,ih=(int)h,id=(int)d;
        Add(faces,p,0,1,2,new Rectangle(u+id,v+id,iw,ih),1,scale,cx,cy);
        Add(faces,p,5,4,7,new Rectangle(u+id+iw+id,v+id,iw,ih),.72F,scale,cx,cy);
        Add(faces,p,4,0,6,new Rectangle(u,v+id,id,ih),.79F,scale,cx,cy);
        Add(faces,p,1,5,3,new Rectangle(u+id+iw,v+id,id,ih),.84F,scale,cx,cy);
        Add(faces,p,4,5,0,new Rectangle(u+id,v,iw,id),1,scale,cx,cy);
        Add(faces,p,2,3,6,new Rectangle(u+id+iw,v,iw,id),.68F,scale,cx,cy,true);
    }
    static void Add(List<Face> faces,V[] p,int a,int b,int c,Rectangle uv,float light,double scale,double cx,double cy,bool flipV=false){
        var corners=new[]{new PointF((float)(cx+p[a].X*scale),(float)(cy-p[a].Y*scale)),new PointF((float)(cx+p[b].X*scale),(float)(cy-p[b].Y*scale)),new PointF((float)(cx+p[c].X*scale),(float)(cy-p[c].Y*scale))};
        double cross=(corners[1].X-corners[0].X)*(corners[2].Y-corners[0].Y)-(corners[1].Y-corners[0].Y)*(corners[2].X-corners[0].X);if(cross<=0)return;
        faces.Add(new Face{Corners=corners,Texture=uv,Depth=(p[b].Z+p[c].Z)/2,Light=light,Z0=p[a].Z,Z1=p[b].Z,Z2=p[c].Z,FlipV=flipV,Order=faces.Count});
    }
    public void Dispose(){skin.Dispose();if(raster!=null)raster.Dispose();}
}
}
