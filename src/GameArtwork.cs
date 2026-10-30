using System;
using System.Drawing;
using System.Reflection;
namespace Astera {
static class GameArtwork {
    internal static readonly Image VanillaMinecraftIcon=VanillaIcon();
    static Image VanillaIcon(){var image=new Bitmap(48,48);using(var g=Graphics.FromImage(image)){g.Clear(Color.Transparent);using(var side=new SolidBrush(Color.FromArgb(121,79,45)))g.FillRectangle(side,5,14,38,29);using(var grass=new SolidBrush(Color.FromArgb(92,166,54)))g.FillRectangle(grass,5,5,38,15);using(var light=new SolidBrush(Color.FromArgb(145,195,79)))g.FillRectangle(light,9,7,30,6);using(var dirt=new SolidBrush(Color.FromArgb(165,115,68))){g.FillRectangle(dirt,10,25,8,6);g.FillRectangle(dirt,28,32,9,5);}}return image;}
    internal static readonly Image MinecraftIcon=Load("Astera.Games.MinecraftSquaredIcon");
    internal static readonly Image FtlIcon=Load("Astera.Games.FtlMultiverseIcon");
    internal static readonly Image StarDashIcon=Load("Astera.Games.StarDashIcon");
    internal static Image Load(string name){using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(name))if(stream!=null)using(var image=Image.FromStream(stream))return new Bitmap(image);return null;}
    internal static void Contain(Graphics graphics,Image image,Rectangle target){if(image==null||target.Width<1||target.Height<1)return;float scale=Math.Min(target.Width/(float)image.Width,target.Height/(float)image.Height);float width=image.Width*scale,height=image.Height*scale;graphics.DrawImage(image,target.X+(target.Width-width)/2,target.Y+(target.Height-height)/2,width,height);}
}
}