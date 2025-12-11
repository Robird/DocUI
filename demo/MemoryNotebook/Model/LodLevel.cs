namespace DocUI.Demo.MemoryNotebook.Model;

/// <summary>
/// Level of Detail - 信息详略级别
/// </summary>
public enum LodLevel
{
    /// <summary>
    /// 最小印象 - 一句话标识，用于"知道有这么个东西但现在不关心"
    /// </summary>
    Gist = 0,

    /// <summary>
    /// 摘要级别 - 保留关键信息，日常工作状态
    /// </summary>
    Summary = 1,

    /// <summary>
    /// 完整内容 - 全部细节
    /// </summary>
    Full = 2
}
