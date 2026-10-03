using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Threading.Tasks;

namespace BetterDesktopPopup
{
    /// <summary>
    /// 命名管道服务端 — 运行在 BetterDesktopPopup.exe 进程中
    /// 监听来自 DLL（BetterDesktopHandler.dll）的管道消息
    /// 收到消息后更新 PopupForm 的状态
    /// </summary>
    internal class PipeServer : IDisposable
    {
        // 命名管道名称，必须与 DLL 端一致
        private const string PipeName = "BetterDesktopInfoTipPipe";

        private CancellationTokenSource _cts;
        private Task _listenTask;
        private readonly PopupForm _popupForm;

        public PipeServer(PopupForm popupForm)
        {
            _popupForm = popupForm ?? throw new ArgumentNullException(nameof(popupForm));
        }

        /// <summary>
        /// 启动管道服务监听（后台线程）
        /// </summary>
        public void Start()
        {
            _cts = new CancellationTokenSource();
            // 使用 Task.Run 在后台线程运行同步监听循环
            _listenTask = Task.Run(() => ListenLoop(_cts.Token), _cts.Token);
            Logger.Write("[BetterDesktopPipe] 管道服务已启动");
        }

        /// <summary>
        /// 停止管道服务
        /// </summary>
        public void Stop()
        {
            _cts?.Cancel();
            try
            {
                _listenTask?.Wait(2000);
            }
            catch (AggregateException)
            {
                // 忽略取消时的异常
            }
            Logger.Write("[BetterDesktopPipe] 管道服务已停止");
        }

        /// <summary>
        /// 监听循环 — 持续等待客户端连接并处理消息
        /// 使用同步 WaitForConnection（.NET Framework 4.8 兼容方式）
        /// </summary>
        private void ListenLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using (var pipe = new NamedPipeServerStream(
                        PipeName,
                        PipeDirection.In,
                        1,                          // 最多 1 个实例
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous))
                    {
                        // 通过异步委托实现带超时的同步等待
                        // 兼容 .NET Framework 4.8（没有 WaitForConnectionAsync）
                        var connectResult = pipe.BeginWaitForConnection(null, null);
                        bool connected = connectResult.AsyncWaitHandle.WaitOne(5000); // 5秒超时

                        if (!connected)
                        {
                            // 超时，检查是否被要求取消
                            if (token.IsCancellationRequested)
                                break;
                            continue;
                        }

                        // 完成连接
                        pipe.EndWaitForConnection(connectResult);
                        Logger.Write("[BetterDesktopPipe] 客户端已连接");

                        // 读取消息
                        var message = ReadMessage(pipe);
                        if (message != null)
                        {
                            Logger.Write($"[BetterDesktopPipe] 收到消息: Action={message.Action}, Path={message.FolderPath}");
                            ProcessMessage(message);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Write($"[BetterDesktopPipe] 监听异常: {ex.Message}");
                    // 短暂等待后继续监听
                    Thread.Sleep(500);
                }
            }
        }

        /// <summary>
        /// 从管道中读取并反序列化消息
        /// </summary>
        private PipeMessage ReadMessage(NamedPipeServerStream pipe)
        {
            using (var ms = new MemoryStream())
            {
                byte[] buffer = new byte[4096];
                int bytesRead;
                do
                {
                    bytesRead = pipe.Read(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                        ms.Write(buffer, 0, bytesRead);
                } while (bytesRead > 0 && ms.Length < 1024 * 1024); // 最大 1MB

                if (ms.Length == 0)
                    return null;

                ms.Seek(0, SeekOrigin.Begin);
                var serializer = new DataContractJsonSerializer(typeof(PipeMessage));
                return serializer.ReadObject(ms) as PipeMessage;
            }
        }

        /// <summary>
        /// 处理收到的消息，更新弹出窗口
        /// </summary>
        private void ProcessMessage(PipeMessage msg)
        {
            switch (msg.Action)
            {
                case "ShowPopup":
                    // 在主线程上更新 UI
                    _popupForm.Invoke((Action)(() =>
                    {
                        _popupForm.ShowForFolder(msg.FolderPath, msg.MouseX, msg.MouseY);
                    }));
                    break;

                case "HidePopup":
                    _popupForm.Invoke((Action)(() =>
                    {
                        _popupForm.HidePopup();
                    }));
                    break;

                default:
                    Logger.Write($"[BetterDesktopPipe] 未知消息类型: {msg.Action}");
                    break;
            }
        }

        public void Dispose()
        {
            Stop();
            _cts?.Dispose();
        }
    }
}