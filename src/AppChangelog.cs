using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
namespace Astera {
static class AppChangelog {
    internal static string Text(){using(var source=Assembly.GetExecutingAssembly().GetManifestResourceStream("Astera.Notes.App")){if(source==null)return "No release notes available yet.";using(var reader=new StreamReader(source))return PatchNotes.PlayerText(reader.ReadToEnd());}}
    internal static void Show(IWin32Window owner){using(var dialog=new AsteraDialog{Text="Astera changelog",Size=new Size(760,650),StartPosition=FormStartPosition.CenterParent}){var content=new Panel{Dock=DockStyle.Fill,Padding=new Padding(18)};var text=new ScrollText{Text=Text(),Dock=DockStyle.Fill,ReadOnly=true,Multiline=true,ScrollBars=ScrollBars.Vertical,BackColor=Color.FromArgb(20,26,43),ForeColor=Color.FromArgb(224,231,250),BorderStyle=BorderStyle.None};var release=new AsteraButton{Text="View release on GitHub",Dock=DockStyle.Bottom};release.Click+=delegate{Process.Start(new ProcessStartInfo("https://github.com/ReshawnStar/Astera-Game-Launcher/releases/tag/astera-v"+SelfUpdate.CurrentVersion){UseShellExecute=true});};content.Controls.Add(text);content.Controls.Add(release);dialog.Controls.Add(content);dialog.ShowDialog(owner);}}
}
}
