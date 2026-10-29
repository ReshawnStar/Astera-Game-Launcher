using System;
using System.Drawing;
using System.Windows.Forms;
namespace Astera {
sealed partial class LibraryForm {
    GameTransition discoverTransition;
    bool DiscoverResourcesPending(){return discoverImagePending.Count>0||catalogLoadTask!=null&&!catalogLoadTask.IsCompleted;}
    void CancelDiscoverTransition(){var old=discoverTransition;discoverTransition=null;if(old!=null){old.Parent=null;old.Dispose();}}
    void DiscoverEffectsChanged(object sender,EventArgs args){if(!UiEffects.Enabled&&discoverTransition!=null){CancelDiscoverTransition();RenderDiscoverCore();}}
    void TransitionDiscover(){
        Bitmap previous=null;try{
            previous=GameTransition.CaptureFrame(discoverTransition==null?(Control)discoverPage:discoverTransition);CancelDiscoverTransition();GameTransition cover=null;int stable=0;
            cover=new GameTransition(previous,delegate{if(discoverTransition==cover)CancelDiscoverTransition();});previous=null;discoverTransition=cover;discoverPage.Controls.Add(cover);cover.BringToFront();cover.Resize+=delegate{if(discoverTransition==cover)cover.Restage();};
            cover.Start(delegate{if(discoverTransition!=cover)return;RenderDiscoverCore();discoverPage.PerformLayout();cover.BringToFront();},delegate{
                if(discoverTransition!=cover)return null;if(DiscoverResourcesPending()){stable=0;return null;}if(++stable<2)return null;
                // Capture only the prepared page, excluding the opaque transition sibling.
                var image=new Bitmap(discoverPage.Width,discoverPage.Height);try{using(var graphics=Graphics.FromImage(image)){graphics.Clear(discoverPage.BackColor);foreach(Control child in discoverPage.Controls)if(child!=cover&&child.Visible)using(var piece=GameTransition.CaptureFrame(child))graphics.DrawImageUnscaled(piece,child.Left,child.Top);}return image;}catch{image.Dispose();throw;}
            });
        }catch(Exception error){if(previous!=null)previous.Dispose();CancelDiscoverTransition();RenderDiscoverCore();AsteraDiagnostics.Write("Discover transition fallback",error);}
    }
}
}