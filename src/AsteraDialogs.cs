using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
namespace Astera {
class AsteraDialog:Form {
    [DllImport("user32.dll")]static extern bool ReleaseCapture();
    [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr handle,int message,IntPtr w,IntPtr l);
    bool styled;
    internal AsteraDialog(){FormBorderStyle=FormBorderStyle.None;Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);BackColor=Color.FromArgb(20,26,43);ForeColor=Color.White;Font=UiTheme.Text(14);Padding=new Padding(2);}
    protected override void OnLoad(EventArgs e){if(!styled){styled=true;FormBorderStyle=FormBorderStyle.None;var header=new Panel{Dock=DockStyle.Top,Height=48,Padding=new Padding(10,6,8,6),BackColor=Color.FromArgb(12,17,33)};var icon=new PictureBox{Dock=DockStyle.Left,Width=32,SizeMode=PictureBoxSizeMode.Zoom,Image=Icon.ToBitmap()};var caption=new Label{Text=Text,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,Font=UiTheme.Text(17,true),Padding=new Padding(8,0,0,0)};var close=new AsteraButton{Symbol=ButtonSymbol.Cancel,Role=ButtonRole.Quiet,Text="",AutoSize=false,Dock=DockStyle.Right,Width=36,MinimumSize=new Size(36,36),Padding=new Padding(6),AccessibleName="Close"};close.Click+=delegate{DialogResult=DialogResult.Cancel;Close();};MouseEventHandler drag=delegate(object sender,MouseEventArgs args){if(args.Button==MouseButtons.Left){ReleaseCapture();SendMessage(Handle,0xa1,new IntPtr(2),IntPtr.Zero);}};header.MouseDown+=drag;caption.MouseDown+=drag;icon.MouseDown+=drag;header.Controls.Add(caption);header.Controls.Add(icon);header.Controls.Add(close);Controls.Add(header);header.SendToBack();Disposed+=delegate{icon.Image.Dispose();};}base.OnLoad(e);}
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);using(var border=new Pen(Color.FromArgb(72,84,132)))e.Graphics.DrawRectangle(border,0,0,ClientSize.Width-1,ClientSize.Height-1);}
}
sealed class AsteraMenuColors:ProfessionalColorTable {
    static Color Background {get{return Color.FromArgb(20,26,43);}}static Color Selected {get{return Color.FromArgb(50,56,100);}}
    public override Color ToolStripDropDownBackground {get{return Background;}}public override Color ImageMarginGradientBegin {get{return Background;}}public override Color ImageMarginGradientMiddle {get{return Background;}}public override Color ImageMarginGradientEnd {get{return Background;}}public override Color MenuItemSelected {get{return Selected;}}public override Color MenuItemSelectedGradientBegin {get{return Selected;}}public override Color MenuItemSelectedGradientEnd {get{return Selected;}}public override Color MenuItemBorder {get{return Color.FromArgb(107,116,192);}}public override Color MenuBorder {get{return Color.FromArgb(72,84,132);}}public override Color SeparatorDark {get{return Color.FromArgb(60,70,104);}}public override Color SeparatorLight {get{return Background;}}
}
sealed class AsteraMenuRenderer:ToolStripProfessionalRenderer {
    internal AsteraMenuRenderer():base(new AsteraMenuColors()){RoundedEdges=false;}
    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e){e.TextColor=e.Item.Enabled?Color.FromArgb(238,241,255):Color.FromArgb(140,150,180);base.OnRenderItemText(e);}
    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e){e.ArrowColor=Color.White;base.OnRenderArrow(e);}
    internal static ContextMenuStrip Create(System.ComponentModel.IContainer container){var menu=new ContextMenuStrip(container);Style(menu);return menu;}
    internal static void Style(ContextMenuStrip menu){menu.Renderer=new AsteraMenuRenderer();menu.BackColor=Color.FromArgb(20,26,43);menu.ForeColor=Color.White;menu.Font=UiTheme.Text(14);menu.ShowImageMargin=false;menu.Padding=new Padding(4);}
}
static class UiEffects {static bool enabled=true;internal static event EventHandler Changed;internal static bool Enabled {get{return enabled;}set{if(enabled==value)return;enabled=value;if(Changed!=null)Changed(null,EventArgs.Empty);}}}
}
