## 跨会话记忆文档
本文档(`./AGENTS.md`)会伴随每个 user 消息注入上下文，是跨会话的外部记忆。完成一个任务、制定或调整计划时务必更新本文件，避免记忆偏差。

## 已知的工具问题
- 需要要删除请用改名替代，因为环境会拦截删除文件操作。
- 不要使用'insert_edit_into_file'工具，经常产生难以补救的错误结果。

## 用户语言
请主要用简体中文与用户交流，对于术语/标识符等实体名称则不不受限制。

## 目录结构
`docs\GitHub-Flavored-Markdown-Spec`: 按章节拆分开的GFM Spec。
`reference\asciidoc-lang`: 作为外部参考用的asciidoc-lang的repo clone。
`reference\cmark-gfm`: 作为外部参考用的cmark-gfm的repo clone。
`DESIGN.md`: 核心设计文档。
`DESIGN-BACKLOG.md`: 待办事项和挖坑清单（详细）。
`MARKDOWN-VS-ASCIIDOC.md`: 格式选择的详细对比分析（已决策：选择 Markdown）。
`USER-STORY-IDE.md`: AI Coder IDE 的完整设计稿（Markdown UI 原型）。
`TUI-RESEARCH.md`: TUI 库与终端 IDE 调研报告（技术选型参考）。
`LANGUAGE-ARCHITECTURE-DECISION.md`: 编程语言与架构选型深度分析（已决策：C# + Roslyn）。

## 项目状态（2025-11-18）

**当前阶段：** 架构设计与挖坑

**已完成：**
- ✅ 明确项目定位：为 LLM Agent 设计的纯文本 TUI 库
- ✅ 澄清功能边界：DocUI vs Agent 框架的职责划分
- ✅ 确立核心机制：LOD 管理、信息注入、Notification、上下文渲染
- ✅ 定义核心愿景：LLM 的自主上下文管理系统
- ✅ 创建设计文档和 Backlog
- ✅ 格式选择决策：GitHub Flavored Markdown (+ 自定义约定)
- ✅ 完成 AI Coder IDE 的完整 UI 设计稿（User Story）
- ✅ 完成 TUI 库与终端 IDE 调研（技术选型）
- ✅ 编程语言与架构决策：C# + Roslyn + "众 Agent"

**进行中：**
- 🔄 逐步填坑，设计核心接口和控件

**技术栈：**
- C# + .NET 9.0
- GitHub Flavored Markdown (GFM)
- 与 Agent 框架集成（基于 `[ToolAttribute]` 反射机制）
- **基础：** Fork Terminal.Gui v2 进行去渲染化改造
- **架构：** 即时模式 + Elm Architecture
- **参考：** Helix (选区系统)、Textual (样式分离)、Bubble Tea (状态管理)

## 关键设计决策

1. **纯文本生成器**：DocUI 只生成 Markdown，不做渲染
2. **全局 FIFO 队列**：所有 App 共享 AbstractToken 配额
3. **4 种信息注入**：History/Window/Notification/Dynamic
4. **LOD 分级**：History(2级) + Window(3级) + Notification(2级)
5. **向导控件**：Past(Gist)/Current(Full)/Future(Gist) 三段式，避免 LLM "失忆"
6. **Memory Notebook**：核心 App，LLM 自主管理知识树
7. **选区可视化**：代码围栏 + 图例（`╔═══╗` + `█` 光标），不修改 tokenizer
8. **预览驱动编辑**：Select → Preview → Confirm → Apply，零意外编辑流程

## 最新记忆
- 2025-11-20：概念稿更新 `MarkerPalette.Allocate(IText text, int count)`，通过检测 `IText.Contains` 退避正文冲突，支撑 LLM `str_replace` 多匹配场景的多选区提示与后续按选区 id 交互。
- 2025-11-20：创建 `DocUI.Text.Abstractions` 项目，抽象 `ITextBuffer/ITextSnapshot/ITextSnapshotLine`，并交付首个基于字符串分段的 `SegmentSnapshot` 最小实现，后续渲染/编辑管道可以在此基础上扩展。
- 2025-11-20：`SegmentSnapshot` 升级为多 segment + Legend metadata 架构，新增 `FromSnapshot/FromMemoryChunks` 工厂与 `ReplaceLineSegments` 等编辑 API，概念稿同步引用 `SegmentSnapshot` 作为渲染期管道。
- 2025-11-20：`TextBox` 将 Legend 与围栏渲染逻辑下移到 `Paragraph`/`CodeFence` 组件，控件自身仅负责生成数据并分发，以便在其他场景复用。
- 2025-11-21：`SegmentSnapshot` 不再记录 `_lineEnding`，原始文本按 CR/LF 拆行并去除空行，`ToString()` 现在固定输出 `\n` 以简化渲染缓存。
- 2025-11-21：`SegmentSnapshot` 引入集中式 `SegmentLineBuilder`、懒加载 `SegmentSnapshotLine` 视图、数组化行克隆以及 `FromText(string)` 重载，减少拆行/复制开销并让 `WithReplace` 避免双重字符串复制。
- 2025-11-21：`SegmentSnapshot` 对外改用 `ReadOnlyMemory<char>` 内容 API，去掉 `ReadOnlySpan<char>` 入口以避免零拷贝误导。
- 2025-11-21：新增 `docs/design/text-buffer-pipeline.md`，确立 `ITextBuffer`/`ITextReadOnly` 分层、PieceTree/Rope 常驻缓冲 + `SegmentSnapshotBuilder` overlay 的渲染方案与优化目标。
- 2025-11-26：`SegmentSnapshot` 的长度与偏移统一改为忽略换行符，`WithReplace` 通过逻辑→物理索引转换串联字符串替换，避免隐式 `\n` 干扰定位。
