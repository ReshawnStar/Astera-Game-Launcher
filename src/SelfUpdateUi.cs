using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace Astera {
sealed partial class LibraryForm {
    readonly AsteraButton appUpdateIcon=new AsteraButton();ToolTip appUpdateTip;bool checkingApp;byte[] appEnvelope;AppRelease appRelease;
    void BuildVersionFooter(Control sidebar){
        var footer=new Panel{Dock=DockStyle.Bottom,Height=64,BackColor=sidebar.BackColor};var label=new Label{Text="by Star  ·  v"+SelfUpdate.CurrentVersion,ForeColor=Color.FromArgb(144,155,205),Location=new Point(0,26),Size=new Size(144,24),Font=UiTheme.Text(12)};
        appUpdateIcon.AutoSize=false;appUpdateIcon.MinimumSize=new Size(36,36);appUpdateIcon.Size=new Size(36,36);appUpdateIcon.Location=new Point(146,18);appUpdateIcon.Padding=new Padding(5);appUpdateIcon.UpdateTile=true;appUpdateIcon.Margin=Padding.Empty;appUpdateIcon.Role=ButtonRole.Quiet;appUpdateIcon.Symbol=ButtonSymbol.Refresh;appUpdateIcon.AccessibleName="Check for Astera updates";appUpdateIcon.Click+=async delegate{await RunAsteraUpdate();};
        appUpdateTip=new ToolTip(menuComponents);appUpdateTip.SetToolTip(appUpdateIcon,"Check for Astera updates");footer.Controls.Add(label);footer.Controls.Add(appUpdateIcon);sidebar.Controls.Add(footer);
    }
    string appCheckError;bool appUpdateFlowActive;
    async Task<bool> CheckAsteraUpdates(bool background,CancellationToken token=default(CancellationToken)){if(previewMode||checkingApp||String.IsNullOrWhiteSpace(channel.AppFeedUrl))return false;checkingApp=true;appCheckError=null;appUpdateIcon.Enabled=false;try{var envelope=await Task.Run(()=>SelfUpdate.Check(channel,token));token.ThrowIfCancellationRequested();var release=SelfUpdate.Verify(envelope,channel.PublicKeyXml);if(release.Sequence<store.Settings.HighestAppSequence)throw new Exception("The launcher channel served an older notice");if(IsDisposed)return false;appEnvelope=envelope;appRelease=release;store.Settings.HighestAppSequence=Math.Max(store.Settings.HighestAppSequence,release.Sequence);store.Save();bool ready=SelfUpdate.ParseVersion(release.Version)>SelfUpdate.ParseVersion(SelfUpdate.CurrentVersion);appUpdateIcon.Role=ready?ButtonRole.Primary:ButtonRole.Quiet;appUpdateIcon.Symbol=ready?ButtonSymbol.Download:ButtonSymbol.Refresh;appUpdateIcon.AccessibleName=ready?"Update Astera to "+release.Version:"Check for Astera updates";appUpdateTip.SetToolTip(appUpdateIcon,ready?"Astera "+release.Version+" is ready":"Astera is up to date");LogActivity("app",ready?"Astera "+release.Version+" is ready.":"Astera is up to date.");return ready;}catch(OperationCanceledException){throw;}catch(Exception error){if(token.IsCancellationRequested)throw new OperationCanceledException(token);appCheckError="Could not check for Astera updates. Try again later.";AsteraDiagnostics.Write("Astera update check",error);LogActivity("app",appCheckError);return false;}finally{checkingApp=false;if(!IsDisposed)appUpdateIcon.Enabled=!busy;}}
    async Task<bool> RunAsteraUpdate(bool startup=false,Func<CancellationToken,Task<bool>> checkOverride=null,Func<Action<string,long,long>,CancellationToken,Task<string>> prepareOverride=null,Action<string> restartOverride=null){
        if(appUpdateFlowActive||busy||checkingApp||IsDisposed||checkOverride==null&&(previewMode||String.IsNullOrWhiteSpace(channel.AppFeedUrl)))return false;
        appUpdateFlowActive=true;bool wasEnabled=Enabled,wasTimerEnabled=timer.Enabled;timer.Stop();
        using(var stop=new CancellationTokenSource())using(var dialog=new AppUpdateDialog()){
            dialog.CancelRequested=delegate{stop.Cancel();if(cancellation!=null)cancellation.Cancel();};
            try{dialog.Show(this);Enabled=false;dialog.Activate();dialog.Progress("Checking");bool ready=checkOverride==null?await CheckAsteraUpdates(true,stop.Token):await checkOverride(stop.Token);stop.Token.ThrowIfCancellationRequested();if(!ready){dialog.Progress(appCheckError==null?"Current":"Stopped");if(appCheckError!=null)dialog.Detail.Text=appCheckError;await Task.Delay(startup?300:900);return false;}
                string job=null;Exception failure=null;dialog.Progress("Downloading",0,appRelease==null?0:appRelease.Size);
                if(prepareOverride!=null)job=await prepareOverride(dialog.Progress,stop.Token);
                else await Work(delegate{try{job=SelfUpdate.Prepare(store,appEnvelope,cancellation.Token,Log,dialog.Progress);}catch(Exception error){failure=error;throw;}},true,"app");
                stop.Token.ThrowIfCancellationRequested();if(job==null){dialog.Progress("Stopped");if(failure!=null)dialog.Detail.Text="Update stopped. Check Windows Security if the file was blocked. Your games and settings were preserved.";await Task.Delay(1400);return false;}
                dialog.Committing=true;dialog.Progress("Restarting");await Task.Delay(300);if(restartOverride!=null)restartOverride(job);else SelfUpdate.StartHelper(job);dialog.Finish();if(!IsDisposed)Enabled=wasEnabled;quitting=true;Close();return true;
            }catch(OperationCanceledException){dialog.Progress("Cancelled");return false;}
            catch(Exception error){AsteraDiagnostics.Write("Astera update flow stopped",error);LogActivity("app","Astera update stopped; the current installation was not replaced.");dialog.Progress("Stopped");dialog.Detail.Text="Astera could not finish updating. Check its logs and Windows Security before retrying.";MessageBox.Show(dialog,dialog.Detail.Text,"Astera update stopped",MessageBoxButtons.OK,MessageBoxIcon.Information);return false;}
            finally{dialog.Finish();appUpdateFlowActive=false;if(!IsDisposed){Enabled=wasEnabled;if(wasTimerEnabled)timer.Start();}}
        }
    }
    async Task InstallAsteraUpdate(){await RunAsteraUpdate();}

}
}