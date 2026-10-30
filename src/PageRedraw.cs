using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Astera
{
    sealed class PageRedraw : IDisposable
    {
        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr handle, int message, IntPtr w, IntPtr l);
        readonly Control page;
        internal PageRedraw(Control control)
        {
            page = control;
            if (page.IsHandleCreated)
                SendMessage(page.Handle, 0xB, IntPtr.Zero, IntPtr.Zero);
        }

        public void Dispose()
        {
            if (!page.IsDisposed && page.IsHandleCreated)
            {
                SendMessage(page.Handle, 0xB, new IntPtr(1), IntPtr.Zero);
                page.Invalidate(true);
            }
        }

        internal static void BufferLayout(Control parent)
        {
            if (parent is Panel || parent is Label)
                typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(parent, true, null);
            foreach (Control child in parent.Controls)
                BufferLayout(child);
        }
    }
}
