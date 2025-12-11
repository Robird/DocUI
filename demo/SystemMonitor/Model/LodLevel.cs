namespace DocUI.Demo.SystemMonitor.Model;

/// <summary>
/// Level of Detail - 信息详略级别
/// </summary>
public enum LodLevel
{
    /// <summary>
    /// 最小印象 - 一行关键指标
    /// </summary>
    Gist = 0,

    /// <summary>
    /// 摘要级别 - 表格形式，日常工作状态
    /// </summary>
    Summary = 1,

    /// <summary>
    /// 完整内容 - 全部细节
    /// </summary>
    Full = 2
}
