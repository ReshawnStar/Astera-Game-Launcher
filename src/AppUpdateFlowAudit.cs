using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace Astera {
sealed partial class LibraryForm {
    static void FinishUpdateAudit(Task task){var deadline=System.Diagnostics.Stopwatch.StartNew();while(!task.IsCompleted&&deadline.ElapsedMilliseconds<6000)MotionAudit.Pump(10);AuditAssert(task.IsCompleted,"Update presentation timed out");task.GetAwaiter().GetResult();}
    internal static int AuditAppUpdateFlow(string root){try{
        if(Directory.Exists(root))throw new IOException("Use a fresh update-flow audit");Directory.CreateDirectory(root);AsteraDiagnostics.Initialize(root);Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        var checks=new List<string>();var store=new Store(Path.Combine(root,"state"));store.Settings.CheckOnStart=false;store.Settings.CheckAppOnStart=false;store.Settings.KeepInTray=false;store.Settings.UseMinecraftSkin=false;
        Func<LibraryForm> create=delegate{var form=new LibraryForm(store,new Channel(),true);form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new System.Drawing.Point(-30000,-30000);form.Show();return form;};
        using(var form=create()){
            bool timerBefore=form.timer.Enabled;
            var task=form.RunAsteraUpdate(false,delegate(CancellationToken token){AuditAssert(!form.Enabled&&!form.timer.Enabled,"Owner and background checks remained active during update");var dialog=Application.OpenForms.OfType<AppUpdateDialog>().Single();AuditAssert(dialog.Visible&&dialog.Heading.Text=="Checking for updates","Checking presentation was missing");AuditAssert(form.RunAsteraUpdate().Result==false,"Duplicate update flow started");return Task.FromResult(false);});
            FinishUpdateAudit(task);AuditAssert(!task.Result&&form.Enabled&&form.timer.Enabled==timerBefore&&!form.appUpdateFlowActive,"Up-to-date check failed to restore the library");checks.Add("Checking uses one compact dialog, prevents duplicate starts and pauses/restores background checks");
            task=form.RunAsteraUpdate(false,delegate(CancellationToken token){Application.OpenForms.OfType<AppUpdateDialog>().Single().Close();return Task.FromResult(false);});FinishUpdateAudit(task);AuditAssert(!task.Result&&form.Enabled&&!form.appUpdateFlowActive,"Cancellation left the library disabled");checks.Add("Closing the update window cancels before any installation and restores the owner");
            task=form.RunAsteraUpdate(false,delegate(CancellationToken token){form.appCheckError="Offline fixture";return Task.FromResult(false);});MotionAudit.Pump(50);AuditAssert(Application.OpenForms.OfType<AppUpdateDialog>().Single().Heading.Text=="Update stopped","Failed check was reported as up to date");FinishUpdateAudit(task);AuditAssert(form.Enabled,"Failed check trapped the owner");form.appCheckError=null;checks.Add("A failed check displays stopped status and returns to the usable library");
            task=form.RunAsteraUpdate(false,delegate(CancellationToken token){return Task.FromResult(true);},delegate(Action<string,long,long> progress,CancellationToken token){progress("Downloading",50,100);var dialog=Application.OpenForms.OfType<AppUpdateDialog>().Single();AuditAssert(dialog.Stage.Text=="50%","Real download bytes were not presented");progress("Verifying",0,0);AuditAssert(dialog.Stage.Text=="Verifying…","Verification phase missing");return Task.FromResult<string>(null);});FinishUpdateAudit(task);AuditAssert(!task.Result&&form.Enabled,"An unprepared update attempted restart");checks.Add("Download progress and verification render in the same dialog; absent prepared job never restarts");
            form.Close();
        }
        using(var form=create()){
            int restartCount=0;var task=form.RunAsteraUpdate(false,delegate(CancellationToken token){return Task.FromResult(true);},delegate(Action<string,long,long> progress,CancellationToken token){return Task.FromResult("isolated-job-fixture");},delegate(string job){AuditAssert(job=="isolated-job-fixture","Prepared job changed");restartCount++;});FinishUpdateAudit(task);AuditAssert(task.Result&&restartCount==1&&form.IsDisposed,"Prepared update did not hand off exactly once and close");checks.Add("A prepared update hands off once and closes without a second confirmation prompt");
        }
        File.WriteAllText(Path.Combine(root,"result.json"),SelfUpdateFiles.Json.Serialize(new{Passed=true,Checks=checks,RealNetworkRequests=false,LiveAppReplaced=false}));return 0;
    }catch(Exception error){Directory.CreateDirectory(root);File.WriteAllText(Path.Combine(root,"failure.txt"),error.ToString());return 1;}}
}
}
