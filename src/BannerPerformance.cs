using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
namespace Astera {
sealed partial class Banner {
    [DllImport("winmm.dll")]static extern uint timeBeginPeriod(uint period);
    [DllImport("winmm.dll")]static extern uint timeEndPeriod(uint period);
    bool timingRequested,measuring;long frameSequence,paintSequence;
    internal long MeasuredRenders,MeasuredPaints;internal readonly List<double> FrameCosts=new List<double>();
    void AnimationTiming(bool active){if(active&&!timingRequested)timingRequested=timeBeginPeriod(1)==0;else if(!active&&timingRequested){timeEndPeriod(1);timingRequested=false;}}
    internal void BeginPerformanceMeasurement(){MeasuredRenders=MeasuredPaints=0;FrameCosts.Clear();measuring=true;}
    internal void EndPerformanceMeasurement(){measuring=false;}
    void RecordFrame(double milliseconds){frameSequence++;if(measuring){MeasuredRenders++;if(FrameCosts.Count<4096)FrameCosts.Add(milliseconds);}}
    void RecordPaint(Rectangle clip){if(clip.Right<Width*.53||frameSequence==paintSequence)return;paintSequence=frameSequence;if(measuring)MeasuredPaints++;}
}
}
