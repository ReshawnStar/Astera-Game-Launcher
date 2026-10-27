using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Drawing;
namespace Astera {
static class LibrarySearch {
    internal static bool Matches(string name,string query){name=(name??"").Replace("²","2");return (query??"").Replace("²","2").Split(new[]{' ','\t'},StringSplitOptions.RemoveEmptyEntries).All(term=>name.IndexOf(term,StringComparison.OrdinalIgnoreCase)>=0);}
}
sealed partial class LibraryForm {
    readonly TextBox librarySearch=new TextBox{Dock=DockStyle.Top,BackColor=Color.FromArgb(26,34,55),ForeColor=Color.White,BorderStyle=BorderStyle.FixedSingle,Font=UiTheme.Text(14),AccessibleName="Search library"};readonly Label noLibraryMatches=new Label{Text="No matching games",Dock=DockStyle.Bottom,Height=22,ForeColor=Color.LightSlateGray,Visible=false};Panel librarySearchHost;bool reloadingLibrary;LibraryEntry activeLibraryEntry;
    [DllImport("user32.dll",EntryPoint="SendMessageW",CharSet=CharSet.Unicode)]static extern IntPtr SearchCue(IntPtr handle,int message,IntPtr parameter,string text);
    void BuildLibrarySearch(Control sidebar){librarySearchHost=new Panel{Dock=DockStyle.Top,Height=42,Padding=new Padding(0,0,0,8)};librarySearchHost.Controls.Add(librarySearch);librarySearchHost.Controls.Add(noLibraryMatches);sidebar.Controls.Add(librarySearchHost);librarySearch.HandleCreated+=delegate{SearchCue(librarySearch.Handle,0x1501,new IntPtr(1),"Search games…");};librarySearch.TextChanged+=delegate{if(!reloadingLibrary)ReloadGames(null,true);};librarySearch.KeyDown+=delegate(object sender,KeyEventArgs e){if(e.KeyCode==Keys.Escape){librarySearch.Clear();e.SuppressKeyPress=true;}else if((e.KeyCode==Keys.Enter||e.KeyCode==Keys.Down)&&games.Items.Count>0){games.Focus();if(games.SelectedIndex<0)games.SelectedIndex=0;e.SuppressKeyPress=true;}};EnableGameFolderDrop(games);EnableGameFolderDrop(sidebar);}
    protected override bool ProcessCmdKey(ref Message message,Keys key){if(key==(Keys.Control|Keys.F)){librarySearch.Focus();librarySearch.SelectAll();return true;}return base.ProcessCmdKey(ref message,key);}
    void ReloadGames(CustomGame selected,bool filterOnly=false){
        if(store.Settings.CustomGames==null)store.Settings.CustomGames=new System.Collections.Generic.List<CustomGame>();var before=games.SelectedItem as LibraryEntry??activeLibraryEntry;
        var rows=new System.Collections.Generic.List<LibraryEntry>{new LibraryEntry{Kind=0,Name="Minecraft²"},new LibraryEntry{Kind=1,Name="Star Dash"},new LibraryEntry{Kind=2,Name="FTL: Multiverse"}};rows.AddRange(store.Settings.CustomGames.Where(g=>g!=null).Select(g=>new LibraryEntry{Kind=3,Custom=g}));
        reloadingLibrary=true;games.BeginUpdate();try{if(selected!=null&&!LibrarySearch.Matches(selected.Name,librarySearch.Text))librarySearch.Clear();var visible=rows.Where(r=>LibrarySearch.Matches(r.ToString(),librarySearch.Text)).OrderBy(r=>r.ToString(),StringComparer.OrdinalIgnoreCase).ThenBy(r=>r.Custom==null?"":r.Custom.Id,StringComparer.Ordinal).ToArray();activeLibraryEntry=selected!=null?rows.First(r=>r.Custom==selected):before==null?rows.First():rows.FirstOrDefault(r=>before.Custom==null?r.Custom==null&&r.Kind==before.Kind:r.Custom!=null&&r.Custom.Id==before.Custom.Id)??rows.First(r=>r.Kind==0);games.Items.Clear();games.Items.AddRange(visible);var selectedRow=visible.FirstOrDefault(r=>activeLibraryEntry.Custom==null?r.Custom==null&&r.Kind==activeLibraryEntry.Kind:r.Custom!=null&&r.Custom.Id==activeLibraryEntry.Custom.Id);if(selectedRow!=null)games.SelectedItem=selectedRow;noLibraryMatches.Visible=visible.Length==0;if(librarySearchHost!=null)librarySearchHost.Height=visible.Length==0?64:42;}finally{games.EndUpdate();reloadingLibrary=false;if(libraryScrollbar!=null)libraryScrollbar.Invalidate();UpdateIconAnimationPlayback();}if(gameBody!=null&&!filterOnly)SwitchGame();
    }
}
}
