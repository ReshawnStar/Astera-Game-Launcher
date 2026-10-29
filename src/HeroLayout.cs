using System;
using System.Drawing;
using System.Windows.Forms;
namespace Astera {
sealed partial class LibraryForm {
    TableLayoutPanel heroTextPanel;FlowLayoutPanel heroActions;bool layingOutHero;
    void LayoutHeroContent(){if(layingOutHero||heroTextPanel==null||heroActions==null||gameViewport==null)return;layingOutHero=true;try{bool minecraft=GameKind==0;heroTextPanel.Width=Math.Max(400,(int)(banner.Width*.57));int subtitle=minecraft?TextRenderer.MeasureText(heroSubtitle.Text,heroSubtitle.Font,new Size(Math.Max(100,heroTextPanel.Width-8),1000),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix).Height+6:40;int tagline=minecraft?26:0;heroTagline.Visible=minecraft;heroTextPanel.RowStyles[1].Height=subtitle;heroTextPanel.RowStyles[2].Height=tagline;int height=56+subtitle+tagline+28+28;heroTextPanel.Height=height;int normal=gameViewport.ClientSize.Height<740?280:330;bool modded=modProfileStrip!=null&&modProfileStrip.Visible;gameBody.RowStyles[0].Height=minecraft?Math.Max(normal,height+98):normal+(modded?42:0);heroActions.Top=banner.Height-72;heroActions.Width=banner.Width-40;LayoutPlaytimeStats();heroTextPanel.Top=minecraft?Math.Max(12,heroActions.Top-height-12):banner.Height<300?44:66;if(modded){LayoutModProfileStrip();heroTextPanel.Top=Math.Max(12,Math.Min(heroTextPanel.Top,modProfileStrip.Top-height-8));}}finally{layingOutHero=false;}}
}
}