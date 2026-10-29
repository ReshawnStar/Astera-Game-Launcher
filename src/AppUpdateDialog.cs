using System;
using System.Drawing;
using System.Windows.Forms;
namespace Astera {
sealed class AppUpdateDialog:AsteraDialog {
    internal readonly Label Heading=new Label{Text="Checking for updates",Dock=DockStyle.Top,Font=UiTheme.Text(20,true)},Detail=new Label{Text="Checking Astera's update channel…",Dock=DockStyle.Top,ForeColor=Color.LightSteelBlue},Stage=new Label{Text="Checking…",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleRight,ForeColor=Color.LightSteelBlue};
    internal readonly WorkMeter Meter=new WorkMeter{Dock=DockStyle.Fill};
    readonly Panel content=new Panel{Dock=DockStyle.Fill,Padding=new Padding(22,18,22,18)},meterSlot=new Panel();
    readonly TableLayoutPanel track=new TableLayoutPanel{Dock=DockStyle.Top,ColumnCount=2,RowCount=1};
    bool mayClose,fitting,ready;internal bool Committing;internal Action CancelRequested;
    internal AppUpdateDialog(){
        Text="Astera update";Size=new Size(600,260);MinimumSize=new Size(580,250);StartPosition=FormStartPosition.CenterParent;ShowInTaskbar=false;
        track.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));track.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,145));track.RowStyles.Add(new RowStyle(SizeType.Percent,100));track.Margin=Padding.Empty;Stage.Margin=Padding.Empty;meterSlot.Dock=DockStyle.Fill;meterSlot.Margin=Padding.Empty;meterSlot.Controls.Add(Meter);track.Controls.Add(meterSlot,0,0);track.Controls.Add(Stage,1,0);
        content.Controls.Add(track);content.Controls.Add(Detail);content.Controls.Add(Heading);Controls.Add(content);ready=true;foreach(var label in new[]{Heading,Detail,Stage}){label.TextChanged+=delegate{FitContent();};label.FontChanged+=delegate{FitContent();};}content.Resize+=delegate{FitContent();};Shown+=delegate{FitContent();};FitContent();
        FormClosing+=delegate(object sender,FormClosingEventArgs e){if(mayClose)return;e.Cancel=true;if(Committing)return;Detail.Text="Cancelling the update check or download…";FitContent();if(CancelRequested!=null)CancelRequested();};
    }

    void FitContent(){
        if(!ready||fitting||content==null||track==null||Heading==null||Detail==null||Stage==null)return;fitting=true;
        try{
            int width=Math.Max(200,content.ClientSize.Width-content.Padding.Horizontal);
            Heading.Height=TextRenderer.MeasureText(Heading.Text,Heading.Font,new Size(width,1000),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix).Height+12;
            Detail.Height=Math.Max(Detail.Font.Height*2,TextRenderer.MeasureText(Detail.Text,Detail.Font,new Size(width,1000),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix).Height)+12;
            int row=Math.Max(36,Stage.Font.Height+16);track.Height=row;track.ColumnStyles[1].Width=Math.Max(115,TextRenderer.MeasureText(Stage.Text,Stage.Font).Width+14);
            int inset=Math.Max(0,(row-8)/2);meterSlot.Padding=new Padding(0,inset,16,row-8-inset);
            int header=0;foreach(Control child in Controls)if(child.Dock==DockStyle.Top)header+=child.Height;
            int needed=header+Padding.Vertical+content.Padding.Vertical+Heading.Height+Detail.Height+row+12;
            if(ClientSize.Height<needed)ClientSize=new Size(ClientSize.Width,needed);
        }finally{fitting=false;}
    }
    internal void Progress(string phase,long done=0,long total=0){
        if(IsDisposed||Disposing)return;if(InvokeRequired){try{BeginInvoke(new Action<string,long,long>(Progress),phase,done,total);}catch(InvalidOperationException){}return;}
        Heading.Text=phase=="Checking"?"Checking for updates":phase=="Current"?"You're up to date":phase=="Stopped"?"Update stopped":"Installing update";
        Detail.Text=phase=="Checking"?"Checking Astera's update channel…":phase=="Downloading"?"Downloading the new Astera update.":phase=="Verifying"?"Verifying the update before changing any files.":phase=="Restarting"?"Astera will close and reopen to finish updating.":phase=="Current"?"You have the latest available version.":phase=="Cancelled"?"The update was cancelled. No installation was started.":phase=="Stopped"?"The update could not finish. Your current installation was preserved.":phase;
        Meter.Fraction=total>0?(double)done/total:phase=="Current"?1:-1;Stage.Text=total>0?((double)done/total*100).ToString("0")+"%":phase=="Restarting"?"Restarting…":phase=="Verifying"?"Verifying…":phase=="Current"?"Done":phase=="Stopped"?"Stopped":phase=="Cancelled"?"Cancelled":"Checking…";FitContent();
    }
    internal void Finish(){mayClose=true;Close();}
}
}
