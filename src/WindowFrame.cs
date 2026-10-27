using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
namespace Astera {
sealed partial class LibraryForm {
    Button windowMaximize;int instanceShowMessage;
    [DllImport("user32.dll")]static extern bool ReleaseCapture();
    [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr window,int message,IntPtr wParam,IntPtr lParam);
    void DragTitleBar(object sender,MouseEventArgs e){if(e.Button!=MouseButtons.Left)return;if(e.Clicks==2){ToggleMaximize();return;}ReleaseCapture();SendMessage(Handle,0x00A1,new IntPtr(2),IntPtr.Zero);}
    void ToggleMaximize(){if(WindowState==FormWindowState.Maximized)WindowState=FormWindowState.Normal;else{MaximizedBounds=Screen.FromControl(this).WorkingArea;WindowState=FormWindowState.Maximized;}}
    Button CaptionButton(ButtonSymbol symbol,string name,EventHandler action){var button=Button("",action);var skin=(AsteraButton)button;skin.Symbol=symbol;skin.Role=ButtonRole.Quiet;skin.Padding=new Padding(8);skin.MinimumSize=new Size(40,40);skin.Margin=new Padding(0,0,4,0);skin.AccessibleName=name;return button;}
    protected override void WndProc(ref Message message){
        if(instanceShowMessage!=0&&message.Msg==instanceShowMessage){ShowWindow();return;}
        const int hitTest=0x84;
        base.WndProc(ref message);
        if(message.Msg!=hitTest||WindowState==FormWindowState.Maximized)return;
        long value=message.LParam.ToInt64();Point point=PointToClient(new Point((short)(value&0xffff),(short)((value>>16)&0xffff)));int edge=6;
        bool left=point.X<edge,right=point.X>=ClientSize.Width-edge,top=point.Y<edge,bottom=point.Y>=ClientSize.Height-edge;
        if(top&&left)message.Result=new IntPtr(13);else if(top&&right)message.Result=new IntPtr(14);else if(bottom&&left)message.Result=new IntPtr(16);else if(bottom&&right)message.Result=new IntPtr(17);else if(left)message.Result=new IntPtr(10);else if(right)message.Result=new IntPtr(11);else if(top)message.Result=new IntPtr(12);else if(bottom)message.Result=new IntPtr(15);
    }
}
}
