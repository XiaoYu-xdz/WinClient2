using System;
using System.Diagnostics;
using System.IO;

namespace BetterDesktop
{
    /// <summary>
    /// 简单日志工具 — 写入 %TEMP%\BetterDesktop.log
    /// 替代 DebugView，方便直接查看
    /// </summary>
    internal static class Logger
    {
        private static readonly string LogPath = Path.Combine(
            Path.GetTempPath(), "BetterDesktop.log");

        private static readonly object _lock = new object();

        /// <summary>
        /// 写入日志（带时间戳）
        /// </summary>
        public static void Write(string message)
        {
            string line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
            Debug.WriteLine(line);  // 仍然同时输出到 DebugView

            lock (_lock)
            {
                try
                {
                    File.AppendAllText(LogPath, line + Environment.NewLine);
                }
                catch
                {
                    // 写入失败时忽略（如权限问题）
                }
            }
        }

        /// <summary>
        /// 获取日志文件路径
        /// </summary>
        public static string GetLogPath() => LogPath;
    }
}