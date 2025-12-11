using DocUI.Demo.SystemMonitor.Model;

namespace DocUI.Demo.SystemMonitor.Collectors;

/// <summary>
/// 系统指标收集器
/// 
/// 注意：这是概念原型，使用模拟数据
/// 真实实现可以使用：
/// - Linux: /proc/stat, /proc/meminfo, df 命令
/// - Windows: PerformanceCounter, WMI
/// - Cross-platform: System.Diagnostics.Process
/// </summary>
public class MetricsCollector
{
    private readonly Random _random = new(42); // 固定种子保证可重现
    private SystemStatus? _cachedStatus;
    private DateTime _lastCollect = DateTime.MinValue;
    private readonly TimeSpan _cacheExpiry = TimeSpan.FromSeconds(5);

    /// <summary>
    /// 收集系统状态
    /// </summary>
    public SystemStatus Collect(bool forceRefresh = false)
    {
        // 简单缓存避免频繁生成
        if (!forceRefresh && _cachedStatus != null &&
            DateTime.UtcNow - _lastCollect < _cacheExpiry)
        {
            return _cachedStatus;
        }

        _cachedStatus = GenerateMockStatus();
        _lastCollect = DateTime.UtcNow;
        return _cachedStatus;
    }

    /// <summary>
    /// 收集 CPU 指标
    /// </summary>
    public CpuMetrics CollectCpu() => Collect().Cpu;

    /// <summary>
    /// 收集内存指标
    /// </summary>
    public MemoryMetrics CollectMemory() => Collect().Memory;

    /// <summary>
    /// 收集磁盘指标
    /// </summary>
    public IReadOnlyList<DiskMetrics> CollectDisks() => Collect().Disks;

    /// <summary>
    /// 收集进程列表
    /// </summary>
    public IReadOnlyList<ProcessInfo> CollectProcesses(int top = 10)
        => Collect().Processes.Take(top).ToList();

    /// <summary>
    /// 生成模拟数据
    /// </summary>
    private SystemStatus GenerateMockStatus()
    {
        return new SystemStatus
        {
            Timestamp = DateTime.UtcNow,
            Cpu = GenerateCpuMetrics(),
            Memory = GenerateMemoryMetrics(),
            Disks = GenerateDiskMetrics(),
            Processes = GenerateProcessList()
        };
    }

    private CpuMetrics GenerateCpuMetrics()
    {
        // 模拟 CPU 使用率在 15-45% 之间浮动
        var usage = 15 + _random.NextDouble() * 30;
        return new CpuMetrics
        {
            UsagePercent = Math.Round(usage, 1),
            CoreCount = 8,
            LoadAverage = (
                Math.Round(0.8 + _random.NextDouble() * 0.8, 2),
                Math.Round(0.6 + _random.NextDouble() * 0.6, 2),
                Math.Round(0.5 + _random.NextDouble() * 0.5, 2)
            )
        };
    }

    private MemoryMetrics GenerateMemoryMetrics()
    {
        const double totalGb = 16.0;
        // 模拟内存使用在 3-6 GB 之间
        var usedGb = 3 + _random.NextDouble() * 3;
        return new MemoryMetrics
        {
            UsedGb = Math.Round(usedGb, 1),
            TotalGb = totalGb,
            SwapUsedGb = Math.Round(_random.NextDouble() * 0.5, 1),
            SwapTotalGb = 2.0
        };
    }

    private IReadOnlyList<DiskMetrics> GenerateDiskMetrics()
    {
        return new List<DiskMetrics>
        {
            new DiskMetrics
            {
                MountPoint = "/",
                UsedGb = 170 + _random.NextDouble() * 10,
                TotalGb = 200
            },
            new DiskMetrics
            {
                MountPoint = "/home",
                UsedGb = 85 + _random.NextDouble() * 10,
                TotalGb = 200
            }
        };
    }

    private IReadOnlyList<ProcessInfo> GenerateProcessList()
    {
        // 模拟进程列表
        var processes = new List<ProcessInfo>
        {
            new ProcessInfo { Pid = 1234, Name = "chrome", CpuPercent = 12.3, MemoryPercent = 5.2 },
            new ProcessInfo { Pid = 2345, Name = "code", CpuPercent = 8.1, MemoryPercent = 4.8 },
            new ProcessInfo { Pid = 3456, Name = "dotnet", CpuPercent = 5.4, MemoryPercent = 3.2 },
            new ProcessInfo { Pid = 4567, Name = "firefox", CpuPercent = 4.2, MemoryPercent = 6.1 },
            new ProcessInfo { Pid = 5678, Name = "slack", CpuPercent = 3.8, MemoryPercent = 2.9 },
            new ProcessInfo { Pid = 6789, Name = "spotify", CpuPercent = 2.5, MemoryPercent = 1.8 },
            new ProcessInfo { Pid = 7890, Name = "terminal", CpuPercent = 1.9, MemoryPercent = 0.9 },
            new ProcessInfo { Pid = 8901, Name = "systemd", CpuPercent = 1.2, MemoryPercent = 0.5 },
            new ProcessInfo { Pid = 9012, Name = "dbus-daemon", CpuPercent = 0.8, MemoryPercent = 0.3 },
            new ProcessInfo { Pid = 9123, Name = "pulseaudio", CpuPercent = 0.5, MemoryPercent = 0.4 }
        };

        // 稍微随机化 CPU 使用率
        return processes.Select(p => p with
        {
            CpuPercent = Math.Round(p.CpuPercent + (_random.NextDouble() - 0.5) * 2, 1)
        }).OrderByDescending(p => p.CpuPercent).ToList();
    }
}
