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
- 2025-11-30：`OverlayBuilder` 重构为接收 `SegmentListBuilder` 参数，`Build()` 现在直接往持有的 builder 中从后往前插入 overlay 并返回它。行列 API（`InsertAtLine`/`SurroundRangeLines`）从 `OverlayBuilderLineAdapter` 合并入 `OverlayBuilder`，`OverlayBuilderLineAdapter.cs` 已废弃（重命名为 `.deprecated`）。
  - `SegmentListBuilder`：底层段列表操作器，即时生效，每次插入改变后续 offset
  - `OverlayBuilder`：渲染期叠加层生成器，基于原始文本坐标的声明式 API，支持 `InsertAt`/`SurroundRange`，`Build()` 时排序后统一应用
- 2025-11-28：为 OverlayBuilder 整合 OverlayBuilderLineAdapter 采样三套方案（Segment-native、持久化 Snapshot、Multi-LOD），已分别落地为 `docs/design/overlaybuilder/option-*.md`。
- 2025-11-28：OverlayBuilder 重构方案二次采样（消除 OverlayBuilderLineAdapter，内部使用已分行结构），新增三个方案文档：
  - `unified-overlay-builder.md`：方案 A 完整版，内部改为 `StructList<LineData>` 按行存储，直接集成行列 API
  - `option-D-lazy-facade.md`：懒加载外观模式，保持扁平存储，通过 `Lines` 视图属性 + 可插拔 `ICoordinateSystem` 提供行列能力
  - `option-E-dual-index.md`：双层索引 + 延迟物化，仅存 `int[]` 行起始偏移，行长度/内容按需从偏移差值推导
- 2025-11-20：创建 `DocUI.Text.Abstractions` 项目，抽象 `ITextBuffer/ITextSnapshot/ITextSnapshotLine`，并交付首个基于字符串分段的 `SegmentSnapshot` 最小实现，后续渲染/编辑管道可以在此基础上扩展。
- 2025-11-20：`SegmentSnapshot` 升级为多 segment + Legend metadata 架构，新增 `FromSnapshot/FromMemoryChunks` 工厂与 `ReplaceLineSegments` 等编辑 API，概念稿同步引用 `SegmentSnapshot` 作为渲染期管道。
- 2025-11-20：`TextBox` 将 Legend 与围栏渲染逻辑下移到 `Paragraph`/`CodeFence` 组件，控件自身仅负责生成数据并分发，以便在其他场景复用。
- 2025-11-21：`SegmentSnapshot` 不再记录 `_lineEnding`，原始文本按 CR/LF 拆行并去除空行，`ToString()` 现在固定输出 `\n` 以简化渲染缓存。
- 2025-11-21：`SegmentSnapshot` 引入集中式 `SegmentLineBuilder`、懒加载 `SegmentSnapshotLine` 视图、数组化行克隆以及 `FromText(string)` 重载，减少拆行/复制开销并让 `WithReplace` 避免双重字符串复制。
- 2025-11-21：`SegmentSnapshot` 对外改用 `ReadOnlyMemory<char>` 内容 API，去掉 `ReadOnlySpan<char>` 入口以避免零拷贝误导。
- 2025-11-21：新增 `docs/design/text-buffer-pipeline.md`，确立 `ITextBuffer`/`ITextReadOnly` 分层、PieceTree/Rope 常驻缓冲 + `SegmentSnapshotBuilder` overlay 的渲染方案与优化目标。
- 2025-11-26：`SegmentSnapshot` 的长度与偏移统一改为忽略换行符，`WithReplace` 通过逻辑→物理索引转换串联字符串替换，避免隐式 `\n` 干扰定位。
- 2025-11-27：`OverlayBuilder` 引入 `InsertSegmentsCore` span 批量插入，公开 API 自动拆分 CR/LF 并通过 `SplitLineAt` 创建新行，私有 core 仅接受无换行段（Debug.Assert 检查）。
- 2025-11-27：单段 `OverlayBuilder.Insert` 直接调用 `InsertNormalizedSegment`，去掉临时 `ReadOnlySpan` 包装回环。
- 2025-11-27：`OverlayBuilder` 清理行级插入 API，新增按全局 offset 与 (line,column) 的 `Insert` 重载，所有外部插入统一走 `InsertNormalizedSegments`，并以 `EnsureDocumentInitialized` 取代公开空行追加。
- 2025-11-27：`StructList<T>` 的 `Add`/`Insert`/`BinarySearch` 入参改为 `in T`，降低大值类型复制成本。
- 2025-11-27：`StructList<T>` 移除未用的 `Get` 并新增 `Set(int index, in T item)` 以便原位覆盖元素。
- 2025-11-27：`StructList<T>` 采用 V3 极简方案：删除 `Peek`/`TryPeek`/`Last` 别名，仅保留 `First()`/`Last()` 返回 `ref T`；不提供 Try 版本以坚守零拷贝原则，调用方应使用 `IsEmpty` 预检查。
- 2025-11-27：`StructList<T>.BinarySearchBy` 重构为 static abstract interface members 方案：新增 `IKeySelector<T,TKey>` 接口，`BinarySearchBy<TKey, TSelector>(key)` 通过泛型特化实现零开销抽象，删除原 `Func` 委托版本。
- 2025-11-27：审查 `StructList<T>`，确认 API/实现已接近定稿，但仍需在进入单测前补充“避免按值复制”的使用约束与 `SetCapacity` 的容量断言。
- 2025-11-27：`StructList<T>` XML Doc 增补强警告与使用准则（字段持有、ref 传递、ArrayPool 生命周期管理等），作为“禁止复制”策略的可交付部分。
- 2025-11-27：`StructList<T>` 引入 `_version` + fail-fast 枚举器，所有结构性修改都会 bump 版本并在迭代时抛出 `InvalidOperationException`，同时建立 `tests/DocUI.Text.Tests` xUnit 项目补足枚举器相关单测。
- 2025-11-27：`DocUI.Text.Tests` 添加 `StructListBasicTests` 与 `StructListAdvancedTests`，覆盖 Add/Insert/Remove/Reset/Detach/Span 访问、容量管理与二分查找等主流程，`dotnet test` 全部通过。
- 2025-11-27：采样 SubAgent 建议：StructList 的“禁止复制”策略暂定为“强化 XML Doc + 内部 Roslyn Analyzer + Debug Guard”，枚举器行为倾向引入 `_version` fail-fast 并配套文档/单测。
