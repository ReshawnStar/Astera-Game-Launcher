using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
namespace Astera {
sealed class RoundedSearchFrame:Panel {
    readonly TextBox editor;
    internal RoundedSearchFrame(TextBox input){editor=input;DoubleBuffered=true;Height=36;BackColor=Color.FromArgb(26,34,55);Padding=new Padding(34,9,12,7);editor.BorderStyle=BorderStyle.None;editor.BackColor=BackColor;editor.Dock=DockStyle.Fill;Controls.Add(editor);editor.GotFocus+=delegate{Invalidate();};editor.LostFocus+=delegate{Invalidate();};}
    protected override void OnPaintBackground(PaintEventArgs e){e.Graphics.Clear(Parent==null?Color.FromArgb(12,17,33):Parent.BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var path=AsteraButton.Round(new Rectangle(0,0,Width-1,Height-1),9)){using(var fill=new SolidBrush(BackColor))e.Graphics.FillPath(fill,path);if(editor.Focused)using(var edge=new Pen(ButtonAtlas.AccentColor,1.2f))e.Graphics.DrawPath(edge,path);}using(var pen=new Pen(Color.FromArgb(155,174,204),1.6f)){e.Graphics.DrawEllipse(pen,12,11,9,9);e.Graphics.DrawLine(pen,20,19,24,23);}}
}
sealed class DiscoverTextAction:Button {
    bool hover;
    internal DiscoverTextAction(){AutoSize=false;FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;BackColor=Color.Transparent;ForeColor=Color.FromArgb(187,205,236);Font=UiTheme.Text(13,true);Cursor=Cursors.Hand;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
    protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
    protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(Parent==null?Color.FromArgb(20,27,46):Parent.BackColor);TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,Enabled?(hover?ButtonAtlas.AccentColor:ForeColor):Color.Gray,TextFormatFlags.Right|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix);if(Focused&&ShowFocusCues)using(var pen=new Pen(ButtonAtlas.AccentColor,2))e.Graphics.DrawLine(pen,Width-50,Height-2,Width-2,Height-2);}
}
sealed class DiscoverProjectCard:Panel {
    internal readonly DiscoverArtwork Artwork=new DiscoverArtwork();
    internal readonly Label TitleLabel=new Label(),CategoryLabel=new Label(),Availability=new Label();
    internal readonly DiscoverTextAction Action=new DiscoverTextAction();
    internal Action OpenProject;
    bool hover;
    internal DiscoverProjectCard(){DoubleBuffered=true;SetStyle(ControlStyles.Selectable,true);TabStop=true;Cursor=Cursors.Hand;AccessibleRole=AccessibleRole.PushButton;BackColor=Color.FromArgb(20,27,46);foreach(var label in new[]{TitleLabel,CategoryLabel,Availability}){label.BackColor=BackColor;label.ForeColor=Color.LightSteelBlue;label.Font=UiTheme.Text(13);}TitleLabel.Font=UiTheme.Text(18,true);TitleLabel.ForeColor=Color.White;Controls.AddRange(new Control[]{Artwork,TitleLabel,CategoryLabel,Availability,Action});foreach(Control child in new Control[]{Artwork,TitleLabel,CategoryLabel,Availability}){child.Cursor=Cursors.Hand;child.Click+=delegate{ActivateProject();};child.MouseEnter+=delegate{SetHover(true);};child.MouseLeave+=delegate{SetHover(RectangleToScreen(ClientRectangle).Contains(Cursor.Position));};}Action.MouseEnter+=delegate{SetHover(true);};Action.MouseLeave+=delegate{SetHover(RectangleToScreen(ClientRectangle).Contains(Cursor.Position));};}
    void SetHover(bool value){if(hover==value)return;hover=value;Invalidate();}
    internal void ActivateProject(){if(OpenProject!=null)OpenProject();}
    protected override void OnClick(EventArgs e){base.OnClick(e);ActivateProject();}
    protected override void OnMouseEnter(EventArgs e){SetHover(true);base.OnMouseEnter(e);}protected override void OnMouseLeave(EventArgs e){SetHover(RectangleToScreen(ClientRectangle).Contains(Cursor.Position));base.OnMouseLeave(e);}
    protected override bool IsInputKey(Keys key){return (key&Keys.KeyCode)==Keys.Enter||(key&Keys.KeyCode)==Keys.Space||base.IsInputKey(key);}
    protected override void OnKeyDown(KeyEventArgs e){if(e.KeyCode==Keys.Enter||e.KeyCode==Keys.Space){ActivateProject();e.Handled=true;e.SuppressKeyPress=true;}else base.OnKeyDown(e);}
    protected override void OnGotFocus(EventArgs e){Invalidate();base.OnGotFocus(e);}protected override void OnLostFocus(EventArgs e){Invalidate();base.OnLostFocus(e);}
    internal static int ArtworkHeight(int width){return Math.Max(100,(int)Math.Round(Math.Max(1,width-24)/1.93));}
    protected override void OnResize(EventArgs e){base.OnResize(e);if(Artwork==null)return;int artHeight=ArtworkHeight(Width);Artwork.SetBounds(12,12,Math.Max(1,Width-24),artHeight);TitleLabel.SetBounds(12,artHeight+22,Math.Max(1,Width-24),28);CategoryLabel.SetBounds(12,artHeight+51,Math.Max(1,Width-24),23);Availability.SetBounds(12,Height-41,Math.Max(1,Width-186),29);Action.SetBounds(Math.Max(12,Width-170),Height-43,158,32);Invalidate();}
    protected override void OnPaintBackground(PaintEventArgs e){e.Graphics.Clear(Parent==null?Color.FromArgb(12,17,33):Parent.BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var path=AsteraButton.Round(new Rectangle(0,0,Width-1,Height-1),12)){using(var fill=new SolidBrush(BackColor))e.Graphics.FillPath(fill,path);if(hover||Focused&&ShowFocusCues)using(var edge=new Pen(Color.FromArgb(170,ButtonAtlas.AccentColor),1.2f))e.Graphics.DrawPath(edge,path);}using(var pen=new Pen(Color.FromArgb(47,61,86)))e.Graphics.DrawLine(pen,12,Height-52,Width-12,Height-52);}
}
}
