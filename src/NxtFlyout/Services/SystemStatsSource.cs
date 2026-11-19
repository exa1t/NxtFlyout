using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using NxtFlyout.Core.Stats;

namespace NxtFlyout.Services;

/// <summary>
/// Measures CPU, memory, GPU, network and disk load for the taskbar stats panel, on a worker
/// thread, and reports each reading on the UI thread.
/// </summary>
/// <remarks>
/// <para>Everything here works without administrator rights and without any driver:</para>
/// <list type="bullet">
/// <item>CPU from <c>GetSystemTimes</c> and memory from <c>GlobalMemoryStatusEx</c>.</item>
/// <item>GPU use, GPU memory and disk rates from the same performance counters Task Manager
/// reads ("GPU Engine", "GPU Adapter Memory", "PhysicalDisk"), through the PDH API.</item>
/// <item>Network rates from the byte counts of the physical network adapters.</item>
/// <item>GPU temperature from the graphics driver's performance data (<c>D3DKMTQueryAdapterInfo</c>),
/// which only recent drivers fill in; when it is missing the figure is left out.</item>
/// </list>
/// <para>Rates are worked out from the difference between two readings, so the first reading
/// after starting has no rates and no CPU figure yet.</para>
/// </remarks>
internal sealed class SystemStatsSource : ISystemStatsSource, IDisposable
{
    private readonly Dispatcher _dispatcher;
    /// <summary>Guards the settings and the timer.</summary>
    private readonly object _gate = new();

    /// <summary>Guards the counters below, which only the worker thread (or Dispose) touches.</summary>
    private readonly object _measureLock = new();

    private Timer? _timer;
    private SystemStatsKinds _kinds;
    private TimeSpan _interval;
    private bool _reading;
    private bool _resetReaders;

    private Pdh? _pdh;
    private GpuTemperatureReader? _gpuTemperature;

    private ulong _lastIdle;
    private ulong _lastKernel;
    private ulong _lastUser;
    private bool _haveCpuBaseline;

    private long _lastSent;
    private long _lastReceived;
    private long _lastNetworkTicks;
    private bool _haveNetworkBaseline;

    private static bool s_loggedFailure;

    public SystemStatsSource(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public event EventHandler<SystemStats>? Updated;

    public void SetActive(SystemStatsKinds kinds, TimeSpan interval)
    {
        lock (_gate)
        {
            if (kinds == _kinds && interval == _interval)
            {
                return;
            }

            if (kinds != _kinds)
            {
                // Different figures need different counters. The next reading starts them
                // afresh (on the worker thread, never while one is in use).
                _resetReaders = true;
            }

            _kinds = kinds;
            _interval = interval;

            if (kinds == SystemStatsKinds.None)
            {
                _timer?.Dispose();
                _timer = null;
                return;
            }

            _timer ??= new Timer(_ => Read(), null, Timeout.Infinite, Timeout.Infinite);
            _timer.Change(TimeSpan.Zero, interval);
        }
    }

    public void Dispose()
    {
        SetActive(SystemStatsKinds.None, TimeSpan.Zero);
        lock (_measureLock)
        {
            ReleaseReaders();
        }
    }

    private void ReleaseReaders()
    {
        _pdh?.Dispose();
        _pdh = null;
        _gpuTemperature?.Dispose();
        _gpuTemperature = null;
        _haveCpuBaseline = false;
        _haveNetworkBaseline = false;
    }

    /// <summary>Takes one reading. Runs on a thread-pool thread.</summary>
    private void Read()
    {
        SystemStats stats;
        SystemStatsKinds kinds;
        bool reset;
        lock (_gate)
        {
            // A slow reading must not pile up behind itself.
            if (_reading || _kinds == SystemStatsKinds.None)
            {
                return;
            }

            _reading = true;
            kinds = _kinds;
            reset = _resetReaders;
            _resetReaders = false;
        }

        try
        {
            lock (_measureLock)
            {
                if (reset)
                {
                    ReleaseReaders();
                }

                stats = Measure(kinds);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            if (!s_loggedFailure)
            {
                s_loggedFailure = true;
                Log.Error("Reading the system stats failed.", ex);
            }

            stats = new SystemStats();
        }
        finally
        {
            lock (_gate)
            {
                _reading = false;
            }
        }

        _dispatcher.InvokeAsync(() =>
        {
            if (_kinds != SystemStatsKinds.None)
            {
                Updated?.Invoke(this, stats);
            }
        });
    }

    private SystemStats Measure(SystemStatsKinds kinds)
    {
        var stats = new SystemStats();

        if (kinds.HasFlag(SystemStatsKinds.Cpu))
        {
            stats = stats with { CpuPercent = ReadCpu() };
        }

        if (kinds.HasFlag(SystemStatsKinds.Ram))
        {
            var memory = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
            if (GlobalMemoryStatusEx(ref memory))
            {
                stats = stats with { RamPercent = memory.dwMemoryLoad };
            }
        }

        if (kinds.HasFlag(SystemStatsKinds.Network))
        {
            var (up, down) = ReadNetwork();
            stats = stats with { NetworkUpBytesPerSecond = up, NetworkDownBytesPerSecond = down };
        }

        const SystemStatsKinds countered = SystemStatsKinds.Gpu | SystemStatsKinds.Vram | SystemStatsKinds.Disk;
        if ((kinds & countered) != 0)
        {
            _pdh ??= new Pdh(kinds);
            stats = _pdh.Read(stats);
        }

        if (kinds.HasFlag(SystemStatsKinds.GpuTemperature))
        {
            _gpuTemperature ??= new GpuTemperatureReader();
            stats = stats with { GpuTemperatureC = _gpuTemperature.Read() };
        }

        return stats;
    }

    private double? ReadCpu()
    {
        if (!GetSystemTimes(out var idleTime, out var kernelTime, out var userTime))
        {
            return null;
        }

        ulong idle = idleTime, kernel = kernelTime, user = userTime;
        double? percent = null;
        if (_haveCpuBaseline)
        {
            // Kernel time includes idle time.
            var total = (kernel - _lastKernel) + (user - _lastUser);
            if (total > 0)
            {
                percent = 100.0 * (total - (idle - _lastIdle)) / total;
            }
        }

        _lastIdle = idle;
        _lastKernel = kernel;
        _lastUser = user;
        _haveCpuBaseline = true;
        return percent;
    }

    private (double? Up, double? Down) ReadNetwork()
    {
        long sent = 0, received = 0;
        foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (IsPhysicalAdapter(adapter))
            {
                var statistics = adapter.GetIPStatistics();
                sent += statistics.BytesSent;
                received += statistics.BytesReceived;
            }
        }

        var now = Environment.TickCount64;
        (double? Up, double? Down) rates = (null, null);
        if (_haveNetworkBaseline && now > _lastNetworkTicks)
        {
            var seconds = (now - _lastNetworkTicks) / 1000.0;

            // A counter that went backwards means an adapter came or went; skip that reading.
            if (sent >= _lastSent && received >= _lastReceived)
            {
                rates = ((sent - _lastSent) / seconds, (received - _lastReceived) / seconds);
            }
        }

        _lastSent = sent;
        _lastReceived = received;
        _lastNetworkTicks = now;
        _haveNetworkBaseline = true;
        return rates;
    }

    /// <summary>
    /// Counts real network cards only. VPN tunnels and virtual switches carry the same traffic
    /// a second time, which would double the figures.
    /// </summary>
    private static bool IsPhysicalAdapter(NetworkInterface adapter)
    {
        if (adapter.OperationalStatus != OperationalStatus.Up)
        {
            return false;
        }

        switch (adapter.NetworkInterfaceType)
        {
            case NetworkInterfaceType.Ethernet:
            case NetworkInterfaceType.GigabitEthernet:
            case NetworkInterfaceType.FastEthernetT:
            case NetworkInterfaceType.FastEthernetFx:
            case NetworkInterfaceType.Ethernet3Megabit:
            case NetworkInterfaceType.Wireless80211:
            case NetworkInterfaceType.Wwanpp:
            case NetworkInterfaceType.Wwanpp2:
                break;
            default:
                return false;
        }

        var description = adapter.Description;
        if (description.Contains("Virtual", StringComparison.OrdinalIgnoreCase)
            || description.Contains("Hyper-V", StringComparison.OrdinalIgnoreCase)
            || description.Contains("VPN", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return adapter.GetPhysicalAddress().GetAddressBytes().Length > 0;
    }

    /// <summary>The Performance Data Helper counters for GPU use, GPU memory and disk rates.</summary>
    private sealed class Pdh : IDisposable
    {
        private const uint PDH_FMT_DOUBLE = 0x00000200;
        private const uint PDH_FMT_NOCAP100 = 0x00008000;
        private const uint PDH_MORE_DATA = 0x800007D2;

        private IntPtr _query;
        private readonly IntPtr _gpuEngine;
        private readonly IntPtr _gpuMemory;
        private readonly IntPtr _diskRead;
        private readonly IntPtr _diskWrite;

        public Pdh(SystemStatsKinds kinds)
        {
            if (PdhOpenQueryW(null, IntPtr.Zero, out _query) != 0)
            {
                _query = IntPtr.Zero;
                return;
            }

            // English names work whatever the Windows display language is.
            if (kinds.HasFlag(SystemStatsKinds.Gpu))
            {
                _gpuEngine = Add(@"\GPU Engine(*)\Utilization Percentage");
            }

            if (kinds.HasFlag(SystemStatsKinds.Vram))
            {
                _gpuMemory = Add(@"\GPU Adapter Memory(*)\Dedicated Usage");
            }

            if (kinds.HasFlag(SystemStatsKinds.Disk))
            {
                _diskRead = Add(@"\PhysicalDisk(_Total)\Disk Read Bytes/sec");
                _diskWrite = Add(@"\PhysicalDisk(_Total)\Disk Write Bytes/sec");
            }

            // Rate counters need two samples; take the first now.
            PdhCollectQueryData(_query);
        }

        public SystemStats Read(SystemStats stats)
        {
            if (_query == IntPtr.Zero || PdhCollectQueryData(_query) != 0)
            {
                return stats;
            }

            if (_gpuEngine != IntPtr.Zero)
            {
                stats = stats with { GpuPercent = BusiestGpuEngine(ReadArray(_gpuEngine)) };
            }

            if (_gpuMemory != IntPtr.Zero && ReadArray(_gpuMemory) is { Count: > 0 } adapters)
            {
                // The discrete card, if there is one, is the adapter with the most memory in use.
                stats = stats with { VramBytes = adapters.Max(a => a.Value) };
            }

            if (_diskRead != IntPtr.Zero)
            {
                stats = stats with
                {
                    DiskReadBytesPerSecond = ReadSingle(_diskRead),
                    DiskWriteBytesPerSecond = ReadSingle(_diskWrite),
                };
            }

            return stats;
        }

        public void Dispose()
        {
            if (_query != IntPtr.Zero)
            {
                PdhCloseQuery(_query);
                _query = IntPtr.Zero;
            }
        }

        /// <summary>
        /// Task Manager's GPU figure: add up every process's use of each engine (3D, copy, video
        /// decode...) of each GPU, then take the busiest engine.
        /// </summary>
        /// <remarks>Instance names look like "pid_1234_luid_0x00000000_0x0000D1E2_phys_0_eng_3_engtype_3D".</remarks>
        private static double? BusiestGpuEngine(List<(string Name, double Value)>? instances)
        {
            if (instances is null)
            {
                return null;
            }

            var perEngine = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var (name, value) in instances)
            {
                var start = name.IndexOf("luid_", StringComparison.Ordinal);
                var engine = start >= 0 ? name[start..] : name;
                perEngine[engine] = perEngine.GetValueOrDefault(engine) + value;
            }

            return perEngine.Count == 0 ? 0 : Math.Min(100, perEngine.Values.Max());
        }

        private IntPtr Add(string path) =>
            PdhAddEnglishCounterW(_query, path, IntPtr.Zero, out var counter) == 0 ? counter : IntPtr.Zero;

        private static double? ReadSingle(IntPtr counter)
        {
            if (counter == IntPtr.Zero
                || PdhGetFormattedCounterValue(counter, PDH_FMT_DOUBLE | PDH_FMT_NOCAP100, out _, out var value) != 0
                || value.CStatus > 1)
            {
                return null;
            }

            return value.DoubleValue;
        }

        private static List<(string Name, double Value)>? ReadArray(IntPtr counter)
        {
            uint size = 0;
            var status = PdhGetFormattedCounterArrayW(counter, PDH_FMT_DOUBLE | PDH_FMT_NOCAP100, ref size, out _, IntPtr.Zero);
            if (status != PDH_MORE_DATA || size == 0)
            {
                return status == 0 ? new List<(string, double)>() : null;
            }

            var buffer = Marshal.AllocHGlobal((int)size);
            try
            {
                if (PdhGetFormattedCounterArrayW(counter, PDH_FMT_DOUBLE | PDH_FMT_NOCAP100, ref size, out var count, buffer) != 0)
                {
                    return null;
                }

                var items = new List<(string, double)>((int)count);
                var itemSize = Marshal.SizeOf<PDH_FMT_COUNTERVALUE_ITEM>();
                for (var i = 0; i < count; i++)
                {
                    var item = Marshal.PtrToStructure<PDH_FMT_COUNTERVALUE_ITEM>(buffer + (i * itemSize));

                    // 0 = valid data, 1 = new data; anything else is not a usable value.
                    if (item.Value.CStatus <= 1)
                    {
                        items.Add((Marshal.PtrToStringUni(item.Name) ?? string.Empty, item.Value.DoubleValue));
                    }
                }

                return items;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct PDH_FMT_COUNTERVALUE
        {
            [FieldOffset(0)] public uint CStatus;
            [FieldOffset(8)] public double DoubleValue;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PDH_FMT_COUNTERVALUE_ITEM
        {
            public IntPtr Name;
            public PDH_FMT_COUNTERVALUE Value;
        }

        [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
        private static extern uint PdhOpenQueryW(string? dataSource, IntPtr userData, out IntPtr query);

        [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
        private static extern uint PdhAddEnglishCounterW(IntPtr query, string path, IntPtr userData, out IntPtr counter);

        [DllImport("pdh.dll")]
        private static extern uint PdhCollectQueryData(IntPtr query);

        [DllImport("pdh.dll")]
        private static extern uint PdhGetFormattedCounterValue(IntPtr counter, uint format, out uint type, out PDH_FMT_COUNTERVALUE value);

        [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
        private static extern uint PdhGetFormattedCounterArrayW(IntPtr counter, uint format, ref uint bufferSize, out uint itemCount, IntPtr buffer);

        [DllImport("pdh.dll")]
        private static extern uint PdhCloseQuery(IntPtr query);
    }

    /// <summary>
    /// Reads GPU temperatures from the graphics driver's performance data, the same source as
    /// Task Manager's "GPU temperature". Needs a WDDM 2.5 or later driver; older ones report 0.
    /// </summary>
    private sealed class GpuTemperatureReader : IDisposable
    {
        private const int KMTQAITYPE_ADAPTERPERFDATA = 62;

        private readonly List<uint> _adapters = new();

        public GpuTemperatureReader()
        {
            var enumerate = new D3DKMT_ENUMADAPTERS2();
            if (D3DKMTEnumAdapters2(ref enumerate) != 0 || enumerate.NumAdapters == 0)
            {
                return;
            }

            var infoSize = Marshal.SizeOf<D3DKMT_ADAPTERINFO>();
            enumerate.pAdapters = Marshal.AllocHGlobal(infoSize * (int)enumerate.NumAdapters);
            try
            {
                if (D3DKMTEnumAdapters2(ref enumerate) != 0)
                {
                    return;
                }

                for (var i = 0; i < enumerate.NumAdapters; i++)
                {
                    var info = Marshal.PtrToStructure<D3DKMT_ADAPTERINFO>(enumerate.pAdapters + (i * infoSize));
                    _adapters.Add(info.hAdapter);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(enumerate.pAdapters);
            }
        }

        /// <summary>The hottest GPU in degrees Celsius, or null when no driver reports one.</summary>
        public double? Read()
        {
            double? hottest = null;
            var size = Marshal.SizeOf<D3DKMT_ADAPTER_PERFDATA>();
            var buffer = Marshal.AllocHGlobal(size);
            try
            {
                foreach (var adapter in _adapters)
                {
                    Marshal.StructureToPtr(new D3DKMT_ADAPTER_PERFDATA(), buffer, false);
                    var query = new D3DKMT_QUERYADAPTERINFO
                    {
                        hAdapter = adapter,
                        Type = KMTQAITYPE_ADAPTERPERFDATA,
                        pPrivateDriverData = buffer,
                        PrivateDriverDataSize = (uint)size,
                    };

                    if (D3DKMTQueryAdapterInfo(ref query) != 0)
                    {
                        continue;
                    }

                    // Reported in tenths of a degree; 0 means the driver does not provide it.
                    var data = Marshal.PtrToStructure<D3DKMT_ADAPTER_PERFDATA>(buffer);
                    if (data.Temperature > 0)
                    {
                        var celsius = data.Temperature / 10.0;
                        hottest = Math.Max(hottest ?? 0, celsius);
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }

            return hottest;
        }

        public void Dispose()
        {
            foreach (var adapter in _adapters)
            {
                var close = new D3DKMT_CLOSEADAPTER { hAdapter = adapter };
                D3DKMTCloseAdapter(ref close);
            }

            _adapters.Clear();
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct D3DKMT_ENUMADAPTERS2
        {
            public uint NumAdapters;
            public IntPtr pAdapters;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct D3DKMT_ADAPTERINFO
        {
            public uint hAdapter;
            public uint LuidLowPart;
            public int LuidHighPart;
            public uint NumOfSources;
            public int bPrecisePresentRegionsPreferred;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct D3DKMT_QUERYADAPTERINFO
        {
            public uint hAdapter;
            public int Type;
            public IntPtr pPrivateDriverData;
            public uint PrivateDriverDataSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct D3DKMT_ADAPTER_PERFDATA
        {
            public uint PhysicalAdapterIndex;
            public ulong MemoryFrequency;
            public ulong MaxMemoryFrequency;
            public ulong MaxMemoryFrequencyOC;
            public ulong MemoryBandwidth;
            public ulong PCIEBandwidth;
            public uint FanRPM;
            public uint Power;
            public uint Temperature;
            public byte PowerStateOverride;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct D3DKMT_CLOSEADAPTER
        {
            public uint hAdapter;
        }

        [DllImport("gdi32.dll")]
        private static extern int D3DKMTEnumAdapters2(ref D3DKMT_ENUMADAPTERS2 enumerate);

        [DllImport("gdi32.dll")]
        private static extern int D3DKMTQueryAdapterInfo(ref D3DKMT_QUERYADAPTERINFO query);

        [DllImport("gdi32.dll")]
        private static extern int D3DKMTCloseAdapter(ref D3DKMT_CLOSEADAPTER close);
    }

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
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX buffer);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out ulong idleTime, out ulong kernelTime, out ulong userTime);
}
