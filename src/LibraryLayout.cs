using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace Astera {
// The background and character are separate assets. The character's canvas starts
// above the background, so hair/hats can extend beyond the hero's upper edge.
sealed class Banner:Panel {
    public string Game="Minecraft²";
    Image scene,character;string scenePath="",characterPath="";
    public Banner(){DoubleBuffered=true;BackColor=Color.FromArgb(12,17,33);}
    public void Artwork(string background,string foreground){
        Replace(ref scene,ref scenePath,background);Replace(ref character,ref characterPath,foreground);Invalidate();
    }
    static void Replace(ref Image image,ref string oldPath,string path){
        path=path??"";if(path==oldPath)return;
        Image next=null;if(path.Length>0){try{next=ReadArtwork(path);}catch(IOException){}catch(ArgumentException){}catch(UnauthorizedAccessException){}}
        if(image!=null)image.Dispose();image=next;oldPath=path;
    }
    internal static Image ReadArtwork(string path){
        var file=new FileInfo(path);if(file.Length>25*1024*1024)throw new IOException("Use artwork smaller than 25 MB.");
        using(var input=Image.FromFile(path)){
            if((long)input.Width*input.Height>20000000)throw new IOException("Use artwork below 20 megapixels.");
            return new Bitmap(input);
        }
    }
    protected override void OnPaint(PaintEventArgs e){
        base.OnPaint(e);if(Width<2||Height<35)return;var g=e.Graphics;
        g.SmoothingMode=SmoothingMode.AntiAlias;g.InterpolationMode=InterpolationMode.HighQualityBicubic;
        var area=new Rectangle(0,30,Width,Height-30);
        if(scene!=null){var clip=g.Save();g.SetClip(area);Cover(g,scene,area);g.Restore(clip);}
        else {
            using(var fill=new LinearGradientBrush(area,Color.FromArgb(22,31,65),Color.FromArgb(54,38,88),25F))g.FillRectangle(fill,area);
            var random=new Random(17);for(int i=0;i<110;i++){int x=random.Next(Width),y=30+random.Next(Height-30);using(var star=new SolidBrush(Color.FromArgb(70+i%150,155,168,241)))g.FillEllipse(star,x,y,i%7==0?3:1.5F,i%7==0?3:1.5F);}
            using(var moon=new SolidBrush(Color.FromArgb(22,164,155,240)))g.FillEllipse(moon,Width*.68F,45,150,150);
        }
        // Reserve the left side for live text and controls, even with bright art.
        using(var shade=new LinearGradientBrush(area,Color.FromArgb(235,10,15,31),Color.FromArgb(15,10,15,31),0F))g.FillRectangle(shade,area);
        if(character!=null){float scale=Math.Min(Height/(float)character.Height,Width*.47F/character.Width);int w=(int)(character.Width*scale),h=(int)(character.Height*scale);g.DrawImage(character,new Rectangle(Width-w-12,Height-h,w,h));}
        else {
            var slot=new Rectangle((int)(Width*.65),8,(int)(Width*.32),Height-20);
            using(var pen=new Pen(Color.FromArgb(72,150,143,230),1)){pen.DashStyle=DashStyle.Dash;g.DrawRectangle(pen,slot);}
            using(var font=new Font("Segoe UI",10))using(var brush=new SolidBrush(Color.FromArgb(165,180,190,229)))
                g.DrawString("CHARACTER LAYER\n\nTransparent artwork\ncan rise above the scene",font,brush,new RectangleF(slot.X+16,slot.Y+70,slot.Width-24,150));
        }
        using(var edge=new Pen(Color.FromArgb(58,76,116)))g.DrawLine(edge,0,Height-1,Width,Height-1);
    }
    internal static void Cover(Graphics g,Image image,Rectangle target){float ratio=Math.Max(target.Width/(float)image.Width,target.Height/(float)image.Height);float w=image.Width*ratio,h=image.Height*ratio;g.DrawImage(image,target.X+(target.Width-w)/2,target.Y+(target.Height-h)/2,w,h);}
    protected override void Dispose(bool disposing){if(disposing){if(scene!=null)scene.Dispose();if(character!=null)character.Dispose();}base.Dispose(disposing);}
}

sealed class Card:Panel {
    public Card(){DoubleBuffered=true;BackColor=Color.FromArgb(21,28,47);Padding=new Padding(16);Margin=new Padding(0,0,14,0);}
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);if(Width>2&&Height>2)using(var pen=new Pen(Color.FromArgb(49,64,101)))using(var path=AsteraButton.Round(new Rectangle(0,0,Width-1,Height-1),9))e.Graphics.DrawPath(pen,path);}
}

sealed class ScenePreview:Panel {
    public Image Artwork;
    public ScenePreview(){DoubleBuffered=true;}
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);if(Width<2||Height<2)return;if(Artwork!=null)Banner.Cover(e.Graphics,Artwork,ClientRectangle);else{using(var fill=new LinearGradientBrush(ClientRectangle,Color.FromArgb(23,36,76),Color.FromArgb(61,38,91),25F))e.Graphics.FillRectangle(fill,ClientRectangle);var random=new Random(19);for(int i=0;i<40;i++)e.Graphics.FillEllipse(Brushes.LightSlateGray,random.Next(Width),random.Next(Height),2,2);}}
}

sealed partial class LibraryForm {
    readonly Label heroTitle=new Label(),heroSubtitle=new Label(),installationPath=new Label(),artLabel=new Label();
    readonly Panel overviewPage=new Panel(),backgroundPage=new Panel(),preferencesPage=new Panel();
    readonly TableLayoutPanel information=new TableLayoutPanel();
    readonly Panel overviewRight=new Panel(),activityCard=new Card();
    readonly FlowLayoutPanel tabs=new FlowLayoutPanel();
    readonly PictureBox artPreview=new PictureBox();
    readonly ScenePreview previewScene=new ScenePreview();
    readonly Button openFolder=new AsteraButton(),chooseBackground=new AsteraButton(),chooseCharacter=new AsteraButton();
    int selectedTab;string previewArtwork="";

    void BuildLibraryLayout(){
        BackColor=Color.FromArgb(12,17,33);
        var header=new Panel{Dock=DockStyle.Top,Height=72,BackColor=Color.FromArgb(11,15,29),Padding=new Padding(24,12,18,12)};
        var brand=new Label{Text="✦  ASTERA",Font=new Font("Segoe UI",21,FontStyle.Bold),ForeColor=Color.FromArgb(235,237,255),AutoSize=false,Width=220,Dock=DockStyle.Left,TextAlign=ContentAlignment.MiddleLeft};
        var tagline=new Label{Text="YOUR WORLDS. A LITTLE MORE ALIVE.",ForeColor=Color.FromArgb(145,160,230),Font=new Font("Segoe UI",8),Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft};
        var top=new FlowLayoutPanel{Dock=DockStyle.Right,Width=270,FlowDirection=FlowDirection.RightToLeft,WrapContents=false};
        var exit=Button("Exit",delegate{quitting=true;Close();});((AsteraButton)exit).Symbol=ButtonSymbol.Exit;
        settings.Text="Settings";Style(settings);((AsteraButton)settings).Symbol=ButtonSymbol.Settings;settings.Click+=delegate{Configure();};top.Controls.Add(exit);top.Controls.Add(settings);
        header.Controls.Add(tagline);header.Controls.Add(top);header.Controls.Add(brand);

        var sidebar=new Panel{Dock=DockStyle.Left,Width=220,BackColor=Color.FromArgb(14,19,36),Padding=new Padding(14,26,12,16)};
        sidebar.Controls.Add(new Label{Text="YOUR LIBRARY",ForeColor=Muted,Height=36,Dock=DockStyle.Top,Font=new Font("Segoe UI",9)});
        games.Dock=DockStyle.Fill;games.BorderStyle=BorderStyle.None;games.BackColor=sidebar.BackColor;games.ForeColor=Color.White;games.Font=new Font("Segoe UI",12);games.ItemHeight=76;games.DrawMode=DrawMode.OwnerDrawFixed;games.Items.AddRange(new object[]{"Minecraft²","Star Dash"});games.DrawItem+=DrawGame;games.SelectedIndexChanged+=delegate{RefreshView();};sidebar.Controls.Add(games);games.BringToFront();
        sidebar.Controls.Add(new Label{Text="ASTERA\nby Star  ·  v0.1.3\n\nSame worlds.\nBrighter tomorrows.",ForeColor=Color.FromArgb(144,155,205),Dock=DockStyle.Bottom,Height=110,Font=new Font("Segoe UI",9)});

        var viewport=new Panel{Dock=DockStyle.Fill,AutoScroll=true};
        var body=new TableLayoutPanel{Dock=DockStyle.Top,Height=740,ColumnCount=1,RowCount=4,Padding=new Padding(24,0,24,12)};
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute,330));body.RowStyles.Add(new RowStyle(SizeType.Absolute,54));body.RowStyles.Add(new RowStyle(SizeType.Percent,100));body.RowStyles.Add(new RowStyle(SizeType.Absolute,28));
        viewport.Controls.Add(body);viewport.Resize+=delegate{body.RowStyles[0].Height=viewport.ClientSize.Height<740?280:330;body.Height=Math.Max(620,viewport.ClientSize.Height);};
        banner.Dock=DockStyle.Fill;banner.Margin=Padding.Empty;body.Controls.Add(banner,0,0);
        var heroText=new TableLayoutPanel{BackColor=Color.Transparent,ColumnCount=1,RowCount=5,Location=new Point(22,66),Height=176,Anchor=AnchorStyles.Left|AnchorStyles.Top};
        foreach(int height in new[]{56,32,34,30,8})heroText.RowStyles.Add(new RowStyle(SizeType.Absolute,height));heroText.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        heroTitle.Text="Minecraft²";heroTitle.Font=new Font("Segoe UI",32,FontStyle.Bold);heroTitle.ForeColor=Color.White;heroTitle.Dock=DockStyle.Fill;
        heroSubtitle.Font=new Font("Segoe UI",13);heroSubtitle.ForeColor=Color.FromArgb(191,200,246);heroSubtitle.Dock=DockStyle.Fill;
        status.Font=new Font("Segoe UI",10,FontStyle.Bold);status.Dock=DockStyle.Fill;status.AutoEllipsis=true;status.TextAlign=ContentAlignment.MiddleLeft;
        profileLabel.Dock=DockStyle.Fill;profileLabel.ForeColor=Muted;profileLabel.AutoEllipsis=true;profileLabel.Font=new Font("Segoe UI",9);
        heroText.Controls.Add(heroTitle,0,0);heroText.Controls.Add(heroSubtitle,0,1);heroText.Controls.Add(status,0,2);heroText.Controls.Add(profileLabel,0,3);banner.Controls.Add(heroText);
        var action=new FlowLayoutPanel{BackColor=Color.Transparent,Location=new Point(24,258),Height=58,WrapContents=false};
        play.Text="PLAY";Style(play);((AsteraButton)play).Role=ButtonRole.Primary;((AsteraButton)play).Symbol=ButtonSymbol.Play;play.Font=new Font("Segoe UI",11,FontStyle.Bold);play.MinimumSize=new Size(170,48);play.Click+=delegate{if(games.SelectedIndex==0)OpenCurseForge();else OpenStarDash();};
        check.Text="Check for updates";Style(check);((AsteraButton)check).Symbol=ButtonSymbol.Refresh;check.MinimumSize=new Size(195,48);check.Click+=async delegate{await Check(false);};
        var more=Button("…",delegate{var menu=new ContextMenuStrip();menu.Items.Add("Open game folder",null,delegate{OpenGameFolder();});menu.Items.Add("Game settings",null,delegate{Configure();});menu.Items.Add("Update activity",null,delegate{SelectLibraryTab(1);});menu.Closed+=delegate{menu.Dispose();};menu.Show(Cursor.Position);});more.MinimumSize=new Size(48,48);
        action.Controls.AddRange(new Control[]{play,check,more});banner.Controls.Add(action);
        banner.Resize+=delegate{heroText.Width=Math.Max(400,(int)(banner.Width*.60));heroText.Top=banner.Height<300?44:66;action.Top=banner.Height-72;action.Width=banner.Width-40;};

        tabs.Dock=DockStyle.Fill;tabs.Padding=new Padding(0,7,0,4);tabs.WrapContents=false;tabs.Margin=Padding.Empty;
        int index=0;foreach(string title in new[]{"Overview","Updates","Backgrounds","Settings"}){int choice=index++;var tab=Button(title,delegate{SelectLibraryTab(choice);});tab.Tag=choice;tab.MinimumSize=new Size(110,40);tabs.Controls.Add(tab);}body.Controls.Add(tabs,0,1);
        var pages=new Panel{Dock=DockStyle.Fill,Margin=Padding.Empty};foreach(var page in new[]{overviewPage,backgroundPage,preferencesPage}){page.Dock=DockStyle.Fill;pages.Controls.Add(page);}body.Controls.Add(pages,0,2);

        information.Dock=DockStyle.Fill;information.ColumnCount=2;information.RowCount=1;information.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,60));information.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,40));information.RowStyles.Add(new RowStyle(SizeType.Percent,100));overviewPage.Controls.Add(information);
        var latest=new Card{Dock=DockStyle.Fill};information.Controls.Add(latest,0,0);
        var latestLayout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3};latestLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));latestLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,36));latestLayout.RowStyles.Add(new RowStyle(SizeType.Percent,100));latestLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,110));latest.Controls.Add(latestLayout);
        latestLayout.Controls.Add(Heading("Latest update"),0,0);
        ConfigureReadOnly(notes,latest.BackColor);latestLayout.Controls.Add(notes,0,1);
        var controls=new FlowLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(0,9,0,0),WrapContents=true};
        download.Text="Download";Style(download);((AsteraButton)download).Symbol=ButtonSymbol.Download;download.Click+=async delegate{await Download();};
        install.Text="Install update";Style(install);((AsteraButton)install).Role=ButtonRole.Primary;((AsteraButton)install).Symbol=ButtonSymbol.Install;install.Click+=async delegate{await Install();};
        restore.Text="Restore backup";Style(restore);((AsteraButton)restore).Role=ButtonRole.Quiet;((AsteraButton)restore).Symbol=ButtonSymbol.Restore;restore.Click+=async delegate{await Restore();};
        cancel.Text="Cancel";Style(cancel);((AsteraButton)cancel).Role=ButtonRole.Quiet;((AsteraButton)cancel).Symbol=ButtonSymbol.Cancel;cancel.Click+=delegate{if(cancellation!=null)cancellation.Cancel();};
        foreach(var button in new[]{download,install,restore,cancel})button.Margin=new Padding(0,0,8,6);controls.Controls.AddRange(new Control[]{download,install,restore,cancel});latestLayout.Controls.Add(controls,0,2);

        overviewRight.Dock=DockStyle.Fill;overviewRight.Margin=Padding.Empty;information.Controls.Add(overviewRight,1,0);
        var rightLayout=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=2,ColumnCount=1};rightLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));rightLayout.RowStyles.Add(new RowStyle(SizeType.Percent,55));rightLayout.RowStyles.Add(new RowStyle(SizeType.Percent,45));overviewRight.Controls.Add(rightLayout);
        var background=new Card{Dock=DockStyle.Fill,Margin=new Padding(0,0,0,12)};rightLayout.Controls.Add(background,0,0);
        var manage=Button("Manage",delegate{SelectLibraryTab(2);});manage.Dock=DockStyle.Right;manage.MinimumSize=new Size(90,36);
        var artHeader=new Panel{Dock=DockStyle.Top,Height=42};artHeader.Controls.Add(Heading("Game artwork"));artHeader.Controls.Add(manage);background.Controls.Add(artHeader);
        artLabel.Text="Default starfield\nBackground + separate character layer";artLabel.Dock=DockStyle.Bottom;artLabel.Height=46;artLabel.ForeColor=Muted;artLabel.Padding=new Padding(0,6,0,0);background.Controls.Add(artLabel);previewScene.Dock=DockStyle.Fill;background.Controls.Add(previewScene);previewScene.BringToFront();
        var installation=new Card{Dock=DockStyle.Fill,Margin=Padding.Empty};rightLayout.Controls.Add(installation,0,1);
        openFolder.Text="Open folder";Style(openFolder);openFolder.Dock=DockStyle.Bottom;openFolder.Click+=delegate{OpenGameFolder();};
        installationPath.Dock=DockStyle.Fill;installationPath.ForeColor=Muted;installationPath.AutoEllipsis=true;installationPath.Padding=new Padding(0,4,0,0);installation.Controls.Add(installationPath);installation.Controls.Add(openFolder);installation.Controls.Add(new Label{Text="Installation",Font=new Font("Segoe UI",11,FontStyle.Bold),Dock=DockStyle.Top,Height=28});
        activityCard.Dock=DockStyle.Fill;activityCard.Margin=Padding.Empty;ConfigureReadOnly(log,activityCard.BackColor);activityCard.Controls.Add(log);activityCard.Controls.Add(new Label{Text="Update activity",Dock=DockStyle.Top,Height=34,Font=new Font("Segoe UI",11,FontStyle.Bold)});
        BuildArtworkPage();BuildPreferencesPage();
        releaseLabel.Dock=DockStyle.Fill;releaseLabel.ForeColor=Muted;releaseLabel.Font=new Font("Segoe UI",9);releaseLabel.TextAlign=ContentAlignment.MiddleLeft;body.Controls.Add(releaseLabel,0,3);
        Controls.Add(viewport);Controls.Add(sidebar);Controls.Add(header);SelectLibraryTab(0);
        Disposed+=delegate{if(artPreview.Image!=null)artPreview.Image.Dispose();if(activityCard.Parent==null)activityCard.Dispose();if(overviewRight.Parent==null)overviewRight.Dispose();tray.Dispose();timer.Dispose();};
    }
    static Label Heading(string title){return new Label{Text=title,Font=new Font("Segoe UI",11,FontStyle.Bold),ForeColor=Color.FromArgb(235,238,255),Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft};}
    static void ConfigureReadOnly(TextBox box,Color background){box.Multiline=true;box.ReadOnly=true;box.TabStop=false;box.HideSelection=true;box.ScrollBars=ScrollBars.Vertical;box.Dock=DockStyle.Fill;box.BackColor=background;box.ForeColor=Color.FromArgb(185,196,225);box.BorderStyle=BorderStyle.None;}
    void SelectLibraryTab(int index){selectedTab=index;overviewPage.Visible=index<2;backgroundPage.Visible=index==2;preferencesPage.Visible=index==3;
        foreach(AsteraButton tab in tabs.Controls)tab.Role=(int)tab.Tag==index?ButtonRole.Primary:ButtonRole.Quiet;
        information.SuspendLayout();if(index<2){overviewRight.Visible=index==0;activityCard.Visible=index==1;if(index==1){information.Controls.Remove(overviewRight);information.Controls.Add(activityCard,1,0);}else{information.Controls.Remove(activityCard);information.Controls.Add(overviewRight,1,0);}}information.ResumeLayout();
    }
    void UpdateLayoutView(){
        bool mc=games.SelectedIndex==0;heroTitle.Text=mc?"Minecraft²":"Star Dash";heroSubtitle.Text=mc?"Your worlds. A little more alive.":"Ready for your next race.";
        string background=mc?store.Settings.BackgroundMinecraft:store.Settings.BackgroundStarDash,character=mc?store.Settings.CharacterMinecraft:store.Settings.CharacterStarDash;
        banner.Artwork(background,character);installationPath.Text=String.IsNullOrWhiteSpace(GameFolder())?"No installation linked":GameFolder();openFolder.Enabled=!busy&&Directory.Exists(GameFolder());
        chooseBackground.Enabled=chooseCharacter.Enabled=!busy;artLabel.Text=(String.IsNullOrEmpty(background)?"Default starfield":Path.GetFileName(background))+"\n"+(String.IsNullOrEmpty(character)?"Character slot ready for transparent artwork":Path.GetFileName(character));
        if(previewArtwork!=(background??"")){previewArtwork=background??"";var old=artPreview.Image;previewScene.Artwork=null;artPreview.Image=null;try{if(previewArtwork.Length>0)artPreview.Image=Banner.ReadArtwork(previewArtwork);}catch{}previewScene.Artwork=artPreview.Image;previewScene.Invalidate();if(old!=null)old.Dispose();}
    }
    string GameFolder(){return games.SelectedIndex==0?store.Settings.Profile:String.IsNullOrWhiteSpace(store.Settings.StarDashExecutable)?null:Path.GetDirectoryName(store.Settings.StarDashExecutable);}
    void OpenGameFolder(){string folder=GameFolder();if(!Directory.Exists(folder)){Configure();return;}try{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(folder){UseShellExecute=true});}catch(Exception error){MessageBox.Show(this,error.Message,"Open game folder");}}
    void BuildArtworkPage(){
        var card=new Card{Dock=DockStyle.Fill,Margin=Padding.Empty};backgroundPage.Controls.Add(card);
        var row=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=100,WrapContents=true,Padding=new Padding(0,8,0,0)};
        chooseBackground.Text="Choose background…";Style(chooseBackground);chooseBackground.Click+=delegate{ChooseArtwork(false);};
        chooseCharacter.Text="Choose character…";Style(chooseCharacter);chooseCharacter.Click+=delegate{ChooseArtwork(true);};
        var reset=Button("Reset artwork",delegate{if(busy)return;if(games.SelectedIndex==0){store.Settings.BackgroundMinecraft="";store.Settings.CharacterMinecraft="";}else{store.Settings.BackgroundStarDash="";store.Settings.CharacterStarDash="";}store.Save();UpdateLayoutView();});
        foreach(var button in new[]{chooseBackground,chooseCharacter,reset})button.Margin=new Padding(0,0,8,6);row.Controls.AddRange(new Control[]{chooseBackground,chooseCharacter,reset});
        artPreview.Dock=DockStyle.Fill;artPreview.SizeMode=PictureBoxSizeMode.Zoom;artPreview.BackColor=Color.FromArgb(15,21,39);card.Controls.Add(artPreview);card.Controls.Add(row);card.Controls.Add(new Label{Text="Game artwork  ·  Background fills the scene. Transparent PNG character sits in front and can extend above it.",Dock=DockStyle.Top,Height=45,ForeColor=Muted});
    }
    void ChooseArtwork(bool character){if(busy)return;using(var dialog=new OpenFileDialog{Title=character?"Choose a transparent character PNG":"Choose a game background",Filter=character?"PNG artwork|*.png":"Artwork|*.png;*.jpg;*.jpeg"}){if(dialog.ShowDialog(this)!=DialogResult.OK)return;try{using(var proof=Banner.ReadArtwork(dialog.FileName)){}
        if(games.SelectedIndex==0){if(character)store.Settings.CharacterMinecraft=dialog.FileName;else store.Settings.BackgroundMinecraft=dialog.FileName;}else{if(character)store.Settings.CharacterStarDash=dialog.FileName;else store.Settings.BackgroundStarDash=dialog.FileName;}store.Save();UpdateLayoutView();
        }catch(Exception error){MessageBox.Show(this,error.Message,"Game artwork");}}}
    void BuildPreferencesPage(){var card=new Card{Dock=DockStyle.Fill,Margin=Padding.Empty};preferencesPage.Controls.Add(card);var edit=Button("Edit settings",delegate{Configure();});edit.Dock=DockStyle.Bottom;card.Controls.Add(new Label{Text="Game settings\n\nLink your CurseForge profile or StarDash export, choose the update channel, and control background checks and tray behaviour.",Dock=DockStyle.Fill,ForeColor=Muted,Font=new Font("Segoe UI",12)});card.Controls.Add(edit);}
    protected override bool ShowWithoutActivation {get{return previewMode||base.ShowWithoutActivation;}}
}
}
