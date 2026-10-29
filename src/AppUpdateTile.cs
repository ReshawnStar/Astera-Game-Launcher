using System;
using System.Drawing;
using System.Drawing.Drawing2D;
namespace Astera {
public sealed partial class AsteraButton {
    bool updateTile;
    public bool UpdateTile {get{return updateTile;}set{if(updateTile==value)return;updateTile=value;Invalidate();}}
    void DrawUpdateTile(Graphics graphics){
        if(Symbol==ButtonSymbol.Download){DownloadIconArtwork.Draw(graphics,new Rectangle(0,0,Width,Height),Enabled);if(Focused&&ShowFocusCues)using(var outline=Round(new Rectangle(0,0,Width-1,Height-1),10))using(var edge=new Pen(Color.White,1))graphics.DrawPath(edge,outline);return;}graphics.SmoothingMode=SmoothingMode.AntiAlias;var area=new Rectangle(2,2,Width-5,Height-5);if(area.Width<10||area.Height<10)return;
        Color accent=ButtonAtlas.AccentColor;bool ready=Role==ButtonRole.Primary;
        Color top=!Enabled?Color.FromArgb(43,51,69):ready?Color.FromArgb(Math.Min(255,accent.R+28),Math.Min(255,accent.G+24),Math.Min(255,accent.B+16)):Color.FromArgb(35,45,68);
        Color bottom=!Enabled?Color.FromArgb(29,36,51):ready?Color.FromArgb(Math.Max(0,accent.R-18),Math.Max(0,accent.G-22),Math.Max(0,accent.B-8)):Color.FromArgb(20,28,47);
        using(var path=Round(area,Math.Max(6,area.Width/4))){
            using(var brush=new LinearGradientBrush(area,top,bottom,90f))graphics.FillPath(brush,path);
            using(var edge=new Pen(!Enabled?Color.FromArgb(76,89,113):ready?Color.FromArgb(210,Math.Min(255,accent.R+65),Math.Min(255,accent.G+55),Math.Min(255,accent.B+45)):Color.FromArgb(118,145,187),Math.Max(1.2f,area.Width/(hover?22f:28f))))graphics.DrawPath(edge,path);
        }
        var state=graphics.Save();graphics.TranslateTransform(area.Left+5+(pressed?1:0),area.Top+5+(pressed?1:0));graphics.ScaleTransform((area.Width-10)/24f,(area.Height-10)/24f);
        Color ink=!Enabled?Color.FromArgb(142,157,181):ready?ButtonAtlas.AccentTextColor:Color.FromArgb(207,222,244);
        using(var pen=new Pen(ink,2.7f)){pen.StartCap=pen.EndCap=LineCap.Round;pen.LineJoin=LineJoin.Round;
            if(Symbol==ButtonSymbol.Download){graphics.DrawLine(pen,12,3,12,15);graphics.DrawLines(pen,new[]{new PointF(6,10),new PointF(12,16),new PointF(18,10)});graphics.DrawLines(pen,new[]{new PointF(3,17),new PointF(3,21),new PointF(21,21),new PointF(21,17)});}
            else{graphics.DrawArc(pen,4,4,16,16,35,285);graphics.DrawLines(pen,new[]{new PointF(20,3),new PointF(20,9),new PointF(14,9)});}
        }graphics.Restore(state);
        if(Focused&&ShowFocusCues)using(var outline=Round(new Rectangle(0,0,Width-1,Height-1),10))using(var edge=new Pen(Color.White,1))graphics.DrawPath(edge,outline);
    }
}
}
