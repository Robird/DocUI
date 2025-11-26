# DocUI 文本缓冲与渲染方案

## 目录
- 背景与目标
- 接口分层策略
- 数据结构选择
- 渲染层 Overlay 管线
- API 风格与可用性
- 优化目标与权衡
- 后续扩展与演进

## 背景与目标
DocUI 需要支持 LLM 驱动的文本编辑与渲染：内层编辑器必须高效处理多次增删，外层渲染只需一次同步调用即可输出 Markdown。为此我们确立“可变缓冲 + 只读快照”的双层架构，用最小的对象数量支撑行级 overlay、行号、选区、围栏等延迟渲染需求，并将中间态在一次渲染后全部释放。

## 接口分层策略
采用 `ITextBuffer` + `ITextReadOnly` 的二元接口：
- `ITextBuffer`：面向编辑器，提供就地编辑语义与最小拷贝，底层由常驻数据结构（PieceTree/Rope）实现，可随时 `Freeze()` 生成只读视图。
- `ITextReadOnly`：面向渲染/分析，保证引用期间内容稳定但不复制实体数据，允许在 stack-scope 内绑定 overlay builder，并在渲染完成后通过匹配的 `Release()`/`Unfreeze()` API 显式归还所有权。

后续若需要时间旅行或跨 Agent 缓存，再在此基础上增加 `ITextSnapshot`/`ITextImmutable`，但当前阶段保持接口最少、专注热路径。

## 数据结构选择
内层 `ITextBuffer` 选用已有的 PieceTree 或 Rope 实现，优点是：
- 具备结构共享与高效插入删除，适合作长文本编辑。
- 移植成熟代码比重新设计简洁。

渲染层 overlay 采用 `OverlayBuffer`：它实现行级 `ITextBuffer` 语义但仅在渲染调用期间存在，按行（`LineSegment[]`）封装 `ReadOnlyMemory<char>`，作为短生命周期的 builder 承载写入与变更。Builder 内部维护可变行数组，可在两种模式下结束：
1. 调试或需要缓存时调用 `BuildFrame()` 生成只读的 `OverlayFrame`（可被多次 materialize）。
2. 常规渲染直接调用 `Materialize()` 将内容写入目标 writer，随后释放内部缓冲且不产生额外对象。

暂不引入树化行表，仅在需要 diff/局部重排时再升级。

## 渲染层 Overlay 管线
1. 上层编辑器在需要渲染时调用 `ITextBuffer.Freeze()`，得到 `ITextReadOnly`，并记录对应的 `ReleaseHandle`。
2. 渲染函数创建 `OverlayBuffer`，把只读内容按行装入并叠加装饰（行号、选区、通知提示），该阶段称为 overlay build。
3. 根据需求选择：
	- 直接调用 `OverlayBuffer.Materialize(writer)`：将结果序列化到 `StringBuilder` 或 `IBufferWriter<char>`，这是唯一的 materialize 点；
	- 或者 `OverlayBuffer.BuildFrame()` 取得只读 `OverlayFrame`，交由其他组件重复使用后再 materialize。
4. 完成后立刻 `Dispose` Builder，并调用 `ReleaseHandle.Dispose()`（或 `ITextBuffer.Unfreeze()`）释放只读视图所有权。

该流程确保 overlay 只在需要时分配，且所有临时结构都在单个调用栈中存活，方便 GC。

## API 风格与可用性
采用 “C Style + 行号” 模式：所有行级操作都挂在 `ITextBuffer/ITextReadOnly` 上并使用 `int lineIndex` 访问，避免下游长期持有 `Line` 对象导致生命周期泄露；同样也利于 FFI 与多语言 Agent 调用。若需要对象语义，可在 UI 端自行封装。

## 优化目标与权衡
目标：
- 渲染路径零冗余拷贝，按需 materialize（依赖“渲染层 Overlay 管线”中的 `Materialize()` 单点输出）。
- 编辑路径支持大文本、频繁增删，不牺牲局部性（依托“数据结构选择”中 PieceTree/Rope 的特性）。
- 接口清晰，避免 Span 伪“零拷贝”的误导（“接口分层策略”统一要求通过 `ReadOnlyMemory<char>` 传递内容）。

权衡：
- 行级 overlay 暂时用数组 + Builder copy-on-write，而非完整 PieceTree；换来实现简洁、可针对栈生命周期做优化，同时能在未来替换为树化行表。
- 通过 `ReadOnlyMemory<char>` 作为内容承载，要求调用方在真正零拷贝时自行维护 buffer 生命周期，但换来语义更准确、与 `ITextReadOnly` 的 Freeze/Release 语义对齐。

## 后续扩展与演进
短期：
- 实现 `OverlayBuffer` 的 ArrayPool/池化复用，减少行装饰时的分配。
- 在 overlay 阶段支持 diff/preserve 行 ID，方便后续增量渲染。

中期：
- 若需要时间旅行/Undo 或 Agent 共享快照，再引入 `ITextSnapshot`/`ITextImmutable`，并评估树化行表或 Rope-based overlay。
- 结合 `IBufferWriter<char>` 流式输出，探索零 materialize 的渲染器。
