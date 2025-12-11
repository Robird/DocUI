namespace DocUI.Demo.MemoryNotebook.Model;

/// <summary>
/// 内容节点 - MemoryNotebook 中的一条记录
/// 
/// 设计理念：
/// - 每个节点都有三个 LOD 级别的表示
/// - Full 是原始完整内容
/// - Summary 和 Gist 可以由 LLM 生成或手动提供
/// - currentLod 控制当前渲染时使用哪个级别
/// </summary>
public record ContentNode
{
    /// <summary>
    /// 唯一标识符，用于引用和操作
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// 显示标题
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// 内容来源类型
    /// </summary>
    public required ContentSource Source { get; init; }

    /// <summary>
    /// 多级别内容
    /// </summary>
    public required LodContent Content { get; init; }

    /// <summary>
    /// 当前 LOD 级别
    /// </summary>
    public LodLevel CurrentLod { get; set; } = LodLevel.Summary;

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// 标签（用于筛选）
    /// </summary>
    public IReadOnlyList<string> Tags { get; init; } = [];
}

/// <summary>
/// 内容来源类型
/// </summary>
public enum ContentSource
{
    /// <summary>
    /// 静态外源内容（如文件、网页）- Summary 由 LLM 折叠时生成
    /// </summary>
    Static,

    /// <summary>
    /// 动态生成内容（如系统监控）- App 自己提供多 LOD 视图
    /// </summary>
    Dynamic,

    /// <summary>
    /// 用户手动输入
    /// </summary>
    UserInput
}

/// <summary>
/// 多级别内容容器
/// </summary>
public record LodContent
{
    /// <summary>
    /// 完整内容（必须提供）
    /// </summary>
    public required string Full { get; init; }

    /// <summary>
    /// 摘要（可选，未提供时降级到 Full）
    /// </summary>
    public string? Summary { get; init; }

    /// <summary>
    /// 最小印象（可选，未提供时降级到 Summary 或 Full）
    /// </summary>
    public string? Gist { get; init; }

    /// <summary>
    /// 获取指定级别的内容，自动降级
    /// </summary>
    public string GetAtLevel(LodLevel level) => level switch
    {
        LodLevel.Gist => Gist ?? Summary ?? Full,
        LodLevel.Summary => Summary ?? Full,
        LodLevel.Full => Full,
        _ => Full
    };

    /// <summary>
    /// 检查指定级别是否有专门的内容（非降级）
    /// </summary>
    public bool HasExplicitContent(LodLevel level) => level switch
    {
        LodLevel.Gist => Gist != null,
        LodLevel.Summary => Summary != null,
        LodLevel.Full => true,
        _ => true
    };
}
