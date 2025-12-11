namespace DocUI.Demo.SystemMonitor.Model;

/// <summary>
/// 系统状态数据模型
/// 包含所有资源指标的聚合
/// </summary>
public record SystemStatus
{
    /// <summary>
    /// 采集时间
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// CPU 指标
    /// </summary>
    public required CpuMetrics Cpu { get; init; }

    /// <summary>
    /// 内存指标
    /// </summary>
    public required MemoryMetrics Memory { get; init; }

    /// <summary>
    /// 磁盘指标
    /// </summary>
    public required IReadOnlyList<DiskMetrics> Disks { get; init; }

    /// <summary>
    /// 进程列表（按 CPU 使用率排序）
    /// </summary>
    public required IReadOnlyList<ProcessInfo> Processes { get; init; }

    /// <summary>
    /// 获取整体状态
    /// </summary>
    public HealthStatus OverallStatus
    {
        get
        {
            if (Cpu.Status == HealthStatus.Critical ||
                Memory.Status == HealthStatus.Critical ||
                Disks.Any(d => d.Status == HealthStatus.Critical))
                return HealthStatus.Critical;

            if (Cpu.Status == HealthStatus.Warning ||
                Memory.Status == HealthStatus.Warning ||
                Disks.Any(d => d.Status == HealthStatus.Warning))
                return HealthStatus.Warning;

            return HealthStatus.Ok;
        }
    }
}

/// <summary>
/// 健康状态
/// </summary>
public enum HealthStatus
{
    Ok,
    Warning,
    Critical
}
