using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using MC2Pack;
namespace Astera {
sealed partial class LibraryForm {
    internal static int AuditPerformance(string directory,string pose){
        directory=Path.GetFullPath(directory);if(Directory.Exists(directory))return 2;Directory.CreateDirectory(directory);Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        var state=new Store(Path.Combine(directory,"preferences"));state.Settings.CheckOnStart=false;state.Settings.KeepInTray=false;
        using(var form=new LibraryForm(state,new Channel(),true))using(var sequence=new Timer{Interval=1000})using(var process=Process.GetCurrentProcess()){
            form.minecraftSkinArt=Path.GetFullPath(pose);form.UpdateLayoutView();if(!form.banner.LiveSkinActive)throw new Exception("Performance rig not active");form.TopMost=true;
            int step=0;long lastPaint=0;double lastTime=0,cpuStart=0;var watch=new Stopwatch();var windows=new List<object>();var rates=new List<double>();Exception failure=null;long peak=0;
            sequence.Tick+=delegate{try{
                if(step++==0){form.banner.BeginPerformanceMeasurement();cpuStart=process.TotalProcessorTime.TotalMilliseconds;watch.Start();return;}
                double now=watch.Elapsed.TotalMilliseconds;long painted=form.banner.MeasuredPaints;double fps=(painted-lastPaint)*1000/(now-lastTime);rates.Add(fps);windows.Add(new{Window=step-1,PaintFps=fps,Size=form.ClientSize.ToString()});lastTime=now;lastPaint=painted;process.Refresh();peak=Math.Max(peak,process.WorkingSet64);
                if(step==5)form.Size=new Size(1100,760);if(step==9)form.ToggleMaximize();
                if(step==13){sequence.Stop();form.banner.EndPerformanceMeasurement();var costs=form.banner.FrameCosts.OrderBy(x=>x).ToArray();double cpu=process.TotalProcessorTime.TotalMilliseconds-cpuStart;
                    File.WriteAllText(Path.Combine(directory,"result.json"),Engine.Json.Serialize(new{Passed=rates.Min()>=60,Metric="Distinct animated banner frames painted by the UI, not hardware/DWM presentation",Windows=windows,MinimumPaintFps=rates.Min(),AveragePaintFps=form.banner.MeasuredPaints*1000/watch.Elapsed.TotalMilliseconds,RenderMeanMs=costs.Average(),RenderP95Ms=costs[(int)((costs.Length-1)*.95)],RenderMaxMs=costs.Max(),CpuPercentOfMachine=100*cpu/watch.Elapsed.TotalMilliseconds/Environment.ProcessorCount,PeakWorkingSetBytes=peak,LivePreferencesChanged=false}));form.Close();}
            }catch(Exception error){failure=error;sequence.Stop();form.Close();}};
            form.Shown+=delegate{form.Activate();sequence.Start();};Application.Run(form);if(failure!=null){File.WriteAllText(Path.Combine(directory,"failure.txt"),failure.ToString());return 1;}return 0;
        }
    }
}
}
