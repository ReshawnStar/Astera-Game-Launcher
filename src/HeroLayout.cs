using System;
using System.Drawing;
using System.Windows.Forms;
namespace Astera {
sealed partial class LibraryForm {
    TableLayoutPanel heroTextPanel;FlowLayoutPanel heroActions;bool layingOutHero;
    void LayoutHeroContent(){
        if(layingOutHero||heroTextPanel==null||heroActions==null||gameViewport==null)return;
        layingOutHero=true;try{
            bool minecraft=GameKind==0;
            heroTextPanel.Width=Math.Max(400,(int)(banner.Width*.57));
            int subtitle=minecraft?TextRenderer.MeasureText(heroSubtitle.Text,heroSubtitle.Font,new Size(Math.Max(100,heroTextPanel.Width-8),1000),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix).Height+6:40;
            int tagline=minecraft?26:0;heroTagline.Visible=minecraft;
            heroTextPanel.RowStyles[1].Height=subtitle;heroTextPanel.RowStyles[2].Height=tagline;
            heroTextPanel.RowStyles[3].Height=heroTextPanel.RowStyles[4].Height=0;profileLabel.Visible=false;
            int height=56+subtitle+tagline;heroTextPanel.Height=height;
            int normal=gameViewport.ClientSize.Height<740?280:330;bool modded=modProfileStrip!=null&&modProfileStrip.Visible;
            gameBody.RowStyles[0].Height=minecraft?Math.Max(normal,height+154):normal+(modded?42:0);
            heroActions.Top=banner.Height-72;heroActions.Width=banner.Width-40;LayoutPlaytimeStats();
            heroTextPanel.Top=minecraft?Math.Max(12,heroActions.Top-height-40):banner.Height<300?44:66;
            string message=(status.Text??"").ToLowerInvariant();
            status.BackColor=Color.Transparent;status.Visible=!busy&&!appUpdateFlowActive&&(message.Contains("not found")||message.Contains("unavailable")||message.Contains("missing")||message.Contains("error")||message.Contains("failed"));

            status.SetBounds(24,heroActions.Top-26,Math.Max(100,banner.Width-48),22);
        }finally{layingOutHero=false;}
    }
}
}