using System;
using System.Diagnostics;
using System.IO;

namespace BetterDesktopPopup
{
    /// <summary>
    /// 简单日志工具 — 写入 %TEMP%\BetterDesktop_Popup.log
    /// </summary>
    internal static class Logger
    {
        private static readonly string LogPath = Path.Combine(
            Path.GetTempPath(), "BetterDesktop_Popup.log");

        private static readonly object _lock = new object();

        public static void Write(string message)
        {
            string line = "[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] " + message;
            Debug.WriteLine(line);

            lock (_lock)
            {
                try
                {
                    File.AppendAllText(LogPath, line + Environment.NewLine);
                }
                catch { }
            }
        }

        public static string GetLogPath() => LogPath;
    }
}