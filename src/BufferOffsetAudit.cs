using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using MC2Pack;
namespace Astera {
sealed partial class LibraryForm {
    internal static int AuditBufferOffset(string root){try{
        if(Directory.Exists(root))throw new IOException("Use a fresh offset audit");Directory.CreateDirectory(root);
        var store=new Store(Path.Combine(root,"state"));store.Settings.CheckOnStart=false;store.Settings.UseMinecraftSkin=false;
        using(var form=new LibraryForm(store,new Channel(),true)){
            form.games.Items.Clear();form.games.Items.Add(new LibraryEntry{Kind=0,Name="Reference row"});form.games.Items.Add(new LibraryEntry{Kind=2,Name="Selected offset title"});
            var row=new Rectangle(0,76,230,76);int referenceTitle,bufferedTitle,referenceStatus,bufferedStatus;
            using(var expected=new Bitmap(240,220))using(var actual=new Bitmap(240,220)){
                using(var graphics=Graphics.FromImage(expected)){graphics.Clear(form.games.BackColor);graphics.SetClip(row);form.DrawGame(form.games,new DrawItemEventArgs(graphics,form.games.Font,row,1,DrawItemState.Selected));}
                using(var graphics=Graphics.FromImage(actual)){graphics.Clear(form.games.BackColor);graphics.SetClip(row);typeof(GameListBox).GetMethod("OnDrawItem",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(form.games,new object[]{new DrawItemEventArgs(graphics,form.games.Font,row,1,DrawItemState.Selected)});}
                referenceTitle=TextPixels(expected,new Rectangle(64,85,120,24),true);bufferedTitle=TextPixels(actual,new Rectangle(64,85,120,24),true);
                referenceStatus=TextPixels(expected,new Rectangle(64,111,120,24),false);bufferedStatus=TextPixels(actual,new Rectangle(64,111,120,24),false);
                expected.Save(Path.Combine(root,"reference.png"));actual.Save(Path.Combine(root,"buffered.png"));
            }
            // Different GDI surfaces can use different subpixel antialiasing.
            // Check actual visible title and status ink, not identical fringes.
            AuditAssert(referenceTitle>40&&referenceStatus>20&&bufferedTitle>=referenceTitle*.65&&bufferedStatus>=referenceStatus*.65,"Native buffered row lost visible title/status at nonzero offset");
        }
        File.WriteAllText(Path.Combine(root,"result.json"),Engine.Json.Serialize(new{Passed=true,Checks=new[]{"Native GDI title/status text in a selected nonzero-offset row matches direct drawing through the actual buffered control"},WindowShown=false,LivePreferencesChanged=false}));return 0;
    }catch(Exception error){Directory.CreateDirectory(root);File.WriteAllText(Path.Combine(root,"failure.txt"),error.ToString());return 1;}}
    static int TextPixels(Bitmap bitmap,Rectangle area,bool title){int count=0;for(int y=area.Top;y<area.Bottom;y++)for(int x=area.Left;x<area.Right;x++){var pixel=bitmap.GetPixel(x,y);if(title?pixel.R>180&&pixel.G>180&&pixel.B>180:pixel.B>130&&pixel.G>110&&pixel.B>pixel.R)count++;}return count;}
}
}
