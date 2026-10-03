using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace BetterDesktop
{
    // ===== IContextMenu COM 接口 =====
    [ComVisible(true)]
    [Guid("000214e4-0000-0000-c000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IContextMenu
    {
        [PreserveSig]
        int QueryContextMenu(IntPtr hMenu, uint indexMenu, int idCmdFirst, int idCmdLast, uint uFlags);

        [PreserveSig]
        int InvokeCommand(IntPtr pici);

        [PreserveSig]
        int GetCommandString(int idCmd, uint uFlags, IntPtr pReserved, StringBuilder pszName, int cchMax);
    }

    // ===== IShellExtInit 接口 =====
    [ComVisible(true)]
    [Guid("000214e8-0000-0000-c000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IShellExtInit
    {
        [PreserveSig]
        int Initialize(IntPtr pidlFolder, IntPtr pDataObject, IntPtr hkeyProgID);
    }

    // ===== Win32 帮助方法 =====
    internal static class Win32Helper
    {
        internal const int CF_HDROP = 15;

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        internal static extern int DragQueryFile(IntPtr hDrop, int iFile, IntPtr lpszFile, int cch);

        internal static string GetPathFromDataObject(IntPtr pDataObject)
        {
            try
            {
                object dataObj = Marshal.GetObjectForIUnknown(pDataObject);
                if (dataObj == null) return null;

                var ido = (System.Runtime.InteropServices.ComTypes.IDataObject)dataObj;

                var fe = new System.Runtime.InteropServices.ComTypes.FORMATETC();
                fe.cfFormat = (short)CF_HDROP;
                fe.ptd = IntPtr.Zero;
                fe.dwAspect = System.Runtime.InteropServices.ComTypes.DVASPECT.DVASPECT_CONTENT;
                fe.lindex = -1;
                fe.tymed = System.Runtime.InteropServices.ComTypes.TYMED.TYMED_HGLOBAL;

                var stg = new System.Runtime.InteropServices.ComTypes.STGMEDIUM();
                ido.GetData(ref fe, out stg);

                if (stg.unionmember != IntPtr.Zero)
                {
                    IntPtr pathPtr = Marshal.AllocHGlobal(260 * 2);
                    int len = DragQueryFile(stg.unionmember, 0, pathPtr, 260);
                    if (len > 0)
                    {
                        string path = Marshal.PtrToStringUni(pathPtr);
                        Marshal.FreeHGlobal(pathPtr);
                        return path;
                    }
                    Marshal.FreeHGlobal(pathPtr);
                }
            }
            catch (Exception ex)
            {
                Logger.Write("GetPathFromDataObject: " + ex.Message);
            }
            return null;
        }
    }

    /// <summary>
    /// 右键菜单处理器
    /// 实现 IContextMenu + IShellExtInit
    /// 当用户右键文件夹时检测 Shift 键，弹出信息窗口
    /// </summary>
    [ComVisible(true)]
    [Guid("B8E2D3F1-5A7C-4E9B-8D1F-3C6A9B2E7F4D")]
    [ProgId("BetterDesktop.InfoTipHandler")]
    public class InfoTipHandler : IContextMenu, IShellExtInit
    {
        private string _selectedPath = string.Empty;
        private PipeClient _pipeClient;

        public InfoTipHandler()
        {
            _pipeClient = new PipeClient();
            Logger.Write("InfoTipHandler 已创建 (IContextMenu 实现)");
        }

        // ===== IShellExtInit =====
        public int Initialize(IntPtr pidlFolder, IntPtr pDataObject, IntPtr hkeyProgID)
        {
            string path = Win32Helper.GetPathFromDataObject(pDataObject);
            if (!string.IsNullOrEmpty(path))
            {
                _selectedPath = path;
                Logger.Write("选中路径: " + path);
            }
            return 0;
        }

        // ===== IContextMenu.QueryContextMenu =====
        public int QueryContextMenu(IntPtr hMenu, uint indexMenu, int idCmdFirst, int idCmdLast, uint uFlags)
        {
            Logger.Write("QueryContextMenu 被调用, Shift=" + ((Control.ModifierKeys & Keys.Shift) == Keys.Shift));

            // 检测 Shift 是否按下
            if ((Control.ModifierKeys & Keys.Shift) == Keys.Shift)
            {
                Logger.Write("Shift 按下，触发弹出窗口");
                if (!string.IsNullOrEmpty(_selectedPath) && Directory.Exists(_selectedPath))
                {
                    _pipeClient.SendShowPopup(_selectedPath);
                }
            }

            // 不添加任何菜单项，返回 0
            return 0;
        }

        // ===== IContextMenu.InvokeCommand =====
        public int InvokeCommand(IntPtr pici)
        {
            return 0; // 没有菜单项，不会调用
        }

        // ===== IContextMenu.GetCommandString =====
        public int GetCommandString(int idCmd, uint uFlags, IntPtr pReserved, StringBuilder pszName, int cchMax)
        {
            return 0; // 没有菜单项，不会调用
        }
    }
}