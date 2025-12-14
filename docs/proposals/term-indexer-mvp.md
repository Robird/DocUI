# Term Indexer MVP 设计文档

> **状态**: Draft
> **作者**: DocUIGPT (AI Specialist)
> **日期**: 2025-12-14
> **来源**: [术语治理架构研讨会](../../../agent-team/meeting/2025-12-14-glossary-architecture-workshop.md)

---

## 背景与动机

在 2025-12-14 的术语治理架构研讨会上，我们确立了 **Primary Definition + Glossary-as-Index** 的术语治理模式：

- 每个术语在首次引入它的 Key-Note 中定义（Primary Definition）
- glossary.md 只做索引，不存放完整定义
- 定义块格式：`## Term` + `> **Term** ...`

这一架构带来了新需求：
1. **术语提取**：从各 Key-Note 自动提取定义块
2. **索引生成**：自动生成/更新 glossary.md
3. **一致性校验**：检测断链、重复定义、摘要漂移
4. **图谱导出**：为 DocUI 语义后端提供数据基础

---

## MVP 范围

### MVP-0: Term Indexer（术语提取 + 索引生成）

**目标**：从 Key-Note 提取术语定义，生成/更新 glossary 索引

**输入**：
- `DocUI/docs/key-notes/**/*.md`

**识别规则**：
- 标题：`## Term-Name` 或 `### Term-Name`（H2/H3）
- 定义块：紧随标题后的第一个 BlockQuote（`> **Term** ...`）
- 排除：代码块内的伪标题、引用块内的标题

**输出**：
- 更新 `glossary.md` 表格（术语列为链接、摘要为纯文本、状态列）
- 控制台报告：新增/变更/删除的术语

**工作量估算**：1 天

---

### MVP-1: Diagnostics（静态校验）

**目标**：检测术语治理规则违规

**校验规则**：

| 规则 | 级别 | 说明 |
|------|------|------|
| Duplicate Definition | Error | 同名术语出现多个 Primary Definition |
| Broken Link | Error | glossary 中链接不可达 |
| Summary Drift | Warning | glossary 摘要与定义块不一致 |
| Missing Definition | Warning | 疑似术语引用但未定义 |
| Orphan Term | Info | glossary 中存在但源文件中已删除 |

**输出**：
- 诊断报告（JSON/Text 格式）
- Exit code：有 Error 则非零

**工作量估算**：0.5 天

---

### MVP-2: Graph Export（概念图谱导出）

**目标**：导出术语依赖图谱，为 DocUI 语义后端奠定基础

**输出格式** (`term-graph.json`)：

```json
{
  "version": "0.1.0",
  "generatedAt": "2025-12-14T12:00:00Z",
  "nodes": [
    {
      "term": "Agent",
      "file": "llm-agent-context.md",
      "anchor": "agent",
      "status": "Stable",
      "definition": "能感知环境、为达成目标而行动、并承担行动后果的计算实体"
    }
  ],
  "edges": [
    {
      "from": "doc-as-usr-interface.md",
      "to": "Agent",
      "type": "reference"
    }
  ]
}
```

**应用场景**：
- DocUI 的 "Smart Tooltip"（悬停显示术语定义）
- DocUI 的 "Semantic Navigation"（概念关系导航）
- LLM Agent 的 "概念图谱内省"（ConceptGraph.Query）

**工作量估算**：0.5 天

---

## 技术选型

### 为什么用 Markdig AST 而不是正则

| 方案 | 优点 | 缺点 |
|------|------|------|
| **正则** | 简单快速 | 易被嵌套块、引用块、代码块击穿；维护困难 |
| **Markdig AST** | 结构化解析；准确识别块类型；与 DocUI 技术栈一致 | 略复杂 |

**决策**：使用 Markdig AST。

### Markdig 关键扩展点

```csharp
// 遍历 Heading + BlockQuote 的示例伪代码
foreach (var block in document)
{
    if (block is HeadingBlock heading && heading.Level <= 3)
    {
        var termName = heading.Inline.FirstChild?.ToString();
        var nextBlock = GetNextNonEmptyBlock(heading);
        if (nextBlock is QuoteBlock quote)
        {
            // 提取定义块内容
            var definition = ExtractDefinition(quote);
            yield return new TermDefinition(termName, definition, heading.Line);
        }
    }
}
```

### 锚点生成算法

统一使用 GitHub 风格的 slug 算法：
1. 转小写
2. 空格替换为 `-`
3. 移除特殊字符（保留字母、数字、连字符）
4. 连续连字符合并

```csharp
string Slugify(string title)
    => Regex.Replace(title.ToLowerInvariant(), @"[^\w\-]", "-")
           .Trim('-')
           .Replace("--", "-");
```

---

## 项目结构建议

```
DocUI/
├── src/
│   └── DocUI.TermIndexer/          # CLI 工具
│       ├── Program.cs              # 入口
│       ├── TermExtractor.cs        # 术语提取
│       ├── GlossaryGenerator.cs    # 索引生成
│       ├── Diagnostics.cs          # 校验
│       └── GraphExporter.cs        # 图谱导出
├── docs/
│   ├── key-notes/
│   │   └── glossary.md             # 自动生成/更新
│   └── proposals/
│       └── term-indexer-mvp.md     # 本文档
└── term-graph.json                 # 自动生成
```

---

## 命令行接口（草案）

```bash
# MVP-0: 生成索引
dotnet docui-term index --input ./docs/key-notes --output ./docs/key-notes/glossary.md

# MVP-1: 诊断校验
dotnet docui-term diagnose --input ./docs/key-notes --report ./term-report.json

# MVP-2: 图谱导出
dotnet docui-term graph --input ./docs/key-notes --output ./term-graph.json

# 组合命令
dotnet docui-term all --input ./docs/key-notes
```

---

## 与 DocUI 的关联

### 短期价值

- **一致性保障**：自动检测术语治理违规
- **索引自动化**：减少手动维护 glossary 的负担
- **文档质量**：通过诊断报告发现问题

### 长期愿景

| DocUI 能力 | Term Indexer 贡献 |
|------------|------------------|
| Smart Tooltip | `term-graph.json` 提供术语定义数据 |
| Semantic Navigation | `edges` 提供术语引用关系 |
| LOD 语义切分 | 术语依赖图指导信息分层 |
| 概念图谱内省 | Agent 可查询"什么概念有定义、如何相互引用" |

这正是研讨会上提出的 **DSP (Documentation Server Protocol)** 的原型——为文档构建类似 LSP 的静态分析引擎。

---

## 开放问题

1. **锚点中文支持**：中文术语的 slug 规则？（拼音？保留中文？）
2. **增量更新**：是否需要支持 watch mode？
3. **CI 集成**：作为 GitHub Actions 还是 pre-commit hook？
4. **版本策略**：`term-graph.json` 的 schema 版本如何管理？

---

## 里程碑

| 阶段 | 内容 | 估算 |
|------|------|------|
| MVP-0 | 术语提取 + 索引生成 | 1 天 |
| MVP-1 | 诊断校验 | 0.5 天 |
| MVP-2 | 图谱导出 | 0.5 天 |
| **合计** | | **2 天** |

---

*本文档为设计草案，详细实现后续迭代*
