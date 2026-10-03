using System.Runtime.Serialization;

namespace BetterDesktopPopup
{
    /// <summary>
    /// 命名管道通信消息类型（与 DLL 端一致）
    /// DLL 与 EXE 之间通过 JSON 序列化交换此消息
    /// </summary>
    [DataContract]
    public class PipeMessage
    {
        /// <summary>操作类型: "ShowPopup" | "HidePopup" | "NavigateFolder"</summary>
        [DataMember]
        public string Action { get; set; }

        /// <summary>目标文件夹路径</summary>
        [DataMember]
        public string FolderPath { get; set; }

        /// <summary>鼠标屏幕 X 坐标（用于定位弹出窗口）</summary>
        [DataMember]
        public int MouseX { get; set; }

        /// <summary>鼠标屏幕 Y 坐标（用于定位弹出窗口）</summary>
        [DataMember]
        public int MouseY { get; set; }
    }
}