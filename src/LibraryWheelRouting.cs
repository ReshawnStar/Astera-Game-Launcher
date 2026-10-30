using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Astera
{
    // Route only this window's wheel messages to the library actually under the pointer.
    // Do not move keyboard focus: typing in search must remain possible after scrolling.
    sealed class LibraryWheelRouting : IMessageFilter, IDisposable
    {
        readonly Form owner;
        readonly Control viewport;
        readonly GameListBox list;
        readonly LibraryScrollbar scrollbar;
        [DllImport("user32.dll")]
        static extern IntPtr WindowFromPoint(Point point);
        internal LibraryWheelRouting(Form form, Control host, GameListBox games, LibraryScrollbar thumb)
        {
            owner = form;
            viewport = host;
            list = games;
            scrollbar = thumb;
            Application.AddMessageFilter(this);
        }

        static bool Within(Control child, Control parent)
        {
            for (var at = child; at != null; at = at.Parent)
                if (at == parent)
                    return true;
            return false;
        }

        public bool PreFilterMessage(ref Message message)
        {
            if (message.Msg != 0x20A || owner.IsDisposed || !owner.Visible || !owner.Enabled || !list.Enabled || !viewport.Visible)
                return false;
            var source = Control.FromChildHandle(message.HWnd);
            if (source == null || source.FindForm() != owner)
                return false;
            long location = message.LParam.ToInt64();
            var point = new Point((short)(location & 0xffff), (short)((location >> 16) & 0xffff));
            var hit = Control.FromChildHandle(WindowFromPoint(point));
            if (!Within(hit, viewport))
                return false;
            scrollbar.ScrollWheel((short)((message.WParam.ToInt64() >> 16) & 0xffff));
            return true;
        }

        public void Dispose()
        {
            Application.RemoveMessageFilter(this);
        }
    }
}
