namespace DocUI.Demo.SystemMonitor.Model;

/// <summary>
/// CPU 指标
/// </summary>
public record CpuMetrics
{
    /// <summary>
    /// CPU 使用率百分比 (0-100)
    /// </summary>
    public required double UsagePercent { get; init; }

    /// <summary>
    /// CPU 核心数
    /// </summary>
    public required int CoreCount { get; init; }

    /// <summary>
    /// 负载均值 (1分钟, 5分钟, 15分钟)
    /// </summary>
    public required (double OneMin, double FiveMin, double FifteenMin) LoadAverage { get; init; }

    /// <summary>
    /// 健康状态
    /// </summary>
    public HealthStatus Status => UsagePercent switch
    {
        >= 90 => HealthStatus.Critical,
        >= 70 => HealthStatus.Warning,
        _ => HealthStatus.Ok
    };
}

/// <summary>
/// 内存指标
/// </summary>
public record MemoryMetrics
{
    /// <summary>
    /// 已用内存 (GB)
    /// </summary>
    public required double UsedGb { get; init; }

    /// <summary>
    /// 总内存 (GB)
    /// </summary>
    public required double TotalGb { get; init; }

    /// <summary>
    /// 可用内存 (GB)
    /// </summary>
    public double AvailableGb => TotalGb - UsedGb;

    /// <summary>
    /// 使用率百分比
    /// </summary>
    public double UsagePercent => TotalGb > 0 ? (UsedGb / TotalGb) * 100 : 0;

    /// <summary>
    /// 交换分区使用量 (GB)
    /// </summary>
    public required double SwapUsedGb { get; init; }

    /// <summary>
    /// 交换分区总量 (GB)
    /// </summary>
    public required double SwapTotalGb { get; init; }

    /// <summary>
    /// 健康状态
    /// </summary>
    public HealthStatus Status => UsagePercent switch
    {
        >= 90 => HealthStatus.Critical,
        >= 80 => HealthStatus.Warning,
        _ => HealthStatus.Ok
    };
}

/// <summary>
/// 磁盘指标
/// </summary>
public record DiskMetrics
{
    /// <summary>
    /// 挂载点
    /// </summary>
    public required string MountPoint { get; init; }

    /// <summary>
    /// 已用空间 (GB)
    /// </summary>
    public required double UsedGb { get; init; }

    /// <summary>
    /// 总空间 (GB)
    /// </summary>
    public required double TotalGb { get; init; }

    /// <summary>
    /// 使用率百分比
    /// </summary>
    public double UsagePercent => TotalGb > 0 ? (UsedGb / TotalGb) * 100 : 0;

    /// <summary>
    /// 健康状态
    /// </summary>
    public HealthStatus Status => UsagePercent switch
    {
        >= 95 => HealthStatus.Critical,
        >= 85 => HealthStatus.Warning,
        _ => HealthStatus.Ok
    };
}

/// <summary>
/// 进程信息
/// </summary>
public record ProcessInfo
{
    /// <summary>
    /// 进程 ID
    /// </summary>
    public required int Pid { get; init; }

    /// <summary>
    /// 进程名称
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// CPU 使用率百分比
    /// </summary>
    public required double CpuPercent { get; init; }

    /// <summary>
    /// 内存使用率百分比
    /// </summary>
    public required double MemoryPercent { get; init; }
}
