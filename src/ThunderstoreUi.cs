using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using MC2Pack;
namespace Astera {
sealed partial class LibraryForm {
    Panel modProfileStrip;Label modProfileLabel;Button modProfileChoose,modProfileUpdate;readonly HashSet<string> modProfileScanned=new HashSet<string>();
    ThunderGame SelectedThunderGame {get{var game=CurrentCustom;if(game==null)return null;var art=CustomSteam(game);return ThunderstoreProfiles.Game(game.Platform=="steam"?game.PlatformId:art==null?null:art.AppId);}}
    ModProfileLink SelectedModProfile(CustomGame game){return game==null||game.ModProfiles==null?null:game.ModProfiles.FirstOrDefault(p=>p.Id==game.SelectedModProfile);}
    void BuildModProfileStrip(){
        modProfileStrip=new Panel{Height=36,BackColor=Color.Transparent,Visible=false};modProfileLabel=new Label{Left=0,Top=9,Width=260,Height=24,AutoEllipsis=true,ForeColor=Color.White};modProfileChoose=Button("Profiles…",delegate{ChooseModProfile();});modProfileChoose.SetBounds(270,0,120,34);modProfileUpdate=Button("Update by code…",async delegate{await UpdateModProfile();});modProfileUpdate.SetBounds(400,0,175,34);modProfileStrip.Controls.AddRange(new Control[]{modProfileLabel,modProfileChoose,modProfileUpdate});banner.Controls.Add(modProfileStrip);
    }
    void RefreshModProfileStrip(){
        if(modProfileStrip==null)return;var game=CurrentCustom;var definition=SelectedThunderGame;modProfileStrip.Visible=game!=null&&definition!=null;
        if(!modProfileStrip.Visible)return;
        if(!previewMode&&modProfileScanned.Add(game.Id))ScanModProfiles(game,definition);
        var selected=SelectedModProfile(game);modProfileLabel.Text="Profile: "+(selected==null?"Vanilla":selected.Name);bool locked=busy||GameRunning(game)||GameStarting(game);modProfileChoose.Enabled=!locked;modProfileUpdate.Enabled=!locked&&definition.Loader=="bepinex";modProfileUpdate.Text=selected==null?"Import by code…":"Update by code…";LayoutModProfileStrip();
    }
    async void ScanModProfiles(CustomGame game,ThunderGame definition){try{var roots=ThunderstoreProfiles.Roots(store.Settings.ThunderstoreRoots).ToArray();var found=await Task.Run(()=>ThunderstoreProfiles.Discover(definition,roots));if(IsDisposed)return;ThunderstoreProfiles.Merge(game,found);store.Save();if(CurrentCustom==game){RefreshModProfileStrip();LayoutHeroContent();}}catch(Exception error){AsteraDiagnostics.Write("Thunderstore profile scan",error);}}
    void LayoutModProfileStrip(){if(modProfileStrip==null||!modProfileStrip.Visible||heroActions==null)return;modProfileStrip.SetBounds(24,heroActions.Top-42,Math.Max(100,banner.Width-48),36);modProfileLabel.Width=Math.Max(140,Math.Min(260,modProfileStrip.Width-350));modProfileChoose.Left=modProfileLabel.Right+10;modProfileUpdate.Left=modProfileChoose.Right+10;modProfileStrip.BringToFront();}
    void ChooseModProfile(){var game=CurrentCustom;var definition=SelectedThunderGame;if(game==null||definition==null||busy||GameRunning(game))return;
        using(var dialog=new AsteraDialog{Text=game.Name+" profiles",Size=new Size(650,480),StartPosition=FormStartPosition.CenterParent}){
            var list=new ListBox{Dock=DockStyle.Fill,BackColor=Base,ForeColor=Color.White,BorderStyle=BorderStyle.None};var rows=new List<ModProfileLink>();Action fill=delegate{rows=new List<ModProfileLink>{null};rows.AddRange(game.ModProfiles??new List<ModProfileLink>());list.Items.Clear();foreach(var row in rows)list.Items.Add(row==null?"Vanilla":row.Name+"  ·  "+(row.UseManaged&&!String.IsNullOrEmpty(row.Managed)?"Astera copy":row.Manager));list.SelectedIndex=Math.Max(0,rows.FindIndex(p=>p!=null&&p.Id==game.SelectedModProfile));};
            var message=new Label{Dock=DockStyle.Top,Height=54,Padding=new Padding(12),Text="Choose which profile Play will use. Updates stay in Astera; your manager's original profile is preserved."};var actions=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=60,Padding=new Padding(10)};
            var use=Button("Use profile",delegate{int index=list.SelectedIndex;if(index<0)return;if(GameRunning(game)){MessageBox.Show(dialog,"Close the game before switching profiles.","Game is running");return;}if(rows[index]!=null)rows[index].UseManaged=true;game.SelectedModProfile=rows[index]==null?"":rows[index].Id;store.Save();RefreshModProfileStrip();dialog.DialogResult=DialogResult.OK;});
            var scan=Button("Refresh",delegate{ThunderstoreProfiles.Merge(game,ThunderstoreProfiles.Discover(definition,ThunderstoreProfiles.Roots(store.Settings.ThunderstoreRoots)));store.Save();fill();});
            var browse=Button("Find data folder…",delegate{using(var picker=new ExplorerFolderDialog{Description="Choose the manager's data folder containing the game folders"})if(picker.ShowDialog(dialog)==DialogResult.OK){ThunderstoreProfiles.Merge(game,ThunderstoreProfiles.Discover(definition,new[]{picker.SelectedPath}));if(!store.Settings.ThunderstoreRoots.Contains(picker.SelectedPath,StringComparer.OrdinalIgnoreCase))store.Settings.ThunderstoreRoots.Add(picker.SelectedPath);store.Save();fill();}});
            var original=Button("Use original",delegate{int index=list.SelectedIndex;if(index<=0||GameRunning(game))return;var link=rows[index];if(String.IsNullOrEmpty(link.Source)||!Directory.Exists(link.Source))return;link.UseManaged=false;game.SelectedModProfile=link.Id;store.Save();RefreshModProfileStrip();dialog.DialogResult=DialogResult.OK;});actions.Height=100;actions.WrapContents=true;actions.Controls.AddRange(new Control[]{use,original,scan,browse});dialog.Controls.Add(list);dialog.Controls.Add(message);dialog.Controls.Add(actions);fill();list.DoubleClick+=delegate{use.PerformClick();};dialog.ShowDialog(this);
        }
    }
    async Task UpdateModProfile(){
        var game=CurrentCustom;var definition=SelectedThunderGame;if(game==null||definition==null||busy||GameRunning(game))return;var selected=SelectedModProfile(game);string code;
        using(var dialog=new AsteraDialog{Text=selected==null?"Import a mod profile":"Update "+selected.Name,Size=new Size(610,235),StartPosition=FormStartPosition.CenterParent}){
            var label=new Label{Left=18,Top=25,Width=565,Height=38,Text="Paste the profile share code from Thunderstore or r2modman."};var input=new TextBox{Left=18,Top=73,Width=565};var go=Button("Check profile",delegate{try{input.Text=ThunderstoreProfiles.Code(input.Text);dialog.DialogResult=DialogResult.OK;}catch(Exception error){MessageBox.Show(dialog,error.Message,"Profile code");}});go.SetBounds(400,125,180,38);dialog.Controls.AddRange(new Control[]{label,input,go});dialog.AcceptButton=go;if(dialog.ShowDialog(this)!=DialogResult.OK)return;code=input.Text;
        }
        string key="custom:"+game.Id;ThunderImportPlan plan=null;string work=SelfUpdateFiles.Inside(SelfUpdateFiles.Root(store.Root),"thunderstore-work/"+Guid.NewGuid().ToString("N"));
        await Work(delegate{plan=ThunderstoreImport.Download(code,work,cancellation.Token,ReportWorkProgress,definition,SelfUpdateFiles.Inside(SelfUpdateFiles.Root(store.Root),"thunderstore-api-cache"));},false,key);if(plan==null||IsDisposed)return;
        if(MessageBox.Show(this,"Install "+plan.Export.Name+" for "+game.Name+"?\n\n"+plan.Packages.Count+" enabled packages. Astera will keep its own copy; the original manager profile and game saves stay intact.","Review mod profile",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
        if(await Task.Run(()=>RunningGames.Check(game,definition.SteamId))){MessageBox.Show(this,"Close the game before updating its profile.","Game is running");return;}
        var link=selected??new ModProfileLink{Name=plan.Export.Name,Manager="Astera"};string ownedId=String.IsNullOrEmpty(link.ManagedId)?Guid.NewGuid().ToString("N"):link.ManagedId;string target=null;
        await Work(delegate{target=ThunderstoreImport.Install(plan,definition,SelfUpdateFiles.Inside(SelfUpdateFiles.Root(store.Root),"mod-profiles/"+definition.SteamId),ownedId,selected==null?null:ThunderstoreProfiles.Active(selected),cancellation.Token,ReportWorkProgress,Log,()=>RunningGames.Check(game,definition.SteamId),null,SelfUpdateFiles.Inside(SelfUpdateFiles.Root(store.Root),"thunderstore-cache"));},false,key);
        if(target!=null){if(game.ModProfiles==null)game.ModProfiles=new List<ModProfileLink>();if(selected==null)game.ModProfiles.Add(link);link.UseManaged=true;link.Managed=target;link.ManagedId=ownedId;link.Code=code;game.SelectedModProfile=link.Id;store.Save();LogActivity(key,"Mod profile ready: "+link.Name);if(CurrentCustom==game){RefreshView();LayoutHeroContent();}}
    }
    bool LaunchThunderProfile(CustomGame game){var definition=SelectedThunderGame;if(definition==null)return false;var profile=SelectedModProfile(game);if(definition.Loader!="bepinex"){if(profile!=null)throw new IOException("This profile uses a loader that still needs its original mod manager. Astera did not start the vanilla game.");return false;}if(profile==null&&!String.IsNullOrEmpty(game.SelectedModProfile))throw new IOException("The selected profile is missing. Choose a profile again.");ProcessThunderLaunch(game,definition,profile==null?null:ThunderstoreProfiles.Active(profile));return true;}
    void ProcessThunderLaunch(CustomGame game,ThunderGame definition,string profile){var start=ThunderstoreLaunch.Start(definition,profile,PlatformLibraries.Folder(game),SelfUpdateFiles.Inside(SelfUpdateFiles.Root(store.Root),"thunderstore-launch-backups/"+definition.SteamId));System.Diagnostics.Process.Start(start);TrackCustomLaunch(game);}
}
}
