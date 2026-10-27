using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using MC2Pack;
namespace Astera {
sealed partial class LibraryForm {
    public static int AuditShutdown(string root){try{
        if(Directory.Exists(root))throw new Exception("Use a new isolated shutdown audit directory.");Directory.CreateDirectory(root);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);AsteraDiagnostics.Initialize(root);var checks=new List<string>();
        for(int mode=0;mode<3;mode++){
            var store=new Store(Path.Combine(root,"case-"+mode));store.Settings.CheckOnStart=false;store.Settings.KeepInTray=mode!=0;store.Settings.UseMinecraftSkin=false;store.Settings.Profile="C:/Astera-fixture-unavailable";store.Settings.StarDashExecutable="C:/Astera-fixture-unavailable/StarDash.exe";
            using(var form=new LibraryForm(store,new Channel(),false)){
                form.Show();Application.DoEvents();
                if(mode==1){form.Close();Application.DoEvents();AuditAssert(!form.IsDisposed&&!form.Visible,"Tray close did not preserve the running form");form.ShowWindow();Application.DoEvents();}
                if(mode!=0)form.quitting=true;
                form.Close();Application.DoEvents();AuditAssert(form.IsDisposed,"Normal/update shutdown did not dispose its window");form.Dispose();Application.DoEvents();
            }
            checks.Add(mode==0?"Real normal window closes and repeated Dispose is safe":mode==1?"Tray hide/restore followed by Exit disposes safely":"Launcher-update quitting path closes its real window safely");
        }
        File.WriteAllText(Path.Combine(root,"result.json"),Engine.Json.Serialize(new{Passed=true,Checks=checks,LiveProfileChanged=false}));return 0;
    }catch(Exception error){Directory.CreateDirectory(root);File.WriteAllText(Path.Combine(root,"failure.txt"),error.ToString());return 1;}}
}
}
