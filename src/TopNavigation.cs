using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
namespace Astera {
sealed class TopNavigationTab:Button {
    internal bool Selected;
    internal TopNavigationTab(string text){Text=text;AccessibleName=text;FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;Size=new Size(98,44);Font=UiTheme.Text(16,true);Cursor=Cursors.Hand;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
    protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(BackColor);TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,Selected?Color.White:Color.FromArgb(167,180,205),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);if(Selected)using(var pen=new Pen(Color.FromArgb(54,201,240),2))e.Graphics.DrawLine(pen,12,Height-3,Width-12,Height-3);if(Focused)ControlPaint.DrawFocusRectangle(e.Graphics,Rectangle.Inflate(ClientRectangle,-5,-7));}
}
sealed partial class LibraryForm {
    Panel topNavigation,discoverPage,arcadePage,librarySidebar;
    TopNavigationTab[] primaryTabs;
    int primaryPage;
    void BuildTopNavigation(Panel header,Panel sidebar,Control tagline){
        librarySidebar=sidebar;topNavigation=new Panel{Size=new Size(294,44),BackColor=header.BackColor};
        primaryTabs=new TopNavigationTab[3];var names=new[]{"Library","Discover","Arcade"};
        for(int i=0;i<names.Length;i++){int choice=i;var tab=new TopNavigationTab(names[i]){Left=i*98,BackColor=header.BackColor,Selected=i==0};tab.Click+=delegate{SelectPrimaryPage(choice);};primaryTabs[i]=tab;topNavigation.Controls.Add(tab);}
        header.Controls.Add(topNavigation);topNavigation.BringToFront();
        Action arrange=delegate{topNavigation.Location=new Point((header.ClientSize.Width-topNavigation.Width)/2,14);tagline.Visible=header.ClientSize.Width>=1450;};header.Resize+=delegate{arrange();};arrange();
        discoverPage=BuildDiscoverPage();arcadePage=new Panel{Dock=DockStyle.Fill,Padding=new Padding(36),Visible=false,BackColor=BackColor};
        var arcadeText=new Label{Dock=DockStyle.Top,Height=150,ForeColor=Color.White,Font=UiTheme.Text(22,true),Text="Arcade\r\n\r\nComing soon"};arcadePage.Controls.Add(arcadeText);
        Controls.Add(discoverPage);Controls.SetChildIndex(discoverPage,0);Controls.Add(arcadePage);Controls.SetChildIndex(arcadePage,0);
    }
    void SelectPrimaryPage(int page){
        if(page<0||page>2||primaryPage==page)return;
        CancelGameTransition();primaryPage=page;
        SuspendLayout();try{gameViewport.Visible=librarySidebar.Visible=page==0;discoverPage.Visible=page==1;arcadePage.Visible=page==2;for(int i=0;i<primaryTabs.Length;i++){primaryTabs[i].Selected=i==page;primaryTabs[i].Invalidate();}}finally{ResumeLayout(true);}
        banner.PauseTransition(page!=0);if(page==0){if(IsCustomGame&&CurrentCustom.ManagedProjectId==ManagedGames.Id)RefreshManagedPresentation(CurrentCustom);LayoutHeroContent();}
    }
    void OpenDiscoverGame(int kind){
        if(kind<0||kind>2||busy)return;
        string key="builtin:"+kind;
        libraryFilter=store.Settings.HiddenGames.Contains(key)?"hidden":"all";
        librarySearch.Clear();ReloadGames(null,true,true);
        var entry=games.Items.Cast<LibraryEntry>().FirstOrDefault(e=>e.Custom==null&&e.Kind==kind);
        if(entry!=null){games.SelectedItem=entry;activeLibraryEntry=entry;CancelGameTransition();RefreshView();}
        SelectPrimaryPage(0);
    }
}
}
