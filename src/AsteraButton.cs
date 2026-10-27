using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace Astera {
public enum ButtonRole { Primary, Secondary, Quiet }
public enum ButtonSymbol { None, Play, Refresh, Settings, Exit, Download, Install, Restore, Cancel, More, Overview, Updates, Backgrounds, Folder, AddArtwork, Drive, Verified, Warning, Previous, Next, Pause, Retry, WindowMinimize, WindowMaximize, WindowRestore }

// Standard Button input/accessibility; only its drawing is customised.
public sealed class AsteraButton:Button {
    bool hover,pressed;
    ButtonRole role=ButtonRole.Secondary;
    ButtonSymbol symbol;
    readonly Timer glowTimer=new Timer();
    static readonly System.Diagnostics.Stopwatch GlowClock=System.Diagnostics.Stopwatch.StartNew();
    bool orbitGlow,releasingGlow;Form glowOwner;PointF[] glowPoints;
    public bool OrbitGlow {get{return orbitGlow;}set{orbitGlow=value;UpdateGlowTimer();Invalidate();}}
    public ButtonRole Role {get{return role;}set{role=value;Invalidate();}}
    public ButtonSymbol Symbol {get{return symbol;}set{symbol=value;Invalidate();PerformLayout();}}
    public AsteraButton(){
        SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.SupportsTransparentBackColor|ControlStyles.Opaque,true);
        FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;UseVisualStyleBackColor=false;
        BackColor=Color.Transparent;ForeColor=Color.FromArgb(237,241,255);Font=UiTheme.Button();
        Padding=new Padding(15,8,15,8);Margin=new Padding(0,0,10,0);MinimumSize=new Size(80,40);AutoSize=true;Cursor=Cursors.Hand;
        glowTimer.Interval=33;glowTimer.Tick+=delegate{Invalidate();};
    }
    public override Size GetPreferredSize(Size proposedSize){
        Size text=String.IsNullOrEmpty(Text)?Size.Empty:TextRenderer.MeasureText(Text,Font,new Size(Int32.MaxValue,Int32.MaxValue),TextFormatFlags.SingleLine|TextFormatFlags.NoPadding);
        return new Size(Math.Max(MinimumSize.Width,text.Width+Padding.Horizontal+(Symbol==ButtonSymbol.None?0:SymbolSize+(text.Width==0?0:8))+4),Math.Max(MinimumSize.Height,Math.Max(text.Height,Symbol==ButtonSymbol.None?0:SymbolSize)+Padding.Vertical+2));
    }
    protected override void OnMouseEnter(EventArgs e){hover=true;if(Enabled)UiAudio.Hover();base.OnMouseEnter(e);Invalidate();}
    protected override void OnClick(EventArgs e){if(Enabled)UiAudio.Select();base.OnClick(e);}
    protected override void OnMouseLeave(EventArgs e){hover=false;base.OnMouseLeave(e);Invalidate();}
    protected override void OnMouseDown(MouseEventArgs e){if(e.Button==MouseButtons.Left)pressed=true;base.OnMouseDown(e);Invalidate();}
    protected override void OnMouseUp(MouseEventArgs e){pressed=false;base.OnMouseUp(e);Invalidate();}
    protected override void OnMouseCaptureChanged(EventArgs e){if(!Capture)pressed=false;base.OnMouseCaptureChanged(e);Invalidate();}
    protected override void OnKeyDown(KeyEventArgs e){if(e.KeyCode==Keys.Space||e.KeyCode==Keys.Enter)pressed=true;base.OnKeyDown(e);Invalidate();}
    protected override void OnKeyUp(KeyEventArgs e){pressed=false;base.OnKeyUp(e);Invalidate();}
    protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);Invalidate();}
    protected override void OnLostFocus(EventArgs e){pressed=false;base.OnLostFocus(e);Invalidate();}
    protected override void OnEnabledChanged(EventArgs e){pressed=false;hover=false;Cursor=Enabled?Cursors.Hand:Cursors.Default;base.OnEnabledChanged(e);UpdateGlowTimer();Invalidate();}
    protected override void OnVisibleChanged(EventArgs e){base.OnVisibleChanged(e);UpdateGlowTimer();}
    protected override void OnSizeChanged(EventArgs e){glowPoints=null;base.OnSizeChanged(e);}
    protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);glowOwner=FindForm();if(glowOwner!=null){glowOwner.Resize+=GlowOwnerChanged;glowOwner.VisibleChanged+=GlowOwnerChanged;}UpdateGlowTimer();}
    protected override void OnHandleDestroyed(EventArgs e){DetachGlowOwner();if(!releasingGlow)glowTimer.Stop();base.OnHandleDestroyed(e);}
    void GlowOwnerChanged(object sender,EventArgs e){UpdateGlowTimer();}
    void UpdateGlowTimer(){if(glowTimer==null||releasingGlow)return;glowTimer.Enabled=orbitGlow&&Enabled&&Visible&&IsHandleCreated&&!IsDisposed&&(glowOwner==null||glowOwner.Visible&&glowOwner.WindowState!=FormWindowState.Minimized);}
    void DetachGlowOwner(){if(glowOwner!=null){glowOwner.Resize-=GlowOwnerChanged;glowOwner.VisibleChanged-=GlowOwnerChanged;glowOwner=null;}}
    protected override void Dispose(bool disposing){if(disposing&&!releasingGlow){releasingGlow=true;DetachGlowOwner();glowTimer.Dispose();}base.Dispose(disposing);}
    protected override void OnPaintBackground(PaintEventArgs e){}
    void PaintBackdrop(PaintEventArgs e){
        // Alpha artwork must start on a fresh parent scene on EVERY repaint.
        // Avoid ButtonBase's native/transparent background snapshot, which can
        // reuse stale owner-draw pixels during animation and resizing.
        Control ancestor=Parent,child=this;int x=0,y=0;
        while(ancestor!=null){x+=child.Left;y+=child.Top;if(ancestor.BackColor.A==255)break;child=ancestor;ancestor=ancestor.Parent;}
        if(ancestor==null){e.Graphics.Clear(Color.FromArgb(12,17,33));return;}
        var saved=e.Graphics.Save();try{e.Graphics.SetClip(ClientRectangle,CombineMode.Intersect);e.Graphics.TranslateTransform(-x,-y);var parentPaint=new PaintEventArgs(e.Graphics,new Rectangle(x,y,Width,Height));InvokePaintBackground(ancestor,parentPaint);InvokePaint(ancestor,parentPaint);}finally{e.Graphics.Restore(saved);}
    }
    protected override void OnPaint(PaintEventArgs e){
        if(Width<4||Height<4)return;
        PaintBackdrop(e);
        var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
        Rectangle bounds=new Rectangle(1,1,Width-3,Height-3);
        string state=!Enabled?"disabled":pressed?"pressed":hover?"hover":"normal";
        if(!ButtonAtlas.Draw(g,bounds,Role,state)){
        Color top,bottom,border;
        if(!Enabled){top=Color.FromArgb(43,48,64);bottom=Color.FromArgb(31,36,50);border=Color.FromArgb(64,73,96);}
        else if(Role==ButtonRole.Primary){top=pressed?Color.FromArgb(77,48,200):hover?Color.FromArgb(136,98,255):Color.FromArgb(113,77,255);bottom=pressed?Color.FromArgb(39,45,171):hover?Color.FromArgb(67,87,255):Color.FromArgb(49,58,239);border=hover?Color.FromArgb(194,182,255):Color.FromArgb(150,143,255);}
        else {top=pressed?Color.FromArgb(26,34,58):hover?Color.FromArgb(57,70,108):Color.FromArgb(38,48,80);bottom=pressed?Color.FromArgb(17,24,43):Color.FromArgb(24,32,55);border=hover?Color.FromArgb(122,143,207):Color.FromArgb(70,85,129);if(Role==ButtonRole.Quiet&&!hover&&!pressed){top=Color.FromArgb(29,36,55);bottom=Color.FromArgb(23,29,46);}}
        using(var path=Round(bounds,8)){
            using(var fill=new LinearGradientBrush(bounds,top,bottom,90F))g.FillPath(fill,path);
            using(var edge=new Pen(border,Role==ButtonRole.Primary&&Enabled?1.6F:1F))g.DrawPath(edge,path);
        }
        bool exact;Image art=ButtonArt.Get(Role,state,out exact);
        if(art!=null)ButtonArt.Draw(g,art,bounds,(!Enabled&&!exact) ? .42F : 1F);
        }
        if(OrbitGlow&&Enabled)DrawOrbitGlow(g);
        Color caption=Enabled?ForeColor:Color.FromArgb(151,162,188);
        Size text=String.IsNullOrEmpty(Text)?Size.Empty:TextRenderer.MeasureText(Text,Font,new Size(Int32.MaxValue,Int32.MaxValue),TextFormatFlags.SingleLine|TextFormatFlags.NoPadding);
        int iconWidth=Symbol==ButtonSymbol.None?0:SymbolSize+(text.Width==0?0:8),total=text.Width+iconWidth;
        int x=(Width-total)/2,y=(Height-text.Height)/2+(pressed&&Enabled?1:0);
        if(Symbol!=ButtonSymbol.None)DrawSymbol(g,Symbol,new Rectangle(x,y+(text.Height-SymbolSize)/2,SymbolSize,SymbolSize),caption);
        TextRenderer.DrawText(g,Text,Font,new Point(x+iconWidth,y),caption,TextFormatFlags.SingleLine|TextFormatFlags.NoPadding|(ShowKeyboardCues?0:TextFormatFlags.HidePrefix));
        if(Focused&&ShowFocusCues){var focus=new Rectangle(5,5,Width-11,Height-11);using(var p=Round(focus,6))using(var pen=new Pen(Color.FromArgb(230,233,255),1)){pen.DashStyle=DashStyle.Dot;g.DrawPath(pen,p);}}
    }
    void DrawOrbitGlow(Graphics g){
        if(Width<16||Height<16)return;
        if(glowPoints==null){
            using(var path=Round(new Rectangle(3,3,Width-7,Height-7),8)){
                path.Flatten(null,.3F);var points=path.PathPoints;var distances=new double[points.Length+1];
                for(int i=0;i<points.Length;i++){var a=points[i];var b=points[(i+1)%points.Length];distances[i+1]=distances[i]+Math.Sqrt((b.X-a.X)*(b.X-a.X)+(b.Y-a.Y)*(b.Y-a.Y));}
                glowPoints=new PointF[192];int edge=0;
                for(int i=0;i<glowPoints.Length;i++){double d=distances[points.Length]*i/glowPoints.Length;while(edge<points.Length-1&&distances[edge+1]<=d)edge++;double length=distances[edge+1]-distances[edge],t=length==0?0:(d-distances[edge])/length;var a=points[edge];var b=points[(edge+1)%points.Length];glowPoints[i]=new PointF(a.X+(float)t*(b.X-a.X),a.Y+(float)t*(b.Y-a.Y));}
            }
        }
        int head=(int)(GlowClock.Elapsed.TotalSeconds*glowPoints.Length/4)%glowPoints.Length;
        float[] widths={8F,4F,1.6F};int[] opacity={22,80,250};
        for(int pass=0;pass<3;pass++)using(var pen=new Pen(Color.White,widths[pass])){
            pen.StartCap=pen.EndCap=LineCap.Round;
            for(int step=0;step<64;step++){float strength=(step+1)/64F;int at=(head-64+step+glowPoints.Length)%glowPoints.Length;Color accent=ButtonAtlas.AccentColor;float white=pass==2?.75F:.15F;pen.Color=Color.FromArgb((int)(opacity[pass]*strength*strength),(int)(accent.R+(255-accent.R)*white),(int)(accent.G+(255-accent.G)*white),(int)(accent.B+(255-accent.B)*white));g.DrawLine(pen,glowPoints[at],glowPoints[(at+1)%glowPoints.Length]);}
        }
    }
    internal static GraphicsPath Round(Rectangle rect,int radius){var path=new GraphicsPath();int d=Math.Min(radius*2,Math.Min(rect.Width,rect.Height));path.AddArc(rect.Left,rect.Top,d,d,180,90);path.AddArc(rect.Right-d,rect.Top,d,d,270,90);path.AddArc(rect.Right-d,rect.Bottom-d,d,d,0,90);path.AddArc(rect.Left,rect.Bottom-d,d,d,90,90);path.CloseFigure();return path;}
    int SymbolSize {get{return Math.Max(20,Font.Height);}}
    static void DrawSymbol(Graphics g,ButtonSymbol type,Rectangle r,Color color){
        if(UiAssets.Draw(g,UiAssets.Name(type),r,color))return;
        var saved=g.Save();g.TranslateTransform(r.X,r.Y);g.ScaleTransform(r.Width/20F,r.Height/20F);
        using(var pen=new Pen(color,1.7F))using(var fill=new SolidBrush(color)){
            pen.StartCap=pen.EndCap=LineCap.Round;pen.LineJoin=LineJoin.Round;
            switch(type){
                case ButtonSymbol.Play:g.FillPolygon(fill,new[]{new PointF(5,2),new PointF(18,10),new PointF(5,18)});break;
                case ButtonSymbol.Refresh:g.DrawArc(pen,3,3,14,14,45,290);g.DrawLines(pen,new[]{new PointF(13,2),new PointF(18,3),new PointF(17,8)});break;
                case ButtonSymbol.Settings:g.DrawEllipse(pen,6,6,8,8);for(int i=0;i<8;i++){double a=i*Math.PI/4;g.DrawLine(pen,10+(float)Math.Cos(a)*7,10+(float)Math.Sin(a)*7,10+(float)Math.Cos(a)*9,10+(float)Math.Sin(a)*9);}break;
                case ButtonSymbol.Exit:g.DrawLines(pen,new[]{new PointF(8,2),new PointF(3,2),new PointF(3,18),new PointF(8,18)});g.DrawLine(pen,8,10,18,10);g.DrawLines(pen,new[]{new PointF(14,6),new PointF(18,10),new PointF(14,14)});break;
                case ButtonSymbol.Download:case ButtonSymbol.Install:g.DrawLine(pen,10,2,10,13);g.DrawLines(pen,new[]{new PointF(5,8),new PointF(10,13),new PointF(15,8)});g.DrawLines(pen,new[]{new PointF(3,14),new PointF(3,18),new PointF(17,18),new PointF(17,14)});break;
                case ButtonSymbol.Restore:g.DrawArc(pen,4,3,13,14,190,300);g.DrawLines(pen,new[]{new PointF(2,3),new PointF(2,9),new PointF(8,9)});break;
                case ButtonSymbol.Cancel:g.DrawLine(pen,5,5,15,15);g.DrawLine(pen,15,5,5,15);break;
                case ButtonSymbol.More:for(int x=3;x<20;x+=7)g.FillEllipse(fill,x-1,8,3,3);break;
                case ButtonSymbol.WindowMinimize:g.DrawLine(pen,4,11,16,11);break;
                case ButtonSymbol.WindowMaximize:g.DrawRectangle(pen,4,4,12,12);break;
                case ButtonSymbol.WindowRestore:g.DrawRectangle(pen,7,3,10,10);g.DrawRectangle(pen,3,7,10,10);break;
            }
        }g.Restore(saved);
    }
}

// Artist-supplied frames are embedded by the build script. Captions/icons stay
// live so Setup, Play and Install retain correct labels and keyboard behaviour.
static class ButtonArt {
    static readonly Dictionary<string,Bitmap> Images=new Dictionary<string,Bitmap>();
    public static Image Get(ButtonRole role,string state,out bool exact){
        string key=role.ToString().ToLowerInvariant()+"-"+state;
        var image=Load(key);exact=image!=null;
        return image??(state=="normal"?null:Load(role.ToString().ToLowerInvariant()+"-normal"));
    }
    static Bitmap Load(string name){Bitmap cached;if(Images.TryGetValue(name,out cached))return cached;
        Bitmap result=null;using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Astera.Buttons."+name)){
            if(stream!=null)using(var image=Image.FromStream(stream)){if(image.Width<3||image.Height<3||image.Width>4096||image.Height>4096)throw new InvalidDataException("Button frame dimensions are unsupported.");result=new Bitmap(image);}
        }Images[name]=result;return result;
    }
    public static void Draw(Graphics g,Image image,Rectangle destination,float opacity){
        int inset=Math.Min(24,Math.Min(image.Width,image.Height)/3);int scaled=Math.Min(destination.Height/2,Math.Max(1,(int)Math.Round(inset*(double)destination.Height/image.Height)));
        int[] sx={0,inset,image.Width-inset,image.Width},sy={0,inset,image.Height-inset,image.Height};
        int[] dx={destination.Left,destination.Left+scaled,destination.Right-scaled,destination.Right},dy={destination.Top,destination.Top+scaled,destination.Bottom-scaled,destination.Bottom};
        using(var attributes=new ImageAttributes()){
            var matrix=new ColorMatrix();matrix.Matrix33=opacity;attributes.SetColorMatrix(matrix);
            for(int y=0;y<3;y++)for(int x=0;x<3;x++)if(dx[x+1]>dx[x]&&dy[y+1]>dy[y])g.DrawImage(image,new Rectangle(dx[x],dy[y],dx[x+1]-dx[x],dy[y+1]-dy[y]),sx[x],sy[y],sx[x+1]-sx[x],sy[y+1]-sy[y],GraphicsUnit.Pixel,attributes);
        }
    }
}
}
