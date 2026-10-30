using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Astera
{
    // Windows' Common Item Dialog provides an address bar, search and pasted paths.
    sealed class ExplorerFolderDialog : IDisposable
    {
        public string Description = "Choose a folder", SelectedPath = "";
        public DialogResult ShowDialog()
        {
            return ShowDialog(null);
        }

        public DialogResult ShowDialog(IWin32Window owner)
        {
            var dialog = Create(Description, SelectedPath);
            try
            {
                int result = dialog.Show(owner == null ? IntPtr.Zero : owner.Handle);
                if (result == unchecked((int)0x800704C7))
                    return DialogResult.Cancel;
                Marshal.ThrowExceptionForHR(result);
                IShellItem item;
                dialog.GetResult(out item);
                try
                {
                    SelectedPath = FileSystemPath(item);
                }
                finally
                {
                    Marshal.FinalReleaseComObject(item);
                }

                return DialogResult.OK;
            }
            finally
            {
                Marshal.FinalReleaseComObject(dialog);
            }
        }

        internal static string InitialFolder(string value)
        {
            if (!String.IsNullOrWhiteSpace(value))
                try
                {
                    string path = Path.GetFullPath(Environment.ExpandEnvironmentVariables(value.Trim().Trim('"')));
                    if (File.Exists(path))
                        path = Path.GetDirectoryName(path);
                    while (!String.IsNullOrEmpty(path))
                    {
                        if (Directory.Exists(path))
                            return path;
                        path = Path.GetDirectoryName(path);
                    }
                }
                catch (ArgumentException)
                {
                }
                catch (NotSupportedException)
                {
                }
                catch (PathTooLongException)
                {
                }

            return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }

        static IFileDialog Create(string title, string initial)
        {
            var dialog = (IFileDialog)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7")));
            try
            {
                uint options;
                dialog.GetOptions(out options);
                dialog.SetOptions(options | 0x20U | 0x40U | 0x800U | 0x8U | 0x02000000U);
                dialog.SetTitle(title);
                dialog.SetOkButtonLabel("Select folder");
                Guid id = typeof(IShellItem).GUID;
                IShellItem folder;
                Marshal.ThrowExceptionForHR(SHCreateItemFromParsingName(InitialFolder(initial), IntPtr.Zero, ref id, out folder));
                try
                {
                    dialog.SetFolder(folder);
                }
                finally
                {
                    Marshal.FinalReleaseComObject(folder);
                }

                return dialog;
            }
            catch
            {
                Marshal.FinalReleaseComObject(dialog);
                throw;
            }
        }

        internal static string Probe(string initial)
        {
            var dialog = Create("Folder picker verification", initial);
            try
            {
                uint options;
                dialog.GetOptions(out options);
                if ((options & 0x860U) != 0x860U)
                    throw new Exception("Explorer folder options were not set");
                IShellItem folder;
                dialog.GetFolder(out folder);
                try
                {
                    return FileSystemPath(folder);
                }
                finally
                {
                    Marshal.FinalReleaseComObject(folder);
                }
            }
            finally
            {
                Marshal.FinalReleaseComObject(dialog);
            }
        }

        static string FileSystemPath(IShellItem item)
        {
            IntPtr text;
            item.GetDisplayName(0x80058000U, out text);
            try
            {
                return Marshal.PtrToStringUni(text);
            }
            finally
            {
                Marshal.FreeCoTaskMem(text);
            }
        }

        public void Dispose()
        {
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        static extern int SHCreateItemFromParsingName(string path, IntPtr binding, ref Guid id, out IShellItem item);
        [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IShellItem
        {
            void BindToHandler(IntPtr context, ref Guid handler, ref Guid id, out IntPtr result);
            void GetParent(out IShellItem parent);
            void GetDisplayName(uint type, out IntPtr name);
            void GetAttributes(uint mask, out uint attributes);
            void Compare(IShellItem other, uint hint, out int order);
        }

        [ComImport, Guid("42F85136-DB7E-439C-85F1-E4075D135FC8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IFileDialog
        {
            [PreserveSig]
            int Show(IntPtr parent);
            void SetFileTypes(uint count, IntPtr filters);
            void SetFileTypeIndex(uint index);
            void GetFileTypeIndex(out uint index);
            void Advise(IntPtr events, out uint cookie);
            void Unadvise(uint cookie);
            void SetOptions(uint options);
            void GetOptions(out uint options);
            void SetDefaultFolder(IShellItem folder);
            void SetFolder(IShellItem folder);
            void GetFolder(out IShellItem folder);
            void GetCurrentSelection(out IShellItem item);
            void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);
            void GetFileName(out IntPtr name);
            void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);
            void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
            void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
            void GetResult(out IShellItem item);
            void AddPlace(IShellItem item, uint placement);
            void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string extension);
            void Close(int result);
            void SetClientGuid(ref Guid id);
            void ClearClientData();
            void SetFilter(IntPtr filter);
        }
    }
}
