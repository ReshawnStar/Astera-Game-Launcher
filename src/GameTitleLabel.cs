using System;
using System.Drawing;
using System.Windows.Forms;
namespace Astera {
sealed class GameTitleLabel:Label {
 bool logo;internal bool MinecraftLogo {get{return logo;}set{if(logo==value)return;logo=value;Invalidate();}}
 protected override void OnPaint(PaintEventArgs e){var image=GameArtwork.VanillaMinecraftLogo;var source=GameArtwork.VanillaLogoBounds;if(!logo||image==null||source.IsEmpty){base.OnPaint(e);return;}float scale=Math.Min(Math.Max(1,Width)/(float)source.Width,Math.Max(1,Height-6)/(float)source.Height);float width=source.Width*scale,height=source.Height*scale;e.Graphics.DrawImage(image,new RectangleF(0,(Height-height)/2,width,height),source,GraphicsUnit.Pixel);}
}
}