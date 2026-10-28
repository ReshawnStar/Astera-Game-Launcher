using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using MC2Pack;
namespace Astera {
static class MotionAudit {
    [DllImport("user32.dll")]static extern int GetGuiResources(IntPtr process,int flags);
    static void Require(bool value,string message){if(!value)throw new IOException(message);}
    internal static void Pump(int milliseconds){var clock=Stopwatch.StartNew();while(clock.ElapsedMilliseconds<milliseconds){Application.DoEvents();Thread.Sleep(5);}}
    internal static int Run(string root){bool effects=UiEffects.Enabled;try{
        if(Directory.Exists(root))throw new IOException("Use a new motion audit directory");Directory.CreateDirectory(root);var checks=new List<string>();
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);UiEffects.Enabled=true;
        Require(AsteraButton.Settle(0)==0&&AsteraButton.Settle(1)==0,"Feedback endpoints do not settle");
        for(int i=0;i<=1000;i++){double t=i/1000.0;Require(Math.Abs(AsteraButton.Settle(t))<=1,"Feedback exceeded bounded range");if(i>0)Require(AsteraButton.Ease(t)>=AsteraButton.Ease(t-.001),"Fade easing reversed");}checks.Add("Bounded feedback settles exactly; fade easing is monotonic");
        using(var form=new Form{Size=new Size(430,190),BackColor=Color.FromArgb(12,17,33),ShowInTaskbar=false})using(var button=new AsteraButton{Text="PLAY",Size=new Size(175,44),AutoSize=false,Location=new Point(45,50),Role=ButtonRole.Primary}){
            form.Controls.Add(button);form.Show();Pump(80);var bounds=button.Bounds;int clicks=0;button.Click+=delegate{clicks++;};
            button.PerformClick();Require(button.FeedbackRunning&&clicks==1,"Click did not start bounded feedback exactly once");Pump(45);using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,form.Size));image.Save(Path.Combine(root,"feedback.png"));}Pump(350);Require(!button.FeedbackRunning&&button.Bounds==bounds,"Feedback did not stop or moved hit target");checks.Add("Click feedback preserves click count and hit target; timer stops after settle");
            button.StartFeedback();UiEffects.Enabled=false;Require(!button.FeedbackRunning,"Effects off kept feedback active");button.PerformClick();Require(!button.FeedbackRunning&&clicks==2,"Reduced motion broke clicks or started feedback");checks.Add("Appearance effects toggle stops animation immediately without changing button actions");
            UiEffects.Enabled=true;button.StartFeedback();form.Hide();Require(!button.FeedbackRunning,"Hidden window kept feedback active");form.Show();Pump(30);button.StartFeedback();button.Enabled=false;Require(!button.FeedbackRunning,"Disabled control kept feedback active");button.Enabled=true;checks.Add("Hidden and disabled buttons stop feedback");
            int before=GetGuiResources(Process.GetCurrentProcess().Handle,0);for(int i=0;i<70;i++){button.StartFeedback();using(var image=new Bitmap(button.Width,button.Height))button.DrawToBitmap(image,button.ClientRectangle);}Pump(400);int after=GetGuiResources(Process.GetCurrentProcess().Handle,0);Require(after-before<=5,"Repeated feedback leaked GDI objects");checks.Add("Repeated feedback redraws retain bounded GDI resources");
            var process=Process.GetCurrentProcess();var cpu=process.TotalProcessorTime;var wall=Stopwatch.StartNew();Pump(600);var idleCpu=(process.TotalProcessorTime-cpu).TotalMilliseconds;Require(!button.FeedbackRunning,"Idle feedback timer restarted");
            long idleWall=wall.ElapsedMilliseconds;button.Role=ButtonRole.Secondary;button.Role=ButtonRole.Primary;Require(button.FeedbackRunning,"Selected tab did not start feedback");Pump(350);typeof(AsteraButton).GetMethod("OnMouseEnter",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(button,new object[]{EventArgs.Empty});Require(button.FeedbackRunning,"Hover did not start feedback");Pump(350);Require(!button.FeedbackRunning,"Hover-only timer did not stop");typeof(AsteraButton).GetMethod("OnMouseLeave",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(button,new object[]{EventArgs.Empty});Pump(350);Require(!button.FeedbackRunning,"Hover exit did not stop");checks.Add("Selected buttons respond; hover-only enter and exit timers both settle");int finished=0;using(var old=new Bitmap(300,120))using(var next=new Bitmap(300,120)){using(var g=Graphics.FromImage(old))g.Clear(Color.DarkBlue);using(var g=Graphics.FromImage(next))g.Clear(Color.DarkRed);using(var transition=new GameTransition(new Bitmap(old),delegate{finished++;})){form.Controls.Add(transition);transition.BringToFront();transition.Start(new Bitmap(next));Pump(70);using(var image=new Bitmap(transition.Width,transition.Height)){transition.DrawToBitmap(image,transition.ClientRectangle);image.Save(Path.Combine(root,"transition.png"));}Pump(1550);Require(finished==1,"Transition did not finish exactly once");Pump(100);Require(finished==1,"Transition timer continued after completion");form.Controls.Remove(transition);}}checks.Add("Covered transition completion fires once and stops its timer");
            LibraryForm.AuditCoveredPreparation(Path.Combine(root,"covered-page"),checks);File.WriteAllText(Path.Combine(root,"result.json"),Engine.Json.Serialize(new{Passed=true,Checks=checks,IdleSampleMilliseconds=idleWall,IdleCpuMilliseconds=idleCpu,GdiBefore=before,GdiAfter=after,LiveSettingsChanged=false}));form.Close();
        }return 0;
    }catch(Exception error){Directory.CreateDirectory(root);File.WriteAllText(Path.Combine(root,"failure.txt"),error.ToString());return 1;}finally{UiEffects.Enabled=effects;}}
}
}