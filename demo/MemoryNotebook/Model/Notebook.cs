using System.Text;

namespace DocUI.Demo.MemoryNotebook.Model;

/// <summary>
/// MemoryNotebook 的数据存储
/// 
/// 概念：作为 LLM Agent 的外部知识库
/// - 存储各种信息片段（文件摘要、网页内容、会话笔记等）
/// - 支持 LOD 控制，Agent 可以主动管理信息焦点
/// - 渲染为 Markdown 输出
/// </summary>
public class Notebook
{
    private readonly Dictionary<string, ContentNode> _nodes = new();
    private readonly List<string> _orderedIds = []; // 保持插入顺序

    /// <summary>
    /// 添加节点
    /// </summary>
    public void Add(ContentNode node)
    {
        if (_nodes.ContainsKey(node.Id))
        {
            _nodes[node.Id] = node;
        }
        else
        {
            _nodes[node.Id] = node;
            _orderedIds.Add(node.Id);
        }
    }

    /// <summary>
    /// 获取节点
    /// </summary>
    public ContentNode? Get(string id) =>
        _nodes.TryGetValue(id, out var node) ? node : null;

    /// <summary>
    /// 获取所有节点（按插入顺序）
    /// </summary>
    public IEnumerable<ContentNode> GetAll() =>
        _orderedIds.Select(id => _nodes[id]);

    /// <summary>
    /// 按标签筛选
    /// </summary>
    public IEnumerable<ContentNode> GetByTag(string tag) =>
        GetAll().Where(n => n.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase));

    /// <summary>
    /// 设置节点的 LOD 级别
    /// </summary>
    public bool SetLod(string id, LodLevel level)
    {
        if (!_nodes.TryGetValue(id, out var node))
            return false;

        // 由于 record 是不可变的，需要创建新实例
        _nodes[id] = node with { CurrentLod = level };
        return true;
    }

    /// <summary>
    /// 折叠节点（设置为 Gist 级别）
    /// </summary>
    public bool Fold(string id) => SetLod(id, LodLevel.Gist);

    /// <summary>
    /// 展开节点（设置为 Full 级别）
    /// </summary>
    public bool Unfold(string id) => SetLod(id, LodLevel.Full);

    /// <summary>
    /// 折叠所有节点到指定级别
    /// </summary>
    public void FoldAll(LodLevel level = LodLevel.Summary)
    {
        foreach (var id in _orderedIds)
        {
            SetLod(id, level);
        }
    }

    /// <summary>
    /// 节点数量
    /// </summary>
    public int Count => _nodes.Count;

    /// <summary>
    /// 删除节点
    /// </summary>
    public bool Remove(string id)
    {
        if (_nodes.Remove(id))
        {
            _orderedIds.Remove(id);
            return true;
        }
        return false;
    }

    /// <summary>
    /// 渲染为 Markdown
    /// </summary>
    public string RenderMarkdown(LodLevel? overrideLod = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# 📓 Memory Notebook");
        sb.AppendLine();

        if (_nodes.Count == 0)
        {
            sb.AppendLine("*No entries yet.*");
            return sb.ToString();
        }

        // 统计信息 - 如果有 overrideLod，统计应反映覆盖后的状态
        var stats = GetStats(overrideLod);
        sb.AppendLine($"> {_nodes.Count} entries | ");
        sb.AppendLine($"> LOD distribution: {stats.GistCount} gist, {stats.SummaryCount} summary, {stats.FullCount} full");
        sb.AppendLine();

        // 渲染每个节点
        foreach (var node in GetAll())
        {
            var effectiveLod = overrideLod ?? node.CurrentLod;
            sb.AppendLine(RenderNode(node, effectiveLod));
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// 渲染单个节点
    /// </summary>
    public string RenderNode(ContentNode node, LodLevel? overrideLod = null)
    {
        var level = overrideLod ?? node.CurrentLod;
        var sb = new StringBuilder();

        // LOD 指示器 - 使用文字标签，对 LLM 更友好且 token 效率更高
        var lodIndicator = level switch
        {
            LodLevel.Gist => "[GIST]",
            LodLevel.Summary => "[SUMMARY]",
            LodLevel.Full => "[FULL]",
            _ => "[?]"
        };

        // 标签
        var tagsStr = node.Tags.Count > 0
            ? $" `{string.Join("` `", node.Tags)}`"
            : "";

        // 根据 LOD 级别渲染
        switch (level)
        {
            case LodLevel.Gist:
                // 最小形式：一行
                var gistContent = node.Content.GetAtLevel(LodLevel.Gist);
                sb.AppendLine($"{lodIndicator} **[{node.Id}]** {node.Title} — _{TruncateToOneLine(gistContent)}_");
                break;

            case LodLevel.Summary:
                // 摘要形式：标题 + 摘要内容
                sb.AppendLine($"{lodIndicator} **[{node.Id}]** {node.Title}{tagsStr}");
                sb.AppendLine();
                var summaryContent = node.Content.GetAtLevel(LodLevel.Summary);
                foreach (var line in summaryContent.Split('\n'))
                {
                    sb.AppendLine($"> {line}");
                }
                break;

            case LodLevel.Full:
                // 完整形式：标题 + 完整内容
                sb.AppendLine($"{lodIndicator} **[{node.Id}]** {node.Title}{tagsStr}");
                sb.AppendLine();
                sb.AppendLine(node.Content.Full);
                break;
        }

        return sb.ToString().TrimEnd();
    }

    private static string TruncateToOneLine(string text, int maxLength = 60)
    {
        var firstLine = text.Split('\n')[0].Trim();
        if (firstLine.Length <= maxLength)
            return firstLine;
        return firstLine[..(maxLength - 3)] + "...";
    }

    /// <summary>
    /// 获取统计信息
    /// </summary>
    /// <param name="overrideLod">如果指定，所有节点都按此级别统计</param>
    public NotebookStats GetStats(LodLevel? overrideLod = null)
    {
        var nodes = GetAll().ToList();
        
        // 如果有 overrideLod，所有节点都算作该级别
        int gistCount, summaryCount, fullCount;
        if (overrideLod.HasValue)
        {
            gistCount = overrideLod == LodLevel.Gist ? nodes.Count : 0;
            summaryCount = overrideLod == LodLevel.Summary ? nodes.Count : 0;
            fullCount = overrideLod == LodLevel.Full ? nodes.Count : 0;
        }
        else
        {
            gistCount = nodes.Count(n => n.CurrentLod == LodLevel.Gist);
            summaryCount = nodes.Count(n => n.CurrentLod == LodLevel.Summary);
            fullCount = nodes.Count(n => n.CurrentLod == LodLevel.Full);
        }
        
        return new NotebookStats
        {
            TotalCount = nodes.Count,
            GistCount = gistCount,
            SummaryCount = summaryCount,
            FullCount = fullCount,
            // 统一使用大小写不敏感的 Distinct
            Tags = nodes.SelectMany(n => n.Tags)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList()
        };
    }
}

public record NotebookStats
{
    public int TotalCount { get; init; }
    public int GistCount { get; init; }
    public int SummaryCount { get; init; }
    public int FullCount { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
}
