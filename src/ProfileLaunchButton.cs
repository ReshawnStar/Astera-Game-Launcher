using System;
using System.Drawing;
using System.Windows.Forms;
namespace Astera {
sealed class ProfileLaunchButton:Button {
    internal string ProfileName="Vanilla";
    internal ProfileLaunchButton(){AutoSize=false;TabStop=true;FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;BackColor=Color.FromArgb(38,48,80);ForeColor=Color.White;Cursor=Cursors.Hand;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
    protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(BackColor);using(var small=UiTheme.Text(9))using(var name=UiTheme.Text(14,true)){TextRenderer.DrawText(e.Graphics,"PLAY PROFILE",small,new Rectangle(10,5,Width-20,14),Color.FromArgb(167,180,205),TextFormatFlags.NoPrefix);TextRenderer.DrawText(e.Graphics,ProfileName,name,new Rectangle(10,20,Width-36,24),Enabled?Color.White:Color.Gray,TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);}using(var pen=new Pen(Enabled?Color.LightSteelBlue:Color.Gray,1.5f)){int x=Width-18,y=Height/2+4;e.Graphics.DrawLine(pen,x-4,y-2,x,y+2);e.Graphics.DrawLine(pen,x,y+2,x+4,y-2);}if(Focused&&ShowFocusCues)using(var focus=new Pen(ButtonAtlas.AccentColor,1.5f)){e.Graphics.DrawRectangle(focus,2,2,Width-5,Height-5);}}
}
sealed partial class LibraryForm {
    AsteraDialog BuildProfileCodeDialog(ModProfileLink selected,out TextBox input){
        var dialog=new AsteraDialog{Text=selected==null?"Import a mod profile":"Update "+selected.Name,Size=new Size(610,270),StartPosition=FormStartPosition.CenterParent};
        var content=new Panel{Dock=DockStyle.Fill,Padding=new Padding(18,14,18,14)};
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3};layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,44));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,42));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        var label=new Label{Dock=DockStyle.Fill,Text="Paste the profile share code from Thunderstore or r2modman."};var field=new TextBox{Dock=DockStyle.Top};input=field;
        var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,WrapContents=false};var go=Button("Check profile",delegate{try{field.Text=ThunderstoreProfiles.Code(field.Text);dialog.DialogResult=DialogResult.OK;}catch(Exception error){MessageBox.Show(dialog,error.Message,"Profile code");}});actions.Controls.Add(go);
        layout.Controls.Add(label,0,0);layout.Controls.Add(field,0,1);layout.Controls.Add(actions,0,2);content.Controls.Add(layout);dialog.Controls.Add(content);dialog.AcceptButton=go;return dialog;
    }
}
}
