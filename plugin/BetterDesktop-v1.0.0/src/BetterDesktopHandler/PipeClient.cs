using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace BetterDesktop
{
    /// <summary>
    /// 命名管道客户端 — 运行在 DLL（Explorer.exe）进程中
    /// 负责向 BetterDesktopPopup.exe 发送消息，触发弹出窗口的显示/隐藏
    /// </summary>
    internal class PipeClient
    {
        // 命名管道名称，必须与 EXE 端一致
        private const string PipeName = "BetterDesktopInfoTipPipe";

        /// <summary>
        /// 发送 "显示弹出窗口" 消息
        /// </summary>
        /// <param name="folderPath">目标文件夹路径</param>
        public void SendShowPopup(string folderPath)
        {
            try
            {
                // 确保 EXE 已启动
                EnsurePopupExeRunning();

                // 构造消息
                var msg = new PipeMessage
                {
                    Action = "ShowPopup",
                    FolderPath = folderPath,
                    MouseX = Cursor.Position.X,  // 需要 System.Windows.Forms
                    MouseY = Cursor.Position.Y
                };

                SendMessage(msg);
            }
            catch (Exception ex)
            {
                Logger.Write($"[BetterDesktop] SendShowPopup 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 发送 "隐藏弹出窗口" 消息
        /// </summary>
        public void SendHidePopup()
        {
            try
            {
                var msg = new PipeMessage
                {
                    Action = "HidePopup"
                };
                SendMessage(msg);
            }
            catch (Exception ex)
            {
                Logger.Write($"[BetterDesktop] SendHidePopup 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 通过命名管道发送消息（JSON 序列化）
        /// </summary>
        private void SendMessage(PipeMessage msg)
        {
            // 序列化为 JSON
            var serializer = new DataContractJsonSerializer(typeof(PipeMessage));
            byte[] buffer;
            using (var ms = new MemoryStream())
            {
                serializer.WriteObject(ms, msg);
                buffer = ms.ToArray();
            }

            // 重试连接管道（EXE 可能还没完全启动完成）
            int retryCount = 3;
            while (retryCount > 0)
            {
                try
                {
                    using (var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.Out))
                    {
                        pipe.Connect(500); // 500ms 超时
                        pipe.Write(buffer, 0, buffer.Length);
                        pipe.Flush();
                    }
                    return; // 成功
                }
                catch (TimeoutException)
                {
                    retryCount--;
                    if (retryCount > 0)
                        Thread.Sleep(300);
                }
            }

            Logger.Write("[BetterDesktop] 无法连接到 Popup EXE 管道");
        }

        /// <summary>
        /// 确保 BetterDesktopPopup.exe 正在运行
        /// 如果未运行，则启动它
        /// </summary>
        private void EnsurePopupExeRunning()
        {
            // 检查是否已有同名进程在运行
            var existing = Process.GetProcessesByName("BetterDesktopPopup");
            if (existing.Length > 0)
                return;

            // 从 DLL 同目录下查找 EXE（推荐：把 EXE 复制到 DLL 目录）
            string dllDir = Path.GetDirectoryName(typeof(InfoTipHandler).Assembly.Location);
            string exePath = Path.Combine(dllDir, "BetterDesktopPopup.exe");

            // 也从项目输出目录查找（调试时未复制的情况）
            if (!File.Exists(exePath))
            {
                // 从 BetterDesktopHandler\bin\x64\Debug 回退到
                // BetterDesktopPopup\bin\x64\Debug\BetterDesktopPopup.exe
                exePath = Path.GetFullPath(Path.Combine(
                    dllDir, "..", "..", "..", "..",
                    "BetterDesktopPopup", "bin", "x64", "Debug", "BetterDesktopPopup.exe"));
            }

            if (!File.Exists(exePath))
            {
                Logger.Write($"[BetterDesktop] 找不到 BetterDesktopPopup.exe，路径: {exePath}");
                return;
            }

            Logger.Write($"[BetterDesktop] 启动 Popup EXE: {exePath}");
            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = Path.GetDirectoryName(exePath),
                UseShellExecute = false
            });

            // 等待 EXE 启动并创建管道服务器
            Thread.Sleep(800);
        }
    }
}