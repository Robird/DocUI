# DocUI 渲染框架设计文档

> 状态: 设计阶段
> 创建日期: 2025-12-10
> 作者: Team Leader (刘德智)

## 1. 动机与背景

通过 MemoryNotebook、TextEditor、SystemMonitor 三个概念原型，我们发现了渲染层的共性模式：

| 原型 | 数据特性 | LOD 对象 | 渲染特点 |
|------|----------|----------|----------|
| MemoryNotebook | 静态条目集合 | 单个条目 | 每条目独立 LOD 控制 |
| TextEditor | 文本模型 | 整体视图 | 光标位置、行号、代码围栏 |
| SystemMonitor | 动态指标 | 整体视图 | 同一数据不同呈现密度 |

**共性需求**:
1. Model/State → Markdown 的自动渲染
2. LOD 三级呈现 (Gist/Summary/Full)
3. 可操作的 UI 锚点 (id/op anchor)
4. 命令/工具的可见性管理

## 2. 设计目标

### 2.1 低代码渲染

**目标**: 开发者定义 Model，框架自动生成 Markdown 输出

```csharp
// 开发者写这个
public class SystemStatus
{
    [LodGist("CPU {CpuPercent}%")]
    [LodSummary] // 使用默认表格渲染
    public CpuMetrics Cpu { get; set; }
    
    [LodFull]  // 只在 Full 级别显示
    public List<ProcessInfo> Processes { get; set; }
}

// 框架自动生成
var markdown = Renderer.Render(status, LodLevel.Summary);
```

### 2.2 UI 锚点 (Anchor) 系统

**目标**: 在 Markdown 中嵌入可操作的锚点，LLM Agent 可以"点击"

```markdown
## Notebook Entry [button:fold] [button:expand]

[GIST] **[entry-1]** PipeMux 概览 — _进程编排框架_
       [button:view] [form:edit id=entry-1]
```

锚点格式:
- `[button:<cmd>]` - 无参动作，点击即执行
- `[form:<cmd> <param>=<value>]` - 有参动作，需要参数
- `[fold:<id>]` / `[expand:<id>]` - LOD 控制（Button 的语义别名）
- `[ref:<id>]` - 引用锚点，用于定位

### 2.3 LOD 三级呈现

| 级别 | 语义 | 典型用途 |
|------|------|----------|
| Gist | "知道存在" | 一行印象，最小 token |
| Summary | "大概了解" | 摘要表格，日常工作状态 |
| Full | "完整细节" | 所有信息，深入查看 |

**关键设计**:
- 对 LLM，LOD 是**内容替换**，不是视觉折叠
- 标签用文字 `[GIST]` 而非符号 `▶`
- 高熵 Gist: 即使最小也要透出关键信息

### 2.4 命令可见性管理

**目标**: 根据上下文动态显示可用命令（微流程/向导基础）

```markdown
## 当前可用操作

- `notebook add <id> <title>` — 添加条目
- `notebook focus <id>` — 聚焦查看
- `notebook fold-all` — 全部折叠

> 已折叠 3 个不相关命令
```

**场景**:
- 向导流程: 分步骤显示相关命令
- 上下文感知: 有文件打开时显示编辑命令
- LOD 联动: Full 级别显示更多高级命令

## 3. 架构概览

```
┌─────────────────────────────────────────────┐
│                Application                  │
│  ┌─────────┐  ┌─────────┐  ┌─────────────┐ │
│  │ Model   │  │ State   │  │ Commands    │ │
│  └────┬────┘  └────┬────┘  └──────┬──────┘ │
└───────┼────────────┼──────────────┼────────┘
        │            │              │
        ▼            ▼              ▼
┌─────────────────────────────────────────────┐
│              DocUI Framework                │
│  ┌──────────────────────────────────────┐  │
│  │           Rendering Engine            │  │
│  │  ┌────────────┐  ┌────────────────┐  │  │
│  │  │ LOD Router │  │ Anchor Manager │  │  │
│  │  └────────────┘  └────────────────┘  │  │
│  │  ┌────────────┐  ┌────────────────┐  │  │
│  │  │ Markdown   │  │ Command        │  │  │
│  │  │ Generator  │  │ Visibility     │  │  │
│  │  └────────────┘  └────────────────┘  │  │
│  └──────────────────────────────────────┘  │
└─────────────────────────────────────────────┘
        │
        ▼
┌─────────────────────────────────────────────┐
│              Markdown Output                │
│  (With embedded anchors & visible commands) │
└─────────────────────────────────────────────┘
```

## 4. 核心组件

### 4.1 LodAttribute 系列

```csharp
// 标记 LOD 级别
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Class)]
public class LodGistAttribute : Attribute
{
    public string? Template { get; }  // 可选模板: "CPU {Value}%"
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Class)]
public class LodSummaryAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Class)]
public class LodFullAttribute : Attribute { }

// 标记锚点
[AttributeUsage(AttributeTargets.Property)]
public class AnchorAttribute : Attribute
{
    public string IdProperty { get; }  // 引用 ID 属性
    public string[] Actions { get; }   // 可用操作
}
```

### 4.2 IRenderable 接口

```csharp
public interface IRenderable
{
    string RenderMarkdown(LodLevel level, RenderContext context);
}

public interface ILodRenderable : IRenderable
{
    string RenderGist(RenderContext context);
    string RenderSummary(RenderContext context);
    string RenderFull(RenderContext context);
}
```

### 4.3 RenderContext

```csharp
public class RenderContext
{
    public LodLevel CurrentLod { get; }
    public AnchorRegistry Anchors { get; }
    public CommandVisibility Commands { get; }
    public Dictionary<string, object> State { get; }
}
```

### 4.4 AnchorRegistry

```csharp
public class AnchorRegistry
{
    // 注册锚点
    public void Register(string id, AnchorType type, string command);
    
    // 生成锚点 Markdown
    public string RenderAnchor(string id, AnchorType type);
    
    // 验证锚点是否有效
    public bool Validate(string anchorText);
}

public enum AnchorType
{
    Button,    // [button:cmd] - 无参动作
    Form,      // [form:cmd param=value] - 有参动作
    Reference  // [ref:id] - 引用锚点
}
```

### 4.5 CommandVisibility

```csharp
public class CommandVisibility
{
    // 根据上下文过滤可见命令
    public IEnumerable<CommandInfo> GetVisibleCommands(VisibilityContext ctx);
    
    // 渲染可用命令区块
    public string RenderAvailableCommands(LodLevel level);
}

public record CommandInfo
{
    public string Name { get; init; }
    public string Description { get; init; }
    public LodLevel MinimumLod { get; init; }
    public Func<object, bool>? VisibilityCondition { get; init; }
}
```

## 5. 使用示例

### 5.1 定义 Model

```csharp
public class NotebookEntry : ILodRenderable
{
    [Anchor(nameof(Id), Actions = ["fold", "unfold", "edit", "delete"])]
    public string Id { get; set; }
    
    public string Title { get; set; }
    
    [LodFull]
    public string FullContent { get; set; }
    
    [LodSummary]
    public string Summary { get; set; }
    
    [LodGist("{Title} — {Summary[..50]}")]
    public string Gist { get; set; }
    
    public string RenderGist(RenderContext ctx) =>
        $"[GIST] **[{Id}]** {Title} — _{Gist}_ {ctx.Anchors.RenderAnchor(Id, AnchorType.Expand)}";
    
    // ... Summary, Full 实现
}
```

### 5.2 注册命令可见性

```csharp
var commands = new CommandVisibility();

commands.Register(new CommandInfo
{
    Name = "notebook add",
    Description = "添加新条目",
    MinimumLod = LodLevel.Summary,
    VisibilityCondition = _ => true  // 始终可见
});

commands.Register(new CommandInfo
{
    Name = "notebook edit",
    Description = "编辑条目内容",
    MinimumLod = LodLevel.Full,  // 只在 Full 级别显示
    VisibilityCondition = ctx => ctx.HasSelection
});
```

### 5.3 渲染输出

```csharp
var context = new RenderContext
{
    CurrentLod = LodLevel.Summary,
    Anchors = anchorRegistry,
    Commands = commandVisibility
};

var output = renderer.Render(notebook, context);
```

输出:
```markdown
# 📓 Memory Notebook

> 5 entries | LOD: Summary

[SUMMARY] **[entry-1]** PipeMux 概览 `project` [fold:entry-1]

> PipeMux 是 ATELIA 计划中的进程编排层...

---

## 可用命令

- `notebook add <id> <title>` — 添加新条目
- `notebook focus <id>` — 聚焦查看
- `notebook fold-all` — 全部折叠
```

## 6. 实现路径

### Phase 1: 基础 LOD 渲染
- [ ] `IRenderable` / `ILodRenderable` 接口
- [ ] `LodAttribute` 系列
- [ ] 反射驱动的自动渲染器
- [ ] 单元测试

### Phase 2: 锚点系统
- [ ] `AnchorRegistry` 实现
- [ ] 锚点格式规范
- [ ] 锚点解析器（从 Markdown 提取）
- [ ] 锚点验证

### Phase 3: 命令可见性
- [ ] `CommandVisibility` 实现
- [ ] 上下文条件评估
- [ ] LOD 联动
- [ ] 可用命令区块渲染

### Phase 4: 迁移现有原型
- [ ] MemoryNotebook 迁移
- [ ] SystemMonitor 迁移
- [ ] TextEditor 迁移（部分适用）

## 7. 软件工程手段分析

### 7.1 现有技术参考

| 技术 | 借鉴点 | 差异 |
|------|--------|------|
| React/Vue | 声明式 UI、组件化 | 输出是 Markdown 不是 DOM |
| Blazor | C# 组件、状态管理 | 无交互式渲染 |
| Razor | 模板语法 | 我们更侧重 LOD |
| Source Generators | 编译时代码生成 | 可用于生成渲染代码 |

### 7.2 实现选项

**选项 A: 反射 + 特性**
- 优点: 简单、灵活、热更新
- 缺点: 运行时性能开销
- 适合: 概念验证阶段

**选项 B: Source Generator**
- 优点: 编译时生成、零运行时开销
- 缺点: 调试困难、学习成本
- 适合: 性能敏感场景

**选项 C: 模板引擎**
- 优点: 灵活的模板定制
- 缺点: 额外的模板语言
- 适合: 高度定制化需求

**推荐路径**: 
1. Phase 1-2 用反射 + 特性（快速验证）
2. 稳定后考虑 Source Generator 优化

### 7.3 设计模式

- **Builder 模式**: 构建复杂的 Markdown 输出
- **Visitor 模式**: 遍历 Model 树生成不同 LOD 输出
- **Strategy 模式**: 不同类型的渲染策略
- **Chain of Responsibility**: 命令可见性条件链

## 8. 开放问题

### Q1: 锚点格式如何设计？
- `[button:cmd]` / `[form:cmd param=value]` 已确定为基础格式
- 详见 Proposal-0003 锚点语法规范

### Q2: 如何处理嵌套 LOD？
- 父节点 Summary + 子节点 Full？
- 是否允许混合 LOD？

### Q3: 命令可见性的粒度？
- 全局 vs 每条目？
- 静态声明 vs 运行时条件？

### Q4: 与 PipeMux 的集成边界？
- 渲染框架是否应该知道 PipeMux？
- 锚点命令如何映射到 PipeMux 命令？

---

## 附录 A: 术语表

| 术语 | 定义 |
|------|------|
| LOD | Level of Detail，信息详细程度 |
| Anchor | 嵌入 Markdown 的可操作锚点 |
| Button | 无参交互锚点，点击即执行 |
| Form | 有参交互锚点，需要填参数 |
| Reference | 引用锚点，用于定位而非操作 |
| Gist | 最小信息级别，一行印象 |
| Summary | 摘要级别，日常工作状态 |
| Full | 完整级别，所有细节 |
| LA | LLM Accessibility，面向 LLM Agent 的可访问性设计 |
| 微流程 | 分步骤引导的交互流程 |

## 附录 B: 参考文件

- MemoryNotebook: `DocUI/demo/MemoryNotebook/`
- SystemMonitor: `DocUI/demo/SystemMonitor/`
- TextEditor: `DocUI/demo/TextEditor/`
- PipeMux.SDK: `PipeMux/src/PipeMux.Sdk/`

---

*最后更新: 2025-12-10*
