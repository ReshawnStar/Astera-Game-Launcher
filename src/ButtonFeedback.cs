using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
namespace Astera {
public sealed partial class AsteraButton {
    readonly Timer feedbackTimer=new Timer{Interval=16};
    readonly Stopwatch feedbackClock=new Stopwatch(),hoverClock=new Stopwatch();
    float hoverFrom,hoverTo;
    internal bool FeedbackRunning {get{return feedbackTimer.Enabled;}}
    internal static double Settle(double t){if(t<=0||t>=1)return 0;return Math.Sin(t*Math.PI*2)*Math.Pow(1-t,2);}
    internal static double Ease(double t){t=Math.Max(0,Math.Min(1,t));return 1-Math.Pow(1-t,3);}
    float HoverAmount {get{return !UiEffects.Enabled?(hover?1:0):hoverFrom+(hoverTo-hoverFrom)*(float)Ease(hoverClock.Elapsed.TotalMilliseconds/150);}}
    int FeedbackLift {get{return UiEffects.Enabled&&Enabled?(int)Math.Round(-3*Settle(feedbackClock.Elapsed.TotalMilliseconds/260)):0;}}
    void InitializeFeedback(){feedbackTimer.Tick+=delegate{if(!UiEffects.Enabled||!Visible||!Enabled){StopFeedback();Invalidate();return;}if((!feedbackClock.IsRunning||feedbackClock.ElapsedMilliseconds>=260)&&(!hoverClock.IsRunning||hoverClock.ElapsedMilliseconds>=150))feedbackTimer.Stop();Invalidate();};}
    void StartHover(){float current=HoverAmount;hoverFrom=current;hoverTo=hover?1:0;hoverClock.Restart();StartFeedbackClock();}
    internal void StartFeedback(){if(!UiEffects.Enabled||!Enabled||!Visible||!IsHandleCreated||IsDisposed)return;feedbackClock.Restart();StartFeedbackClock();}
    void StartFeedbackClock(){if(UiEffects.Enabled&&Enabled&&Visible&&IsHandleCreated&&!IsDisposed)feedbackTimer.Start();}
    void StopFeedback(){if(feedbackTimer==null)return;feedbackTimer.Stop();feedbackClock.Reset();hoverClock.Reset();hoverFrom=hoverTo=hover?1:0;}
    protected override void OnClick(EventArgs e){StartFeedback();base.OnClick(e);}
    void DrawHover(Graphics g,Rectangle bounds){float amount=HoverAmount;if(!Enabled||amount<=0||bounds.Width<8||bounds.Height<8)return;using(var path=Round(bounds,8))using(var pen=new Pen(Color.FromArgb((int)(65*amount),ButtonAtlas.AccentColor),1.4F))g.DrawPath(pen,path);}
}
}