using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace Astera {
sealed partial class LibraryForm {
    CatalogProject NebulaProject {get{return projectCatalog==null?null:projectCatalog.Projects.FirstOrDefault(p=>p.Id==ManagedGames.Id);}}
    CustomGame ManagedNebula {get{return store.Settings.CustomGames.FirstOrDefault(g=>g.ManagedProjectId==ManagedGames.Id);}}
    bool ManagedUpdateReady(CustomGame game){var project=NebulaProject;if(game==null||project==null||project.Package==null||!ManagedInstalled(game))return false;var state=ManagedGames.State(Path.GetDirectoryName(game.Executable));Version have,want;return state!=null&&Version.TryParse(state.Version,out have)&&Version.TryParse(project.Package.Version,out want)&&want>have;}
    async Task InstallManagedProject(CatalogProject project){
        if(busy||project==null||project.Id!=ManagedGames.Id||project.Package==null)return;ManagedGames.Validate(project.Package);var existing=ManagedNebula;
        if(!ManagedInstalled(existing)){
            string found=await DiscoverExistingManaged(project);if(IsDisposed)return;
            if(found!=null){UseExistingManaged(found);return;}
        }
        string root=RetainedManagedRoot(),archive=null;var package=project.Package;
        if(String.IsNullOrEmpty(package.Url)){using(var picker=new OpenFileDialog{Title="Choose the Gacha Nebula + release ZIP",Filter="Game release ZIP (*.zip)|*.zip",CheckFileExists=true}){if(picker.ShowDialog(this)!=DialogResult.OK)return;archive=picker.FileName;}}
        if(!ManagedInstalled(existing)){root=ReviewManagedTarget(root,project);if(root==null)return;if(ManagedLibrary.VerifiedExisting(root,package)){UseExistingManaged(root);return;}}
        if(MessageBox.Show(this,"Install Gacha Nebula + "+package.Version+"?\r\n\r\n"+root+"\r\n\r\nYour save profile and Recovery backups are preserved. Close the game and its tray helper first.","Install Gacha Nebula +",MessageBoxButtons.YesNo,MessageBoxIcon.Information)!=DialogResult.Yes)return;
        string executable=null;string target=root;
        using(var progressDialog=new AppUpdateDialog{Text="Install Gacha Nebula +"})using(var monitor=new System.Windows.Forms.Timer{Interval=100}){bool wasEnabled=Enabled;progressDialog.CancelRequested=delegate{if(cancellation!=null)cancellation.Cancel();};monitor.Tick+=delegate{PresentManagedInstall(progressDialog,workPhase,workDone,workTotal);};try{PresentManagedInstall(progressDialog,"Preparing",0,0);progressDialog.Show(this);Enabled=false;monitor.Start();
        await Work(delegate{
            if(archive==null){string cache=Path.Combine(store.Root,"managed-game-downloads");Directory.CreateDirectory(cache);archive=Path.Combine(cache,package.Hash.ToLowerInvariant()+".zip");if(!File.Exists(archive)||new FileInfo(archive).Length!=package.Bytes||!MC2Pack.Engine.Hash(archive).Equals(package.Hash,StringComparison.OrdinalIgnoreCase)){string temporary=archive+"."+Guid.NewGuid().ToString("N")+".part";try{using(var output=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))new Transport().Get(package.Url,output,package.Bytes,package.Bytes,cancellation.Token,done=>ReportWorkProgress("Downloading Gacha Nebula +",done,package.Bytes));if(!MC2Pack.Engine.Hash(temporary).Equals(package.Hash,StringComparison.OrdinalIgnoreCase))throw new IOException("Game download hash mismatch");if(File.Exists(archive))File.Delete(archive);File.Move(temporary,archive);}finally{if(File.Exists(temporary))File.Delete(temporary);}}}
            ReportWorkProgress("Verifying Gacha Nebula +",0,0);executable=ManagedGames.Install(archive,package,target,cancellation.Token,ReportWorkProgress,()=>RunningGames.Check(new CustomGame{Executable=Path.Combine(target,ManagedGames.Launcher),ManagedProjectId=ManagedGames.Id},null));
        },false,"project:"+ManagedGames.Id);
        }finally{monitor.Stop();progressDialog.Finish();if(!IsDisposed)Enabled=wasEnabled;}}
        if(executable==null||IsDisposed)return;var game=RegisterManagedNebula(executable);ReloadGames(game);SelectPrimaryPage(0);RefreshView();LogActivity("custom:"+game.Id,"Gacha Nebula + "+package.Version+" is ready to play.");
    }
    internal static void PresentManagedInstall(AppUpdateDialog dialog,string phase,long done,long total){dialog.Heading.Text="Installing Gacha Nebula +";dialog.Detail.Text=total>0?(phase.StartsWith("Downloading",StringComparison.Ordinal)?"Downloading the game. ":"Installing the game files. ")+(done/1048576D).ToString("0.0")+" / "+(total/1048576D).ToString("0.0")+" MiB":phase.StartsWith("Verifying",StringComparison.Ordinal)?"Checking the release before installation.":"Preparing installation. Your saves and backups stay intact.";dialog.Meter.Fraction=total>0?(double)done/total:-1;dialog.Stage.Text=total>0?((double)done/total*100).ToString("0")+"%":"Working…";}
    CustomGame RegisterManagedNebula(string executable){var game=ManagedNebula??store.Settings.CustomGames.FirstOrDefault(g=>String.Equals(g.Executable,executable,StringComparison.OrdinalIgnoreCase));if(game==null){game=new CustomGame{Name="Gacha Nebula +"};store.Settings.CustomGames.Add(game);}game.ManagedProjectId=ManagedGames.Id;game.Executable=executable;game.Platform="";game.PlatformId="";RememberManaged(Path.GetDirectoryName(executable),true);if(String.IsNullOrEmpty(game.Background)){string directory=Path.Combine(store.Root,"managed-game-artwork");Directory.CreateDirectory(directory);string path=Path.Combine(directory,"gacha-nebula-plus.png");if(!File.Exists(path))using(var image=GameArtwork.Load("Astera.Backgrounds.GachaNebulaPlus"))image.Save(path,System.Drawing.Imaging.ImageFormat.Png);game.Background=path;}store.Save();return game;}
    void RefreshManagedPresentation(CustomGame game){if(game.ManagedProjectId!=ManagedGames.Id)return;var project=NebulaProject;bool installed=ManagedInstalled(game),update=ManagedUpdateReady(game);if(!installed&&!busy&&!GameRunning(game)&&!GameStarting(game)){play.Text="INSTALL";((AsteraButton)play).Symbol=ButtonSymbol.Download;play.Enabled=project!=null&&project.Package!=null;}if(update&&!GameRunning(game)&&!GameStarting(game)){play.Text="UPDATE";((AsteraButton)play).Symbol=ButtonSymbol.Download;}check.Text="Check for updates";check.Enabled=!busy;heroSubtitle.Text=project==null?"A new chapter in character creation.":project.Summary;latestHeading.Text="Latest update";notes.Text=project==null||String.IsNullOrWhiteSpace(project.Notes)?"No patch notes available yet.":project.Notes;LayoutHeroContent();}
    async Task CheckManagedProject(){if(busy||CurrentCustom==null)return;var game=CurrentCustom;check.Enabled=false;try{await RefreshDiscoverCatalog(true);if(!IsDisposed){LogActivity("custom:"+game.Id,catalogOnlineFresh?(ManagedUpdateReady(game)?"A Gacha Nebula + update is ready.":"No newer Gacha Nebula + release is available."):"Could not check the project channel. Your installed game is unchanged.");RefreshView();}}finally{if(!IsDisposed)check.Enabled=!busy;}}
}
}
