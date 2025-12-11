// MemoryNotebook - DocUI 概念原型
// 
// 一个 LLM Agent 可以自主操作的外部知识库
// 支持 LOD (Level of Detail) 控制，管理信息焦点
//
// LOD 级别:
//   [GIST]    - 最小印象，一句话
//   [SUMMARY] - 摘要，保留关键信息
//   [FULL]    - 完整内容
//
// 使用方式 (通过 pmux 命令):
//
// === 查看命令 ===
//   pmux notebook view              # 查看所有条目（按当前 LOD 渲染）
//   pmux notebook view --lod full   # 以指定 LOD 级别查看所有
//   pmux notebook get <id>          # 查看指定条目
//   pmux notebook list              # 列出所有条目 ID 和标题
//   pmux notebook stats             # 显示统计信息
//   pmux notebook tags              # 列出所有标签
//   pmux notebook filter <tag>      # 按标签筛选
//
// === LOD 控制 ===
//   pmux notebook fold <id>         # 折叠到 Gist
//   pmux notebook unfold <id>       # 展开到 Full
//   pmux notebook summary <id>      # 设置为 Summary
//   pmux notebook fold-all [level]  # 批量折叠
//   pmux notebook unfold-all        # 批量展开
//   pmux notebook focus <id1> <id2> # 聚焦指定条目（展开它们，折叠其余）
//
// === 写入命令 ===
//   pmux notebook add <id> <title> [--gist] [--summary] [--full] [--tags]
//   pmux notebook remove <id>       # 删除条目

using System.CommandLine;
using DocUI.Demo.MemoryNotebook;
using DocUI.Demo.MemoryNotebook.Model;
using PipeMux.Sdk;

// === 创建有状态的 Notebook 服务 ===
var notebook = SampleData.CreateSampleNotebook();

// === 创建 PipeMux App ===
var app = new PipeMuxApp("notebook");

// === 定义命令 ===

// view - 查看所有条目
var viewLodOption = new Option<string?>("--lod") { Description = "Override LOD level (gist/summary/full)" };
var viewCommand = new Command("view", "View all entries with current LOD levels") { viewLodOption };
viewCommand.SetAction(parseResult =>
{
    var (lodOverride, error) = ParseLodOptionWithValidation(parseResult.GetValue(viewLodOption));
    if (error != null)
    {
        parseResult.Configuration.Error.WriteLine(error);
        return 1;
    }
    var output = notebook.RenderMarkdown(lodOverride);
    parseResult.Configuration.Output.WriteLine(output);
    return 0;
});

// view - 查看指定条目
var getIdArg = new Argument<string>("id") { Description = "Entry ID to view" };
var getLodOption = new Option<string?>("--lod") { Description = "Override LOD level" };
var getCommand = new Command("get", "View a specific entry") { getIdArg, getLodOption };
getCommand.SetAction(parseResult =>
{
    var id = parseResult.GetValue(getIdArg)!;
    var (lodOverride, error) = ParseLodOptionWithValidation(parseResult.GetValue(getLodOption));
    if (error != null)
    {
        parseResult.Configuration.Error.WriteLine(error);
        return 1;
    }

    var node = notebook.Get(id);
    if (node == null)
    {
        parseResult.Configuration.Error.WriteLine($"Entry not found: {id}");
        return 1;
    }

    var output = notebook.RenderNode(node, lodOverride);
    parseResult.Configuration.Output.WriteLine(output);
    return 0;
});

// fold - 折叠到 Gist
var foldIdArg = new Argument<string>("id") { Description = "Entry ID to fold" };
var foldCommand = new Command("fold", "Fold entry to Gist level") { foldIdArg };
foldCommand.SetAction(parseResult =>
{
    var id = parseResult.GetValue(foldIdArg)!;
    if (notebook.Fold(id))
    {
        var node = notebook.Get(id)!;
        parseResult.Configuration.Output.WriteLine($"Folded [{id}] to Gist:");
        parseResult.Configuration.Output.WriteLine($"  [GIST] {node.Title} — _{node.Content.GetAtLevel(LodLevel.Gist)}_");
    }
    else
    {
        parseResult.Configuration.Error.WriteLine($"Entry not found: {id}");
        return 1;
    }
    return 0;
});

// unfold - 展开到 Full
var unfoldIdArg = new Argument<string>("id") { Description = "Entry ID to unfold" };
var unfoldCommand = new Command("unfold", "Unfold entry to Full level") { unfoldIdArg };
unfoldCommand.SetAction(parseResult =>
{
    var id = parseResult.GetValue(unfoldIdArg)!;
    if (notebook.Unfold(id))
    {
        parseResult.Configuration.Output.WriteLine($"Unfolded [{id}] to Full level");
        var node = notebook.Get(id)!;
        parseResult.Configuration.Output.WriteLine();
        parseResult.Configuration.Output.WriteLine(notebook.RenderNode(node));
    }
    else
    {
        parseResult.Configuration.Error.WriteLine($"Entry not found: {id}");
        return 1;
    }
    return 0;
});

// summary - 设置为 Summary 级别
var summaryIdArg = new Argument<string>("id") { Description = "Entry ID" };
var summaryCommand = new Command("summary", "Set entry to Summary level") { summaryIdArg };
summaryCommand.SetAction(parseResult =>
{
    var id = parseResult.GetValue(summaryIdArg)!;
    if (notebook.SetLod(id, LodLevel.Summary))
    {
        parseResult.Configuration.Output.WriteLine($"Set [{id}] to Summary level");
        var node = notebook.Get(id)!;
        parseResult.Configuration.Output.WriteLine();
        parseResult.Configuration.Output.WriteLine(notebook.RenderNode(node));
    }
    else
    {
        parseResult.Configuration.Error.WriteLine($"Entry not found: {id}");
        return 1;
    }
    return 0;
});

// list - 列出所有条目
var listCommand = new Command("list", "List all entry IDs and titles");
listCommand.SetAction(parseResult =>
{
    parseResult.Configuration.Output.WriteLine("# Notebook Entries");
    parseResult.Configuration.Output.WriteLine();
    foreach (var node in notebook.GetAll())
    {
        var lodTag = node.CurrentLod switch
        {
            LodLevel.Gist => "[GIST]",
            LodLevel.Summary => "[SUMMARY]",
            LodLevel.Full => "[FULL]",
            _ => "[?]"
        };
        var tags = node.Tags.Count > 0 ? $" [{string.Join(", ", node.Tags)}]" : "";
        parseResult.Configuration.Output.WriteLine($"  {lodTag} {node.Id}: {node.Title}{tags}");
    }
});

// stats - 统计信息
var statsCommand = new Command("stats", "Show notebook statistics");
statsCommand.SetAction(parseResult =>
{
    var stats = notebook.GetStats();
    parseResult.Configuration.Output.WriteLine("# Notebook Statistics");
    parseResult.Configuration.Output.WriteLine();
    parseResult.Configuration.Output.WriteLine($"Total entries: {stats.TotalCount}");
    parseResult.Configuration.Output.WriteLine($"LOD distribution:");
    parseResult.Configuration.Output.WriteLine($"  [GIST]    {stats.GistCount}");
    parseResult.Configuration.Output.WriteLine($"  [SUMMARY] {stats.SummaryCount}");
    parseResult.Configuration.Output.WriteLine($"  [FULL]    {stats.FullCount}");
    parseResult.Configuration.Output.WriteLine();
    parseResult.Configuration.Output.WriteLine($"Tags: {string.Join(", ", stats.Tags)}");
});

// tags - 列出所有标签
var tagsCommand = new Command("tags", "List all tags");
tagsCommand.SetAction(parseResult =>
{
    var stats = notebook.GetStats();
    parseResult.Configuration.Output.WriteLine("# Tags");
    foreach (var tag in stats.Tags)
    {
        var count = notebook.GetByTag(tag).Count();
        parseResult.Configuration.Output.WriteLine($"  - {tag} ({count})");
    }
});

// filter - 按标签筛选
var filterTagArg = new Argument<string>("tag") { Description = "Tag to filter by" };
var filterCommand = new Command("filter", "Filter entries by tag") { filterTagArg };
filterCommand.SetAction(parseResult =>
{
    var tag = parseResult.GetValue(filterTagArg)!;
    var filtered = notebook.GetByTag(tag).ToList();

    if (filtered.Count == 0)
    {
        parseResult.Configuration.Output.WriteLine($"No entries with tag: {tag}");
        return 0;
    }

    parseResult.Configuration.Output.WriteLine($"# Entries tagged '{tag}'");
    parseResult.Configuration.Output.WriteLine();
    foreach (var node in filtered)
    {
        parseResult.Configuration.Output.WriteLine(notebook.RenderNode(node));
        parseResult.Configuration.Output.WriteLine();
    }
    return 0;
});

// fold-all - 批量折叠
var foldAllLevelArg = new Argument<string>("level") { Description = "Target LOD level (gist/summary)" };
var foldAllCommand = new Command("fold-all", "Fold all entries to a level") { foldAllLevelArg };
foldAllCommand.SetAction(parseResult =>
{
    var levelStr = parseResult.GetValue(foldAllLevelArg) ?? "summary";
    var level = levelStr?.ToLower() switch
    {
        "gist" => LodLevel.Gist,
        "summary" => LodLevel.Summary,
        _ => LodLevel.Summary
    };

    notebook.FoldAll(level);
    parseResult.Configuration.Output.WriteLine($"Folded all entries to {level} level");
    var stats = notebook.GetStats();
    parseResult.Configuration.Output.WriteLine($"  [GIST]    {stats.GistCount}");
    parseResult.Configuration.Output.WriteLine($"  [SUMMARY] {stats.SummaryCount}");
    parseResult.Configuration.Output.WriteLine($"  [FULL]    {stats.FullCount}");
});

// unfold-all - 批量展开
var unfoldAllCommand = new Command("unfold-all", "Unfold all entries to Full level");
unfoldAllCommand.SetAction(parseResult =>
{
    notebook.FoldAll(LodLevel.Full);
    parseResult.Configuration.Output.WriteLine("Unfolded all entries to Full level");
    var stats = notebook.GetStats();
    parseResult.Configuration.Output.WriteLine($"  [GIST]    {stats.GistCount}");
    parseResult.Configuration.Output.WriteLine($"  [SUMMARY] {stats.SummaryCount}");
    parseResult.Configuration.Output.WriteLine($"  [FULL]    {stats.FullCount}");
});

// focus - 声明式聚焦命令
// 语义: "我要关注这些条目" → 自动展开指定条目，折叠其余
var focusIdsArg = new Argument<string[]>("ids") { Description = "Entry IDs to focus on (space-separated)" };
focusIdsArg.Arity = ArgumentArity.OneOrMore;
var focusCommand = new Command("focus", "Focus on specific entries (unfold them, fold others to gist)") { focusIdsArg };
focusCommand.SetAction(parseResult =>
{
    var ids = parseResult.GetValue(focusIdsArg) ?? [];
    var focusSet = new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase);
    
    // 验证 IDs 存在
    var notFound = ids.Where(id => notebook.Get(id) == null).ToList();
    if (notFound.Count > 0)
    {
        parseResult.Configuration.Error.WriteLine($"Entries not found: {string.Join(", ", notFound)}");
        return 1;
    }
    
    // 聚焦逻辑：指定的展开到 Full，其余折叠到 Gist
    int focused = 0, folded = 0;
    foreach (var node in notebook.GetAll())
    {
        if (focusSet.Contains(node.Id))
        {
            notebook.SetLod(node.Id, LodLevel.Full);
            focused++;
        }
        else
        {
            notebook.SetLod(node.Id, LodLevel.Gist);
            folded++;
        }
    }
    
    parseResult.Configuration.Output.WriteLine($"Focused on {focused} entries, folded {folded} others to gist");
    parseResult.Configuration.Output.WriteLine();
    
    // 渲染聚焦后的视图
    parseResult.Configuration.Output.WriteLine(notebook.RenderMarkdown());
    return 0;
});

// add - 添加新条目
var addIdArg = new Argument<string>("id") { Description = "Unique entry ID" };
var addTitleArg = new Argument<string>("title") { Description = "Entry title" };
var addGistOption = new Option<string?>("--gist", "-g") { Description = "Gist (one-line summary)" };
var addSummaryOption = new Option<string?>("--summary", "-s") { Description = "Summary content" };
var addFullOption = new Option<string?>("--full", "-f") { Description = "Full content" };
var addTagsOption = new Option<string[]>("--tags") { Description = "Tags (comma or space separated)" };
addTagsOption.AllowMultipleArgumentsPerToken = true;
var addCommand = new Command("add", "Add a new entry") { addIdArg, addTitleArg, addGistOption, addSummaryOption, addFullOption, addTagsOption };
addCommand.SetAction(parseResult =>
{
    var id = parseResult.GetValue(addIdArg)!;
    var title = parseResult.GetValue(addTitleArg)!;
    var gist = parseResult.GetValue(addGistOption);
    var summary = parseResult.GetValue(addSummaryOption);
    var full = parseResult.GetValue(addFullOption);
    var tags = parseResult.GetValue(addTagsOption) ?? [];
    
    // 检查 ID 是否已存在
    if (notebook.Get(id) != null)
    {
        parseResult.Configuration.Error.WriteLine($"Entry already exists: {id}");
        return 1;
    }
    
    // 如果没有提供 full 内容，使用 summary 或 gist
    if (string.IsNullOrEmpty(full))
    {
        full = summary ?? gist ?? title;
    }
    
    // 自动生成缺失的 LOD 内容
    if (string.IsNullOrEmpty(summary))
    {
        summary = full.Length > 200 ? full[..200] + "..." : full;
    }
    if (string.IsNullOrEmpty(gist))
    {
        gist = title;
    }
    
    var node = new ContentNode
    {
        Id = id,
        Title = title,
        Source = ContentSource.UserInput,
        Content = new LodContent
        {
            Full = full,
            Summary = summary,
            Gist = gist
        },
        Tags = tags.SelectMany(t => t.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries)).ToList()
    };
    
    notebook.Add(node);
    parseResult.Configuration.Output.WriteLine($"Added entry: [{id}] {title}");
    parseResult.Configuration.Output.WriteLine();
    parseResult.Configuration.Output.WriteLine(notebook.RenderNode(node));
    return 0;
});

// remove - 删除条目
var removeIdArg = new Argument<string>("id") { Description = "Entry ID to remove" };
var removeCommand = new Command("remove", "Remove an entry") { removeIdArg };
removeCommand.SetAction(parseResult =>
{
    var id = parseResult.GetValue(removeIdArg)!;
    if (notebook.Remove(id))
    {
        parseResult.Configuration.Output.WriteLine($"Removed entry: {id}");
        return 0;
    }
    parseResult.Configuration.Error.WriteLine($"Entry not found: {id}");
    return 1;
});

// === 根命令 ===
var rootCommand = new RootCommand("MemoryNotebook - LLM Agent's external knowledge base with LOD control")
{
    viewCommand,
    getCommand,
    foldCommand,
    unfoldCommand,
    summaryCommand,
    listCommand,
    statsCommand,
    tagsCommand,
    filterCommand,
    foldAllCommand,
    unfoldAllCommand,
    focusCommand,
    addCommand,
    removeCommand
};

// === 运行 ===
await app.RunAsync(rootCommand);

// === 辅助函数 ===

/// <summary>
/// 解析 LOD 参数，无效值返回错误而非静默忽略
/// </summary>
static (LodLevel? Level, string? Error) ParseLodOptionWithValidation(string? value)
{
    if (string.IsNullOrEmpty(value))
        return (null, null);
    
    return value.ToLower() switch
    {
        "gist" => (LodLevel.Gist, null),
        "summary" => (LodLevel.Summary, null),
        "full" => (LodLevel.Full, null),
        _ => (null, $"Invalid LOD level: '{value}'. Must be one of: gist, summary, full")
    };
}
