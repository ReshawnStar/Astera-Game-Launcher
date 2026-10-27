using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
namespace Astera {
sealed class GameTransition:Panel {
    Bitmap previous,incoming;readonly Timer clock=new Timer{Interval=15};readonly Stopwatch elapsed=new Stopwatch();readonly Action finished;
    internal GameTransition(Bitmap old,Action done){previous=old;finished=done;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.Opaque,true);Dock=DockStyle.Fill;clock.Tick+=delegate{if(elapsed.ElapsedMilliseconds>=240){clock.Stop();if(finished!=null)finished();}else Invalidate();};}
    internal void Start(Bitmap next){incoming=next;elapsed.Restart();clock.Start();Invalidate();}
    internal static Bitmap CaptureFrame(Control content){var image=new Bitmap(content.Width,content.Height);try{using(var g=Graphics.FromImage(image))g.Clear(content.BackColor);content.DrawToBitmap(image,content.ClientRectangle);return image;}catch{image.Dispose();throw;}}
    protected override void OnPaintBackground(PaintEventArgs e){}
    protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(Parent==null?Color.FromArgb(12,17,33):Parent.BackColor);double t=Math.Min(1,elapsed.ElapsedMilliseconds/240.0),ease=incoming==null?0:1-Math.Pow(1-t,3);DrawFrame(e.Graphics,previous,0,1);if(incoming!=null)DrawFrame(e.Graphics,incoming,(int)(24*(1-ease)),1);}
    static void DrawFrame(Graphics g,Image frame,int y,float alpha){if(frame==null)return;using(var attributes=new ImageAttributes()){var matrix=new ColorMatrix();matrix.Matrix33=alpha;attributes.SetColorMatrix(matrix);attributes.SetWrapMode(System.Drawing.Drawing2D.WrapMode.TileFlipXY);g.DrawImage(frame,new Rectangle(0,y,frame.Width,frame.Height),0,0,frame.Width,frame.Height,GraphicsUnit.Pixel,attributes);}}
    protected override void Dispose(bool disposing){if(disposing){clock.Dispose();if(previous!=null)previous.Dispose();if(incoming!=null)incoming.Dispose();previous=incoming=null;}base.Dispose(disposing);}
}
sealed partial class LibraryForm {
    Panel gameViewport;TableLayoutPanel gameBody;GameTransition gameTransition;
    void CancelGameTransition(){var old=gameTransition;gameTransition=null;if(old!=null){old.Parent=null;old.Dispose();}if(gameBody!=null)gameBody.Invalidate(true);}
    void SwitchGame(){UpdateRobloxSlidePlayback();UpdatePublicArtworkSelection();if(!UiEffects.Enabled){CancelGameTransition();RefreshView();return;}
        if(gameViewport==null||gameBody==null||!Visible||!IsHandleCreated||gameBody.Width<2||gameBody.Height<2){RefreshView();return;}
        Bitmap before=null;try{
            before=GameTransition.CaptureFrame(gameTransition==null?(Control)gameBody:gameTransition);
            GameTransition cover=null;cover=new GameTransition(before,delegate{if(gameTransition==cover)CancelGameTransition();});before=null;gameViewport.Controls.Add(cover);cover.BringToFront();cover.Refresh();CancelGameTransition();gameTransition=cover;
            using(new PageRedraw(gameBody)){RefreshView();gameBody.PerformLayout();}cover.Start(GameTransition.CaptureFrame(gameBody));
        }catch(Exception error){if(before!=null)before.Dispose();CancelGameTransition();RefreshView();AsteraDiagnostics.Write("Game transition fallback",error);}
    }
}
}
