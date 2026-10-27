using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
namespace Astera {
// Native read-only text selection/wrapping, with a separately painted scrollbar.
sealed class ScrollText:UserControl {
    const int GetLineCount=0xBA,GetFirstLine=0xCE,LineScroll=0xB6;
    [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr window,int message,IntPtr wParam,IntPtr lParam);
    readonly TextBody body=new TextBody();readonly TextureScrollbar scroll;
    ScrollBars scrollbarMode=ScrollBars.Vertical;bool updating;
    public ScrollText(){SetStyle(ControlStyles.OptimizedDoubleBuffer,true);scroll=new TextureScrollbar(this);body.Multiline=true;body.ReadOnly=true;body.WordWrap=true;body.ScrollBars=ScrollBars.None;body.BorderStyle=BorderStyle.None;body.Dock=DockStyle.Fill;body.TabStop=false;body.HideSelection=true;scroll.Dock=DockStyle.Right;scroll.Width=14;Controls.Add(body);Controls.Add(scroll);body.Wheel=delta=>MoveLines(-delta/120*Math.Max(1,SystemInformation.MouseWheelScrollLines));body.Changed=UpdateScroll;body.TextChanged+=delegate{UpdateScroll();};body.Resize+=delegate{UpdateScroll();};}
    public override string Text {get{return body==null?"":body.Text;}set{if(body==null||body.Text==(value??""))return;body.Text=value??"";UpdateScroll();}}
    public bool ReadOnly {get{return body.ReadOnly;}set{body.ReadOnly=value;}}
    public bool Multiline {get{return body.Multiline;}set{body.Multiline=value;}}
    public bool HideSelection {get{return body.HideSelection;}set{body.HideSelection=value;}}
    public ScrollBars ScrollBars {get{return scrollbarMode;}set{scrollbarMode=value;scroll.Visible=value!=ScrollBars.None;UpdateScroll();}}
    public void AppendText(string text){body.AppendText(text);UpdateScroll();}
    internal int PageLines {get{return Math.Max(1,body.ClientSize.Height/Math.Max(1,body.Font.Height));}}
    internal int FirstLine {get{return body.IsHandleCreated?(int)SendMessage(body.Handle,GetFirstLine,IntPtr.Zero,IntPtr.Zero):0;}}
    internal int Maximum {get{return body.IsHandleCreated?Math.Max(0,(int)SendMessage(body.Handle,GetLineCount,IntPtr.Zero,IntPtr.Zero)-PageLines):0;}}
    internal void MoveLines(int lines){if(!body.IsHandleCreated)return;int from=FirstLine,target=Math.Max(0,Math.Min(Maximum,from+lines));SendMessage(body.Handle,LineScroll,IntPtr.Zero,new IntPtr(target-from));UpdateScroll();}
    internal void MoveTo(int line){MoveLines(line-FirstLine);}
    void UpdateScroll(){if(updating||scroll==null||IsDisposed)return;updating=true;try{scroll.Invalidate();}finally{updating=false;}}
    protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);UpdateScroll();}
    protected override void OnFontChanged(EventArgs e){base.OnFontChanged(e);if(body!=null)body.Font=Font;UpdateScroll();}
    protected override void OnBackColorChanged(EventArgs e){base.OnBackColorChanged(e);if(body!=null)body.BackColor=BackColor;if(scroll!=null)scroll.BackColor=BackColor;}
    protected override void OnForeColorChanged(EventArgs e){base.OnForeColorChanged(e);if(body!=null)body.ForeColor=ForeColor;}
    sealed class TextBody:TextBox {
        public Action<int> Wheel;public Action Changed;
        protected override void WndProc(ref Message message){if(message.Msg==0x20A&&Wheel!=null){Wheel((short)((message.WParam.ToInt64()>>16)&0xffff));return;}base.WndProc(ref message);if(Changed!=null&&(message.Msg==0x101||message.Msg==0x115||message.Msg==0xB1))Changed();}
    }
    sealed class TextureScrollbar:Control {
        readonly ScrollText owner;bool hover,dragging;int grabOffset;
        public TextureScrollbar(ScrollText text){owner=text;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);TabStop=false;Cursor=Cursors.Hand;}
        Rectangle Thumb {get{int maximum=owner.Maximum;if(maximum==0)return Rectangle.Empty;int height=Math.Min(Height-4,Math.Max(28,(int)((Height-4)*(owner.PageLines/(float)(maximum+owner.PageLines))))),range=Math.Max(1,Height-height-4);return new Rectangle(3,2+(int)(range*(owner.FirstLine/(float)maximum)),Math.Max(1,Width-5),height);}}
        protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(BackColor);if(Height<8||Width<6)return;using(var track=new SolidBrush(Color.FromArgb(12,17,31)))e.Graphics.FillRectangle(track,new Rectangle(4,2,Width-7,Height-4));var thumb=Thumb;if(thumb.IsEmpty)return;if(!ButtonAtlas.Draw(e.Graphics,thumb,ButtonRole.Primary,dragging?"pressed":hover?"hover":"normal"))using(var fill=new SolidBrush(ButtonAtlas.AccentColor))e.Graphics.FillRectangle(fill,thumb);}
        protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
        protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
        protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button!=MouseButtons.Left||owner.Maximum==0)return;var thumb=Thumb;if(thumb.Contains(e.Location)){dragging=true;grabOffset=e.Y-thumb.Top;Capture=true;}else owner.MoveLines(e.Y<thumb.Top?-owner.PageLines:owner.PageLines);Invalidate();}
        protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(!dragging)return;var thumb=Thumb;int range=Math.Max(1,Height-thumb.Height-4);owner.MoveTo((int)Math.Round(Math.Max(0,Math.Min(range,e.Y-grabOffset-2))*owner.Maximum/(double)range));}
        protected override void OnMouseUp(MouseEventArgs e){dragging=false;Capture=false;Invalidate();base.OnMouseUp(e);}
        protected override void OnMouseCaptureChanged(EventArgs e){if(!Capture)dragging=false;Invalidate();base.OnMouseCaptureChanged(e);}
        protected override void OnMouseWheel(MouseEventArgs e){owner.MoveLines(-e.Delta/120*Math.Max(1,SystemInformation.MouseWheelScrollLines));base.OnMouseWheel(e);}
    }
}
}
