using System;
using System.Drawing;
using System.Reflection;
namespace Astera {
static class GameArtwork {
    internal static readonly Image FtlIcon=Load("Astera.Games.FtlMultiverseIcon");
    internal static readonly Image StarDashIcon=Load("Astera.Games.StarDashIcon");
    internal static Image Load(string name){using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(name))if(stream!=null)using(var image=Image.FromStream(stream))return new Bitmap(image);return null;}
    internal static void Contain(Graphics graphics,Image image,Rectangle target){if(image==null||target.Width<1||target.Height<1)return;float scale=Math.Min(target.Width/(float)image.Width,target.Height/(float)image.Height);float width=image.Width*scale,height=image.Height*scale;graphics.DrawImage(image,target.X+(target.Width-width)/2,target.Y+(target.Height-height)/2,width,height);}
}
}