// SystemMonitor - DocUI 概念原型
// 
// 展示动态内容的 LOD (Level of Detail) 控制
// 与 MemoryNotebook 的静态内容不同，这里展示实时变化的系统信息
//
// LOD 级别:
//   [GIST]    - 一行关键指标
//   [SUMMARY] - 摘要表格
//   [FULL]    - 完整详情
//
// 使用方式 (通过 pmux 命令):
//
//   pmux monitor view [--lod gist|summary|full]  # 查看系统状态
//   pmux monitor cpu                              # 只看 CPU
//   pmux monitor memory                           # 只看内存
//   pmux monitor disk                             # 只看磁盘
//   pmux monitor processes [--top N]              # 查看进程
//   pmux monitor set-lod <level>                  # 设置默认 LOD

using System.CommandLine;
using DocUI.Demo.SystemMonitor.Collectors;
using DocUI.Demo.SystemMonitor.Model;
using DocUI.Demo.SystemMonitor.Rendering;
using PipeMux.Sdk;

// === 创建服务实例 ===
var collector = new MetricsCollector();
var renderer = new MonitorRenderer();
var defaultLod = LodLevel.Summary;

// === 创建 PipeMux App ===
var app = new PipeMuxApp("monitor");

// === 定义命令 ===

// view - 查看系统状态
var viewLodOption = new Option<string?>("--lod", "-l") { Description = "LOD level (gist/summary/full)" };
var viewCommand = new Command("view", "View system status") { viewLodOption };
viewCommand.SetAction(parseResult =>
{
    var (level, error) = ParseLodOption(parseResult.GetValue(viewLodOption), defaultLod);
    if (error != null)
    {
        parseResult.Configuration.Error.WriteLine(error);
        return 1;
    }

    var status = collector.Collect();
    var output = renderer.Render(status, level);
    parseResult.Configuration.Output.WriteLine(output);
    return 0;
});

// cpu - 查看 CPU 状态
var cpuLodOption = new Option<string?>("--lod", "-l") { Description = "LOD level (gist/summary/full)" };
var cpuCommand = new Command("cpu", "View CPU metrics") { cpuLodOption };
cpuCommand.SetAction(parseResult =>
{
    var (level, error) = ParseLodOption(parseResult.GetValue(cpuLodOption), defaultLod);
    if (error != null)
    {
        parseResult.Configuration.Error.WriteLine(error);
        return 1;
    }

    var cpu = collector.CollectCpu();
    var output = renderer.RenderCpu(cpu, level);
    parseResult.Configuration.Output.WriteLine(output);
    return 0;
});

// memory - 查看内存状态
var memLodOption = new Option<string?>("--lod", "-l") { Description = "LOD level (gist/summary/full)" };
var memCommand = new Command("memory", "View memory metrics") { memLodOption };
memCommand.SetAction(parseResult =>
{
    var (level, error) = ParseLodOption(parseResult.GetValue(memLodOption), defaultLod);
    if (error != null)
    {
        parseResult.Configuration.Error.WriteLine(error);
        return 1;
    }

    var mem = collector.CollectMemory();
    var output = renderer.RenderMemory(mem, level);
    parseResult.Configuration.Output.WriteLine(output);
    return 0;
});

// disk - 查看磁盘状态
var diskLodOption = new Option<string?>("--lod", "-l") { Description = "LOD level (gist/summary/full)" };
var diskCommand = new Command("disk", "View disk metrics") { diskLodOption };
diskCommand.SetAction(parseResult =>
{
    var (level, error) = ParseLodOption(parseResult.GetValue(diskLodOption), defaultLod);
    if (error != null)
    {
        parseResult.Configuration.Error.WriteLine(error);
        return 1;
    }

    var disks = collector.CollectDisks();
    var output = renderer.RenderDisk(disks, level);
    parseResult.Configuration.Output.WriteLine(output);
    return 0;
});

// processes - 查看进程
var procTopOption = new Option<int>("--top", "-n") { Description = "Number of processes to show" };
var procLodOption = new Option<string?>("--lod", "-l") { Description = "LOD level (gist/summary/full)" };
var procCommand = new Command("processes", "View top processes") { procTopOption, procLodOption };
procCommand.SetAction(parseResult =>
{
    var top = parseResult.GetValue(procTopOption);
    if (top <= 0) top = 10;
    var (level, error) = ParseLodOption(parseResult.GetValue(procLodOption), defaultLod);
    if (error != null)
    {
        parseResult.Configuration.Error.WriteLine(error);
        return 1;
    }

    var processes = collector.CollectProcesses(top);
    var output = renderer.RenderProcesses(processes, top, level);
    parseResult.Configuration.Output.WriteLine(output);
    return 0;
});

// set-lod - 设置默认 LOD
var setLodLevelArg = new Argument<string>("level") { Description = "Default LOD level (gist/summary/full)" };
var setLodCommand = new Command("set-lod", "Set default LOD level") { setLodLevelArg };
setLodCommand.SetAction(parseResult =>
{
    var levelStr = parseResult.GetValue(setLodLevelArg);
    var (level, error) = ParseLodOption(levelStr, LodLevel.Summary);
    if (error != null)
    {
        parseResult.Configuration.Error.WriteLine(error);
        return 1;
    }

    defaultLod = level;
    parseResult.Configuration.Output.WriteLine($"Default LOD set to: {level}");
    return 0;
});

// === 根命令 ===
var rootCommand = new RootCommand("SystemMonitor - Real-time system metrics with LOD control")
{
    viewCommand,
    cpuCommand,
    memCommand,
    diskCommand,
    procCommand,
    setLodCommand
};

// === 运行 ===
await app.RunAsync(rootCommand);

// === 辅助函数 ===

/// <summary>
/// 解析 LOD 参数
/// </summary>
static (LodLevel Level, string? Error) ParseLodOption(string? value, LodLevel defaultLevel)
{
    if (string.IsNullOrEmpty(value))
        return (defaultLevel, null);

    return value.ToLower() switch
    {
        "gist" => (LodLevel.Gist, null),
        "summary" => (LodLevel.Summary, null),
        "full" => (LodLevel.Full, null),
        _ => (defaultLevel, $"Invalid LOD level: '{value}'. Must be one of: gist, summary, full")
    };
}
