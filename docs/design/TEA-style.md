
## 核心思想（这部分保持人手写，AI可追加评论，但误直接修改，作为立意锚点）
  - 显式把Agent系统的状态分为[persist state]和[temporary state]两种。[persist state]依靠[增量序列化]和[Append-only History]来落盘，时机为[agent state machine]成功完整完成一次状态转移时。[persist state]必须是自包含的，不能引用[temporary state]。[persist state]的运行时内存状态呈现为Immutable Object Graph，统一支持Builder模式，统一支持增量序列化，增量部分Append-only落盘。从[persist state]查询关联的[temporary state]用System.Runtime.CompilerServices.ConditionalWeakTable<TKey,TValue>实现。

> AI 注：建议把“增量序列化”与“Append-only History”的耦合做成可配置策略（例如 `SnapshotEvery = N`、`HistoryRetention = duration`），以便不同 Agent 选择更高频率的 LiveWindow 一致性或更低的磁盘压力。

## agent state machine
  - Observation
  - History

建议引入 `Transition Contract`：每个状态转移声明输入（Observation）、副作用（Tool 调用）和持久化输出（History delta）。这样可以把“可复用”抽象在合同级别（类似 TEA 的 `Program.init/update`），新 Agent 只需实现合同即可享用统一的持久化/回放策略。

可复用性提升策略：
- 定义轻量 `IPlaybackController`，负责“从最新 root 指针向后回放”，供自动恢复与“时间旅行调试”共用。
- 状态机节点可标注 `Deterministic = true`，框架遇到 nondeterministic 节点时记录额外输入（例如外部 API 响应摘要），避免复播时失真。

## agent app
  - LiveWindow
  - Notification

这里可引入“约束层”：
- LiveWindow 仅引用 `persist state` + 派生 Selector，禁止临时对象穿透，保证可复现。
- Notification 允许挂载 `HistoryRef`（例如事件 id），UI 上点击即可跳回对应快照，增强可导航性。
- 为提升复用度，每个 App 提供 `AppCapabilities` 描述（可编辑/只读/需要输入等），框架可基于能力自动编排上下文注入顺序。

## document user interface
注入LLM Context的Markdown,就是面向Agent系统中的LLM的DocUI。

可在 DocUI 层加入 `ContextBudgetPlanner`：按照 LiveWindow、Notification、History 各自的重要性和体量，动态分配 Token Budget。接口上暴露 `IContextSlice`，任意 App 都可以返回“最小必要描述 + 可选扩展”，框架按预算裁剪，提升易用性与跨 App 一致性。

## persist state
重建系统状态所需的最小必要数据集合。

建议：
- 每种 persist state 结构提供 `SchemaId`（例如基于 Source Generator 的版本号），append 新根节点时把 `SchemaId` 一起落盘，方便跨版本升级和工具复现。
- 引入 `PersistAudit`（hash + monotonic id），不仅支持校验，也让多 Agent 之间引用彼此状态更容易（相当于状态 DAG）。
- 将“数据读取器”和“Agent App 程序本身”也记录在 History 中，形成“代码即数据”的快照：
  - 稳定版本升级时，直接用老版本的 reader/exporter 帮忙导出，再由新版本导入，保证历史永远可读。
  - 小版本变化则可通过兼容适配器读取老块，反正历史分区全是只读。
  - 需要压缩时，提供工具生成“最新状态全量快照”文件（丢弃旧节点但保留引用索引），相当于对 append-only 日志做一次 compaction。

## temporary state
用于提高运行效率的，运行时的附加内存状态，可以从persist state重建。
  - cache
  - index
  - buffer

建议把构建 temporary state 的流程 Formalize 成 `Warmup Recipe`：例如 `cache := RebuildFrom(snapshot)`，`index := Replay(history tail)`。框架可以在 Agent 重新激活、或 LLM 需要特定能力时自动触发重建，保障可预测性。

## capture closure state
为重建系统状态所从外界捕获的“已感知数据”的集合。

可以为 capture closure state 定义 `Provenance` 元数据（来源、时间戳、可靠性等级）。这样在时间旅行或回放时，可以决定是否需要再次验证某些外部输入，增强故障恢复体验。

## 系统边界

建议明确两类边界：
- **Agent 内部边界**：Persist/Temporary/Closure 之间的引用规则（例如禁止 Closure 直接引用 Temporary）。
- **Agent 与宿主框架边界**：通过 Tool Contract 暴露可执行操作，并定义“失败后自动重试”策略，避免 Copilot Chat 这类“流程断裂”问题。

可复用性方面，可以把边界描述抽象为配置（YAML/JSON）+ 代码生成器，让新 Agent 只需填空式描述即可拓展。

## Append-only History

- 在 History 中划分 `Chronicle`（长久保存）与 `Ephemeral`（可清理）两个优先级，便于做归档/压实策略。
- 引入 `HistoryIndex`，为每条记录附上 `ContextTag`（例如 `ClockApp/Tick`），UI 查询时可只加载特定 App 的尾部日志，提高易用性。
- 针对框架约束，可提供 `HistorySegmentProvider` 接口，支持分段存储（文件、对象存储、EventStore 流），落地时可自由替换。
- 记录“读取器/程序版本”以及“导入导出工具”的指针，让任何快照都能声明自己需要的解释器；这样即便 Schema 大改，也能通过“老 reader 导出 → 新 reader 导入”流程保持可用。

## 增量序列化

- 建议把增量序列化拆成 `DiffPlan`（记录哪些节点发生变化）+ `Writer`（根据 plan 写 chunk）。这样 Builder 只负责标记 dirty 节点，具体写入由框架统一处理，增强共享能力。
- 可以约定 `IncrementId` 规则（例如 `AppName/StateName/Tick`），方便跨 Agent 关联同一事件的多个视图。
- 可以将“未序列化节点集合”看作 Builder 维护的 dirty set：Agent 状态机暂停时遍历 dirty set，把所有叶节点和结构节点携带的 Append-only 地址打包成一个 block 追加落盘；恢复时从最新根开始沿地址链懒加载节点，体验更像“无限内存 + 即时启动”。

## immutable and builder

- 对 Immutable Object Graph，建议统一使用 `WithXxx` + `BeginBuild()/Commit()` 模式，避免不同 App 自行实现 Builder 导致难以复用。
- 可提供 Source Generator 自动生成 `Builder<T>` 与增量序列化 hook，减少手写代码量，提升可维护性。


