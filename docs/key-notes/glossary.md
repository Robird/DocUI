# DocUI 术语索引 (Glossary)

> **状态**: 索引文件（Primary Definition 分布在各 Key-Note）
> **最后更新**: 2025-12-14
>
> 本文件是 DocUI 术语的**索引**。每个术语的完整定义见"定义位置"链接。

---

## 核心概念

| 术语 | 一句话摘要 | 定义位置 | 状态 |
|------|-----------|---------|------|
| [Agent](llm-agent-context.md#agent) | 能感知环境、为达成目标而行动、并承担行动后果的计算实体 | llm-agent-context.md | Stable |
| [Environment](llm-agent-context.md#environment) | Agent 系统中的外部状态转移函数 | llm-agent-context.md | Stable |
| [Agent-OS](llm-agent-context.md#agent-os) | LLM 与 Environment 之间进行交互的中间件 | llm-agent-context.md | Stable |
| [LLM](llm-agent-context.md#llm) | Agent 系统中的内部状态转移函数 | llm-agent-context.md | Stable |

---

## 通信与交互

| 术语 | 一句话摘要 | 定义位置 | 状态 |
|------|-----------|---------|------|
| [Observation](llm-agent-context.md#observation) | Agent-OS 发送给 LLM 的 Message | llm-agent-context.md | Stable |
| [Action](llm-agent-context.md#action) | LLM 发送给 Agent-OS 的 Message，由 Thinking 和 Tool-Call 组成 | llm-agent-context.md | Stable |
| [Tool-Call](llm-agent-context.md#tool-call) | LLM 发出的、Agent-OS 负责执行的同步功能调用 | llm-agent-context.md | Stable |
| [Thinking](llm-agent-context.md#thinking) | Action 中非 Tool-Call 的部分，体现 Chain-of-Thought 推理过程 | llm-agent-context.md | Stable |
| [Message](llm-agent-context.md#message) | LLM 与 Agent-OS 之间的一次单向信息传递 | llm-agent-context.md | Stable |

---

## 历史与状态

| 术语 | 一句话摘要 | 定义位置 | 状态 |
|------|-----------|---------|------|
| [Agent-History](llm-agent-context.md#agent-history) | Agent 系统状态的一部分，由 Agent-OS 负责记录、管理、维护 | llm-agent-context.md | Stable |
| [HistoryEntry](llm-agent-context.md#historyentry) | Agent-History 中的单条记录，包含 Basic + Detail 两个 LOD 级别信息 | llm-agent-context.md | Stable |
| [History-View](llm-agent-context.md#history-view) | Agent-OS 通过 Context-Projection 渲染的、用于向 LLM 展示的历史部分信息 | llm-agent-context.md | Stable |
| [Context-Projection](llm-agent-context.md#context-projection) | 由 HistoryEntry 和 AppState 生成 IHistoryMessage[] 的过程 | llm-agent-context.md | Stable |

---

## DocUI 核心

| 术语 | 一句话摘要 | 定义位置 | 状态 |
|------|-----------|---------|------|
| [DocUI](doc-as-usr-interface.md#docui) | LLM-Native 的用户界面框架，将 Markdown 文档作为 LLM 感知系统状态的界面 | doc-as-usr-interface.md | Stable |
| [Window](doc-as-usr-interface.md#window) | 呈现给 LLM 的当前系统状态的快照视图 | doc-as-usr-interface.md | Stable |
| [Notification](doc-as-usr-interface.md#notification) | 呈现给 LLM 的事件流或变更历史条目 | doc-as-usr-interface.md | Stable |
| [LOD](doc-as-usr-interface.md#lod-level-of-detail) | 信息密度的分级控制机制 | doc-as-usr-interface.md | Stable |

---

## 能力来源

| 术语 | 一句话摘要 | 定义位置 | 状态 |
|------|-----------|---------|------|
| [Capability-Provider](app-for-llm.md#capability-provider) | 通过 DocUI 向 LLM 提供能力的实体的统称 | app-for-llm.md | Stable |
| [Built-in](app-for-llm.md#built-in) | Agent 内建功能，与 Agent 生命周期绑定，进程内直接调用 | app-for-llm.md | Stable |
| [App-For-LLM](app-for-llm.md#app-for-llm) | 外部扩展机制，独立进程通过 RPC 与 Agent 通信 | app-for-llm.md | Stable |

---

## UI-Anchor 体系

| 术语 | 一句话摘要 | 定义位置 | 状态 |
|------|-----------|---------|------|
| [UI-Anchor](UI-Anchor.md#ui-anchor) | 为 LLM 提供引用和操作 DocUI 中可见元素的可靠锚点 | UI-Anchor.md | Draft |
| [Object-Anchor](UI-Anchor.md#object-anchor) | 标识界面中的实体对象（名词），语法 `[Label](obj:type:id)` | UI-Anchor.md | Draft |
| [Action-Prototype](UI-Anchor.md#action-prototype) | 以函数原型形式披露操作接口，将 UI 转化为 Live API Documentation | UI-Anchor.md | Draft |
| [Action-Link](UI-Anchor.md#action-link) | 预填充参数的快捷操作链接，相当于 GUI 中的 Button | UI-Anchor.md | Draft |
| [AnchorTable](UI-Anchor.md#anchorid-结构) | 锚点 ID 到实体的映射表，每次 Context-Projection 重建 | UI-Anchor.md | Draft |
| [Micro-Wizard](micro-wizard.md#micro-wizard) | 轻量级多步骤交互模式，帮助 LLM 渐进解决局部复杂性 | micro-wizard.md | Draft |
| [Cursor-And-Selection](cursor-and-selection.md#cursor-and-selection) | 向 LLM 展示光标位置和选区范围的机制 | cursor-and-selection.md | Draft |
| [Selection-Marker](cursor-and-selection.md#selection-marker) | 代码围栏内的内联标记，标识选区起止位置 | cursor-and-selection.md | Draft |
| [Selection-Legend](cursor-and-selection.md#selection-legend) | 代码围栏外的图例说明，解释各选区标记的语义 | cursor-and-selection.md | Draft |

---

## 弃用术语

| 旧术语 | 替代术语 | 说明 |
|--------|---------|------|
| ~~Render~~ | Context-Projection | 过于宽泛，易与前端渲染混淆 |
| ~~Human-User~~ | — | Agent 通过 Agent-OS 与 Environment 交互，不是一对一问答 |
| ~~To-User-Response~~ | — | LLM 向他人说话需要通过 Tool-Call |
| ~~History~~ | Agent-History | 需要完整术语以区分上下文 |

---

## 术语使用规则

1. **简称约束**: "App" 简称**仅指 App-For-LLM**（外部扩展），不包括 Built-in
2. **统称使用**: 需要同时指代 Built-in 和 App-For-LLM 时，使用 "Capability-Provider"
3. **连字符风格**: 复合术语使用连字符连接，如 Context-Projection、Capability-Provider、App-For-LLM
4. **弃用标记**: 已弃用术语使用 ~~删除线~~ 标记，并指向替代术语

---

*本文件格式于 2025-12-14 术语治理架构研讨会后重构为索引格式*
*UI-Anchor 术语于 2025-12-14 UI-Anchor 研讨会后添加*
*Cursor-And-Selection 术语于 2025-12-15 添加*
