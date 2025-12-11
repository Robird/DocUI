using System.Text;
using DocUI.Demo.SystemMonitor.Model;

namespace DocUI.Demo.SystemMonitor.Rendering;

/// <summary>
/// 系统监控渲染器
/// 
/// 核心设计：LOD 控制的是同一数据的不同呈现方式
/// - [GIST]    一行关键指标
/// - [SUMMARY] 摘要表格
/// - [FULL]    完整详情
/// </summary>
public class MonitorRenderer
{
    /// <summary>
    /// 渲染完整系统状态
    /// </summary>
    public string Render(SystemStatus status, LodLevel level)
    {
        return level switch
        {
            LodLevel.Gist => RenderGist(status),
            LodLevel.Summary => RenderSummary(status),
            LodLevel.Full => RenderFull(status),
            _ => RenderSummary(status)
        };
    }

    /// <summary>
    /// 渲染 CPU 指标
    /// </summary>
    public string RenderCpu(CpuMetrics cpu, LodLevel level)
    {
        return level switch
        {
            LodLevel.Gist => $"CPU {cpu.UsagePercent:F0}% | {cpu.CoreCount} cores",
            LodLevel.Summary => RenderCpuSummary(cpu),
            LodLevel.Full => RenderCpuFull(cpu),
            _ => RenderCpuSummary(cpu)
        };
    }

    /// <summary>
    /// 渲染内存指标
    /// </summary>
    public string RenderMemory(MemoryMetrics mem, LodLevel level)
    {
        return level switch
        {
            LodLevel.Gist => $"Mem {mem.UsedGb:F1}/{mem.TotalGb:F0}GB ({mem.UsagePercent:F0}%)",
            LodLevel.Summary => RenderMemorySummary(mem),
            LodLevel.Full => RenderMemoryFull(mem),
            _ => RenderMemorySummary(mem)
        };
    }

    /// <summary>
    /// 渲染磁盘指标
    /// </summary>
    public string RenderDisk(IReadOnlyList<DiskMetrics> disks, LodLevel level)
    {
        return level switch
        {
            LodLevel.Gist => RenderDiskGist(disks),
            LodLevel.Summary => RenderDiskSummary(disks),
            LodLevel.Full => RenderDiskFull(disks),
            _ => RenderDiskSummary(disks)
        };
    }

    /// <summary>
    /// 渲染进程列表
    /// </summary>
    public string RenderProcesses(IReadOnlyList<ProcessInfo> processes, int top, LodLevel level)
    {
        var topProcesses = processes.Take(top).ToList();
        return level switch
        {
            LodLevel.Gist => RenderProcessesGist(topProcesses),
            LodLevel.Summary => RenderProcessesSummary(topProcesses),
            LodLevel.Full => RenderProcessesFull(topProcesses),
            _ => RenderProcessesSummary(topProcesses)
        };
    }

    // === GIST Level ===

    private string RenderGist(SystemStatus status)
    {
        var statusIcon = status.OverallStatus switch
        {
            HealthStatus.Ok => "✓",
            HealthStatus.Warning => "⚠",
            HealthStatus.Critical => "✗",
            _ => "?"
        };

        var statusText = status.OverallStatus switch
        {
            HealthStatus.Ok => "OK",
            HealthStatus.Warning => "WARN",
            HealthStatus.Critical => "CRIT",
            _ => "?"
        };

        var mainDisk = status.Disks.FirstOrDefault();
        var diskInfo = mainDisk != null ? $"Disk {mainDisk.UsagePercent:F0}%" : "";

        return $"System {statusIcon} {statusText} | CPU {status.Cpu.UsagePercent:F0}% | Mem {status.Memory.UsedGb:F1}/{status.Memory.TotalGb:F0}GB | {diskInfo}";
    }

    // === SUMMARY Level ===

    private string RenderSummary(SystemStatus status)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## System Monitor");
        sb.AppendLine();
        sb.AppendLine("| Resource | Status | Usage |");
        sb.AppendLine("|----------|--------|-------|");
        sb.AppendLine($"| CPU      | {FormatStatus(status.Cpu.Status)} | {status.Cpu.UsagePercent:F0}% |");
        sb.AppendLine($"| Memory   | {FormatStatus(status.Memory.Status)} | {status.Memory.UsedGb:F1}/{status.Memory.TotalGb:F0} GB |");

        foreach (var disk in status.Disks)
        {
            sb.AppendLine($"| Disk {disk.MountPoint} | {FormatStatus(disk.Status)} | {disk.UsagePercent:F0}% |");
        }

        sb.AppendLine();

        // Top 3 Processes
        var topProcesses = status.Processes.Take(3).ToList();
        if (topProcesses.Count > 0)
        {
            var processStr = string.Join(", ",
                topProcesses.Select(p => $"{p.Name} ({p.CpuPercent:F0}%)"));
            sb.AppendLine($"**Top 3 Processes:** {processStr}");
        }

        return sb.ToString().TrimEnd();
    }

    private string RenderCpuSummary(CpuMetrics cpu)
    {
        var sb = new StringBuilder();
        sb.AppendLine("### CPU");
        sb.AppendLine($"- **Status:** {FormatStatus(cpu.Status)}");
        sb.AppendLine($"- **Usage:** {cpu.UsagePercent:F1}%");
        sb.AppendLine($"- **Cores:** {cpu.CoreCount}");
        return sb.ToString().TrimEnd();
    }

    private string RenderMemorySummary(MemoryMetrics mem)
    {
        var sb = new StringBuilder();
        sb.AppendLine("### Memory");
        sb.AppendLine($"- **Status:** {FormatStatus(mem.Status)}");
        sb.AppendLine($"- **Used:** {mem.UsedGb:F1} / {mem.TotalGb:F0} GB ({mem.UsagePercent:F0}%)");
        sb.AppendLine($"- **Available:** {mem.AvailableGb:F1} GB");
        return sb.ToString().TrimEnd();
    }

    private string RenderDiskSummary(IReadOnlyList<DiskMetrics> disks)
    {
        var sb = new StringBuilder();
        sb.AppendLine("### Disk");
        foreach (var disk in disks)
        {
            sb.AppendLine($"- **{disk.MountPoint}:** {disk.UsagePercent:F0}% ({disk.UsedGb:F0}/{disk.TotalGb:F0} GB) - {FormatStatus(disk.Status)}");
        }
        return sb.ToString().TrimEnd();
    }

    private string RenderProcessesSummary(IReadOnlyList<ProcessInfo> processes)
    {
        var sb = new StringBuilder();
        sb.AppendLine("### Top Processes");
        foreach (var p in processes)
        {
            sb.AppendLine($"- {p.Name} (PID {p.Pid}): CPU {p.CpuPercent:F1}%, Mem {p.MemoryPercent:F1}%");
        }
        return sb.ToString().TrimEnd();
    }

    // === FULL Level ===

    private string RenderFull(SystemStatus status)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## System Monitor (Full View)");
        sb.AppendLine();
        sb.AppendLine($"> Collected at: {status.Timestamp:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"> Overall Status: {FormatStatus(status.OverallStatus)}");
        sb.AppendLine();
        sb.AppendLine(RenderCpuFull(status.Cpu));
        sb.AppendLine();
        sb.AppendLine(RenderMemoryFull(status.Memory));
        sb.AppendLine();
        sb.AppendLine(RenderDiskFull(status.Disks));
        sb.AppendLine();
        sb.AppendLine(RenderProcessesFull(status.Processes.Take(10).ToList()));

        return sb.ToString().TrimEnd();
    }

    private string RenderCpuFull(CpuMetrics cpu)
    {
        var sb = new StringBuilder();
        sb.AppendLine("### CPU");
        sb.AppendLine($"- **Usage:** {cpu.UsagePercent:F1}%");
        sb.AppendLine($"- **Cores:** {cpu.CoreCount}");
        sb.AppendLine($"- **Load Average:** {cpu.LoadAverage.OneMin:F2}, {cpu.LoadAverage.FiveMin:F2}, {cpu.LoadAverage.FifteenMin:F2}");
        sb.AppendLine($"- **Status:** {FormatStatus(cpu.Status)}");
        return sb.ToString().TrimEnd();
    }

    private string RenderMemoryFull(MemoryMetrics mem)
    {
        var sb = new StringBuilder();
        sb.AppendLine("### Memory");
        sb.AppendLine($"- **Used:** {mem.UsedGb:F1} GB");
        sb.AppendLine($"- **Total:** {mem.TotalGb:F0} GB");
        sb.AppendLine($"- **Available:** {mem.AvailableGb:F1} GB");
        sb.AppendLine($"- **Usage:** {mem.UsagePercent:F1}%");
        sb.AppendLine($"- **Swap:** {mem.SwapUsedGb:F1}/{mem.SwapTotalGb:F0} GB");
        sb.AppendLine($"- **Status:** {FormatStatus(mem.Status)}");
        return sb.ToString().TrimEnd();
    }

    private string RenderDiskFull(IReadOnlyList<DiskMetrics> disks)
    {
        var sb = new StringBuilder();
        sb.AppendLine("### Disk");
        foreach (var disk in disks)
        {
            sb.AppendLine($"- **{disk.MountPoint}:** {disk.UsagePercent:F0}% ({disk.UsedGb:F0}/{disk.TotalGb:F0} GB)");
        }
        return sb.ToString().TrimEnd();
    }

    private string RenderProcessesFull(IReadOnlyList<ProcessInfo> processes)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"### Top {processes.Count} Processes");
        sb.AppendLine();
        sb.AppendLine("| PID | Name | CPU% | Mem% |");
        sb.AppendLine("|-----|------|------|------|");
        foreach (var p in processes)
        {
            sb.AppendLine($"| {p.Pid} | {p.Name} | {p.CpuPercent:F1}% | {p.MemoryPercent:F1}% |");
        }
        return sb.ToString().TrimEnd();
    }

    // === Gist helpers ===

    private string RenderDiskGist(IReadOnlyList<DiskMetrics> disks)
    {
        var parts = disks.Select(d => $"{d.MountPoint} {d.UsagePercent:F0}%");
        return $"Disk: {string.Join(", ", parts)}";
    }

    private string RenderProcessesGist(IReadOnlyList<ProcessInfo> processes)
    {
        var parts = processes.Take(3).Select(p => $"{p.Name} ({p.CpuPercent:F0}%)");
        return $"Top: {string.Join(", ", parts)}";
    }

    // === Helpers ===

    private static string FormatStatus(HealthStatus status)
    {
        return status switch
        {
            HealthStatus.Ok => "OK",
            HealthStatus.Warning => "WARN",
            HealthStatus.Critical => "CRIT",
            _ => "?"
        };
    }
}
