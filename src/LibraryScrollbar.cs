using System;
using System.Drawing;
using System.Windows.Forms;
namespace Astera {
// Keep native list selection/keyboard behaviour. Clip its Windows scrollbar out
// of the viewport and paint a separate thumb, without compositing native HWNDs.
sealed class GameListBox:ListBox {
    internal Action ViewChanged;
    protected override void WndProc(ref Message m){base.WndProc(ref m);if(ViewChanged!=null&&(m.Msg==0x20A||m.Msg==0x115||m.Msg==0x100||m.Msg==0x101||m.Msg==0x197||m.Msg==0x185||m.Msg==0x5))ViewChanged();}
}
sealed class LibraryScrollbar:Control {
    readonly GameListBox list;readonly Control viewport;bool hover,dragging;int grab;
    internal LibraryScrollbar(GameListBox gameList,Control clip){list=gameList;viewport=clip;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);TabStop=false;Cursor=Cursors.Hand;}
    internal int Page {get{return Math.Max(1,list.ClientSize.Height/Math.Max(1,list.ItemHeight));}}
    internal int Maximum {get{return Math.Max(0,list.Items.Count-Page);}}
    internal void MoveTo(int index){if(list.Items.Count==0)return;int next=Math.Max(0,Math.Min(Maximum,index));if(list.TopIndex!=next)list.TopIndex=next;Invalidate();}
    internal Rectangle Thumb {get{if(Maximum==0||Height<8)return Rectangle.Empty;int h=Math.Min(Height-4,Math.Max(28,(int)((Height-4)*Page/(float)list.Items.Count)));return new Rectangle(3,2+(int)((Height-h-4)*Math.Min(Maximum,list.TopIndex)/(float)Maximum),Math.Max(1,Width-6),h);}}
    protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(BackColor);if(Maximum==0||Height<8)return;using(var brush=new SolidBrush(Color.FromArgb(12,17,31)))e.Graphics.FillRectangle(brush,4,2,Math.Max(1,Width-8),Height-4);var thumb=Thumb;if(!ButtonAtlas.Draw(e.Graphics,thumb,ButtonRole.Primary,dragging?"pressed":hover?"hover":"normal"))using(var brush=new SolidBrush(ButtonAtlas.AccentColor))e.Graphics.FillRectangle(brush,thumb);}
    protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
    protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
    protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button!=MouseButtons.Left||Maximum==0)return;var thumb=Thumb;if(thumb.Contains(e.Location)){dragging=true;grab=e.Y-thumb.Y;Capture=true;}else MoveTo(list.TopIndex+(e.Y<thumb.Y?-Page:Page));}
    protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(!dragging)return;int range=Math.Max(1,Height-Thumb.Height-4);MoveTo((int)Math.Round(Math.Max(0,Math.Min(range,e.Y-grab-2))*Maximum/(double)range));}
    protected override void OnMouseUp(MouseEventArgs e){dragging=false;Capture=false;Invalidate();base.OnMouseUp(e);}
    protected override void OnMouseCaptureChanged(EventArgs e){if(!Capture)dragging=false;base.OnMouseCaptureChanged(e);}
    protected override void OnMouseWheel(MouseEventArgs e){MoveTo(list.TopIndex-e.Delta/120*Math.Max(1,SystemInformation.MouseWheelScrollLines));base.OnMouseWheel(e);}
}
sealed partial class LibraryForm {
    Panel libraryListHost,libraryListClip;LibraryScrollbar libraryScrollbar;
    void BuildLibraryViewport(Control sidebar){games.Dock=DockStyle.None;games.IntegralHeight=false;libraryListHost=new Panel{Dock=DockStyle.Fill,BackColor=sidebar.BackColor};libraryListClip=new Panel{Dock=DockStyle.Fill,BackColor=sidebar.BackColor};libraryScrollbar=new LibraryScrollbar(games,libraryListClip){Dock=DockStyle.Right,Width=18,BackColor=sidebar.BackColor,Margin=Padding.Empty};libraryListHost.Controls.Add(libraryListClip);libraryListHost.Controls.Add(libraryScrollbar);libraryListClip.Controls.Add(games);libraryListClip.Resize+=delegate{games.Bounds=new Rectangle(0,0,libraryListClip.ClientSize.Width+SystemInformation.VerticalScrollBarWidth,libraryListClip.ClientSize.Height);libraryScrollbar.Invalidate();};games.ViewChanged=delegate{if(libraryScrollbar!=null&&!libraryScrollbar.IsDisposed)libraryScrollbar.Invalidate();UpdateIconAnimationPlayback();};sidebar.Controls.Add(libraryListHost);libraryListHost.BringToFront();}
    int LibraryRowRight {get{return libraryListClip==null?games.ClientSize.Width:libraryListClip.ClientSize.Width;}}
}
}
