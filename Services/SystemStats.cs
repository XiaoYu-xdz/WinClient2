using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace WinClient2.Services;

/// <summary>
/// 系统状态采集：CPU 占用（GetSystemTimes）、内存（GlobalMemoryStatusEx）、
/// 网速（NetworkInterface 采样差分）、电池（GetSystemPowerStatus）。
/// Tick() 每秒调用一次，返回快照。
/// </summary>
public sealed class SystemStats
{
    public static SystemStats Instance { get; } = new();

    private readonly object _lock = new();

    // CPU
    private long _prevIdle, _prevKernel, _prevUser;

    // 网络
    private long _prevBytesIn, _prevBytesOut;
    private DateTime _prevNetTime;

    private SystemStats()
    {
        SampleCpu();
        SampleNet(out _, out _);
    }

    public double CpuPercent { get; private set; }
    public double RamUsedMb { get; private set; }
    public double RamTotalMb { get; private set; }
    public double NetDownKbps { get; private set; }
    public double NetUpKbps { get; private set; }
    public int BatteryPercent { get; private set; } = -1;

    /// <summary>每秒调用：刷新所有指标。</summary>
    public void Tick()
    {
        lock (_lock)
        {
            CpuPercent = SampleCpu();
            SampleRam(out var usedMb, out var totalMb);
            RamUsedMb = usedMb;
            RamTotalMb = totalMb;
            SampleNet(out var downBps, out var upBps);
            NetDownKbps = downBps / 1024.0;
            NetUpKbps = upBps / 1024.0;
            BatteryPercent = SampleBattery();
        }
    }

    private double SampleCpu()
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user)) return 0;
        var idleTicks = idle;
        var kernelTicks = kernel;
        var userTicks = user;

        if (_prevKernel == 0 || _prevUser == 0 || _prevIdle == 0)
        {
            _prevIdle = idleTicks;
            _prevKernel = kernelTicks;
            _prevUser = userTicks;
            return 0;
        }

        var idleDelta = idleTicks - _prevIdle;
        var kernelDelta = kernelTicks - _prevKernel;
        var userDelta = userTicks - _prevUser;
        var total = kernelDelta + userDelta;
        var busy = total - idleDelta;

        _prevIdle = idleTicks;
        _prevKernel = kernelTicks;
        _prevUser = userTicks;

        return total <= 0 ? 0 : Math.Clamp(busy * 100.0 / total, 0, 100);
    }

    private static void SampleRam(out double usedMb, out double totalMb)
    {
        var status = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (GlobalMemoryStatusEx(ref status))
        {
            totalMb = status.ullTotalPhys / 1024.0 / 1024.0;
            usedMb = (status.ullTotalPhys - status.ullAvailPhys) / 1024.0 / 1024.0;
        }
        else
        {
            usedMb = 0;
            totalMb = 0;
        }
    }

    private void SampleNet(out double downBps, out double upBps)
    {
        long down = 0;
        long up = 0;
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    continue;
                var stats = nic.GetIPv4Statistics();
                down += stats.BytesReceived;
                up += stats.BytesSent;
            }
        }
        catch
        {
            downBps = 0;
            upBps = 0;
            return;
        }

        var now = DateTime.UtcNow;
        if (_prevNetTime != default)
        {
            var elapsed = (now - _prevNetTime).TotalSeconds;
            if (elapsed > 0)
            {
                downBps = Math.Max(0, (down - _prevBytesIn) / elapsed);
                upBps = Math.Max(0, (up - _prevBytesOut) / elapsed);
                _prevBytesIn = down;
                _prevBytesOut = up;
                _prevNetTime = now;
                return;
            }
        }
        _prevBytesIn = down;
        _prevBytesOut = up;
        _prevNetTime = now;
        downBps = 0;
        upBps = 0;
    }

    private static int SampleBattery()
    {
        var status = new SYSTEM_POWER_STATUS();
        if (GetSystemPowerStatus(ref status) && status.BatteryLifePercent != 255)
            return status.BatteryLifePercent;
        return -1;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(out long idle, out long kernel, out long user);

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEM_POWER_STATUS
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public uint BatteryLifeTime;
        public uint BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll")]
    private static extern bool GetSystemPowerStatus(ref SYSTEM_POWER_STATUS lpSystemPowerStatus);
}
