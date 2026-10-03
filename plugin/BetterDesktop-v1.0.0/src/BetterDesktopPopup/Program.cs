using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;

namespace BetterDesktopPopup
{
    /// <summary>
    /// BetterDesktopPopup.exe 入口点
    /// 
    /// 作为独立进程运行，通过全局鼠标钩子检测悬停文件夹 + Shift 键
    /// 同时通过命名管道接收来自 DLL 的消息（兼容旧方式）
    /// </summary>
    internal static class Program
    {
        private const string MutexName = "BetterDesktopPopup_Singleton_Mutex";

        [STAThread]
        private static void Main()
        {
            using (var mutex = new Mutex(true, MutexName, out bool createdNew))
            {
                if (!createdNew)
                {
                    Debug.WriteLine("[BetterDesktopPopup] 已有实例在运行，退出");
                    return;
                }

                Logger.Write("BetterDesktopPopup 启动");

                // 加载可调配置（config.json，外部程序可直接修改，即时生效）
                Settings.Initialize();

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                // 创建弹出窗口（初始隐藏）
                var popupForm = new PopupForm();

                // 安装全局鼠标钩子（检测悬停 + Shift）
                using (var mouseHook = new MouseHook(popupForm))
                {
                    mouseHook.Install();

                    // 命名管道服务（兼容旧方式，接收 DLL 消息）
                    using (var pipeServer = new PipeServer(popupForm))
                    {
                        pipeServer.Start();
                        Logger.Write("管道服务和鼠标钩子已启动");

                        // 运行消息循环
                        Application.Run();
                    }
                }

                Logger.Write("BetterDesktopPopup 退出");
            }
        }
    }
}