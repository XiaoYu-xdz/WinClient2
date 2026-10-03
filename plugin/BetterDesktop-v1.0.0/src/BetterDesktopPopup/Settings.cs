using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace BetterDesktopPopup
{
    /// <summary>
    /// 运行时可调配置：读取 exe 同目录 config.json（UTF-8）
    /// 通过 FileSystemWatcher 监听，外部程序修改文件后立即生效，无需重启
    /// </summary>
    internal static class Settings
    {
        /// <summary>鼠标移出文件夹后，主窗口延迟关闭（毫秒）</summary>
        public static int LingerMs = 1500;

        /// <summary>级联子窗口延迟关闭（毫秒）</summary>
        public static int ChildCloseMs = 1500;

        /// <summary>弹窗间缝隙宽限期（毫秒）</summary>
        public static int GraceMs = 500;

        /// <summary>触发热键的虚拟键码（默认右 Shift）</summary>
        public static int HotKeyVk = NativeMethods.VK_RSHIFT;

        /// <summary>触发热键的名称（config.json 中 hotkey 字段的字符串形式）</summary>
        public static string HotKeyName = "RShift";

        private static FileSystemWatcher _watcher;
        private static string _configPath;
        private static DateTime _lastLoadTime = DateTime.MinValue;
        private static System.Threading.Timer _reloadTimer;

        private const string ConfigName = "config.json";

        public static void Initialize()
        {
            try
            {
                _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigName);
                WriteDefaultIfMissing();
                LoadConfig();

                var watcher = new FileSystemWatcher
                {
                    Path = Path.GetDirectoryName(_configPath),
                    Filter = ConfigName,
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName
                };
                watcher.Changed += OnConfigChanged;
                watcher.Created += OnConfigChanged;
                watcher.EnableRaisingEvents = true;
                _watcher = watcher;

                Logger.Write("[Settings] 配置已加载: " + _configPath);
            }
            catch (Exception ex)
            {
                Logger.Write("[Settings] 初始化失败: " + ex.Message);
            }
        }

        private static void WriteDefaultIfMissing()
        {
            if (File.Exists(_configPath)) return;

            string json = "{\n" +
                "  \"_comment\": \"悬停终止后窗口保持显示的毫秒数，修改后保存本文件立即生效，无需重启\",\n" +
                "  \"lingerMs\": 1500,\n" +
                "  \"childCloseMs\": 1500,\n" +
                "  \"graceMs\": 500,\n" +
                "  \"hotkey\": \"RShift\"\n" +
                "}\n";
            File.WriteAllText(_configPath, json, new UTF8Encoding(false));
            Logger.Write("[Settings] 已生成默认配置: " + _configPath);
        }

        private static void OnConfigChanged(object sender, FileSystemEventArgs e)
        {
            _lastLoadTime = DateTime.Now;

            try
            {
                _reloadTimer?.Dispose();
                _reloadTimer = new System.Threading.Timer(
                    _ => LoadConfig(),
                    null,
                    300,
                    System.Threading.Timeout.Infinite);
            }
            catch { }
        }

        private static void LoadConfig()
        {
            try
            {
                _lastLoadTime = DateTime.Now;

                if (!File.Exists(_configPath)) return;

                string json = File.ReadAllText(_configPath, Encoding.UTF8);
                var serializer = new DataContractJsonSerializer(typeof(ConfigData));
                using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    var data = serializer.ReadObject(ms) as ConfigData;
                    if (data == null) return;

                    if (data.LingerMs > 0) LingerMs = data.LingerMs;
                    if (data.ChildCloseMs > 0) ChildCloseMs = data.ChildCloseMs;
                    if (data.GraceMs > 0) GraceMs = data.GraceMs;

                    if (!string.IsNullOrEmpty(data.HotKey))
                    {
                        int vk = ParseKeyVk(data.HotKey);
                        if (vk > 0)
                        {
                            HotKeyVk = vk;
                            HotKeyName = data.HotKey;
                        }
                    }
                }

                Logger.Write("[Settings] 已重载: lingerMs=" + LingerMs + ", childCloseMs=" + ChildCloseMs + ", graceMs=" + GraceMs);
            }
            catch (Exception ex)
            {
                Logger.Write("[Settings] 读取配置失败，保持原值: " + ex.Message);
            }
        }

        /// <summary>把 config.json 中的按键名解析为虚拟键码（对应 WinClient2 写入的名称格式）。</summary>
        private static int ParseKeyVk(string name)
        {
            switch (name)
            {
                case "LShiftKey":
                case "LShift":
                    return NativeMethods.VK_LSHIFT;
                case "RShift":
                    return NativeMethods.VK_RSHIFT;
                case "LControlKey":
                case "LCtrl":
                    return NativeMethods.VK_LCONTROL;
                case "LMenu":
                case "Alt":
                    return NativeMethods.VK_LMENU;
                case "LWin":
                    return NativeMethods.VK_LWIN;
                case "Space":
                    return 0x20;
                case "Enter":
                    return 0x0D;
                case "Tab":
                    return 0x09;
            }

            if (name.Length == 1)
            {
                char c = char.ToUpperInvariant(name[0]);
                if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))
                    return c;
            }

            if (name.Length == 3 && name[0] == 'F' && int.TryParse(name.Substring(1), out int fn) && fn >= 1 && fn <= 12)
                return 0x6F + fn;

            return 0;
        }
    }

    /// <summary>
    /// config.json 数据结构（JSON 序列化用）
    /// </summary>
    [System.Runtime.Serialization.DataContract]
    internal class ConfigData
    {
        [System.Runtime.Serialization.DataMember(Name = "lingerMs")]
        public int LingerMs { get; set; }

        [System.Runtime.Serialization.DataMember(Name = "childCloseMs")]
        public int ChildCloseMs { get; set; }

        [System.Runtime.Serialization.DataMember(Name = "graceMs")]
        public int GraceMs { get; set; }

        [System.Runtime.Serialization.DataMember(Name = "hotkey")]
        public string HotKey { get; set; }
    }
}
