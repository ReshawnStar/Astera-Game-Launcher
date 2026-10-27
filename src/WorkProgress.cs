using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
namespace Astera {
sealed class WorkMeter:Control {
    internal double Fraction=-1;
    public WorkMeter(){DoubleBuffered=true;Height=8;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
    protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(Color.FromArgb(31,40,64));using(var brush=new SolidBrush(Fraction<0?Color.FromArgb(100,ButtonAtlas.AccentColor):ButtonAtlas.AccentColor)){e.Graphics.FillRectangle(brush,0,0,Fraction<0?Width:(int)(Width*Math.Max(0,Math.Min(1,Fraction))),Height);}}
}
sealed partial class LibraryForm {
    readonly Panel workPanel=new Panel{Dock=DockStyle.Bottom,Height=28,Visible=false};readonly Label workLabel=new Label{Dock=DockStyle.Top,Height=20,ForeColor=Color.LightSteelBlue};readonly WorkMeter workMeter=new WorkMeter{Dock=DockStyle.Bottom};string workPhase="Working";long workDone,workTotal,lastProgressTicks;
    internal static bool NeedsProfileCheck(Preferences before,Preferences after){return !String.IsNullOrWhiteSpace(after.Profile)&&(!String.Equals(before.Profile,after.Profile,StringComparison.OrdinalIgnoreCase)||!String.Equals(before.FeedUrl,after.FeedUrl,StringComparison.Ordinal));}
    void BuildWorkProgress(TableLayoutPanel body){var footer=new Panel{Dock=DockStyle.Fill,Margin=Padding.Empty};releaseLabel.Dock=DockStyle.Fill;workPanel.Controls.Add(workLabel);workPanel.Controls.Add(workMeter);footer.Controls.Add(releaseLabel);footer.Controls.Add(workPanel);body.Controls.Add(footer,0,3);}
    void UpdateWorkProgress(){workPanel.Visible=busy;if(gameBody!=null)gameBody.RowStyles[3].Height=busy?58:28;workMeter.Fraction=workTotal>0?(double)workDone/workTotal:-1;workMeter.Invalidate();workLabel.Text=workPhase+(workTotal>0?" · "+((double)workDone/workTotal*100).ToString("0")+"% · "+(workPhase=="Installing"?workDone+" / "+workTotal+" files":(workDone/1048576D).ToString("0.0")+" / "+(workTotal/1048576D).ToString("0.0")+" MiB"):"…");}
    void ReportWorkProgress(string phase,long done,long total){if(IsDisposed||Disposing)return;long now=DateTime.UtcNow.Ticks;if(done>0&&done<total&&now-Interlocked.Read(ref lastProgressTicks)<TimeSpan.TicksPerMillisecond*200)return;Interlocked.Exchange(ref lastProgressTicks,now);if(InvokeRequired){try{BeginInvoke(new Action<string,long,long>(SetWorkProgress),phase,done,total);}catch(InvalidOperationException){}return;}SetWorkProgress(phase,done,total);}
    void SetWorkProgress(string phase,long done,long total){if(IsDisposed||Disposing)return;workPhase=phase;workDone=done;workTotal=total;UpdateWorkProgress();}
}
}
