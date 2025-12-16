# Doc as User Interface

## DocUI

> **DocUI** 是一个 LLM-Native 的用户界面框架，它将 Markdown 文档作为 LLM 感知系统状态的界面（Window），并将工具调用（Tool-Call）作为 LLM 操作系统的手段。

LLM 是 Agent-OS 的用户，LLM 从 Agent-OS 获取信息和进行操作的界面就是 DocUI。因将展现给 LLM 的信息渲染为 Markdown Document 而命名。

---

## DocUI 与 GUI/TUI/API 的区别与联系

在渲染形式上，DocUI 与 TUI 和 Web 服务器最为近似，只是 DocUI 渲染出的信息以 Markdown 文档为基础，并按需进行扩展。选择 Markdown 的原因在于 LLM 对此熟悉，语法噪声也较少。xml 和 html 语法噪音较多，json 转义序列问题更突出。asciidoc 也入围，但没有 markdown 语料多。

---

## DocUI 注入 LLM 上下文的形式

DocUI 主要通过 2 种形式注入 LLM 上下文中：Window 和 Notification。Window 呈现实况状态。Notification 呈现事件历史。

### Window / Notification / History-View 的层级关系（澄清）

本节先解决"有无问题"：明确三者的包含关系与职责边界，细节策略（如选取算法、精确 LOD 规则）可后续迭代。

\`\`\`mermaid
flowchart TB
  OBS["Observation<br/>(Agent-OS → LLM)"] --> HV["History-View<br/>(呈现给 LLM 的历史视图渲染产物)"]

  HV --> W["Window<br/>(状态快照 / 实况视图)"]
  HV --> N["Notification*<br/>(事件/变更条目流)"]

  subgraph Source["内部状态来源（不直接等同于注入内容）"]
    H["History<br/>(仅追加、不可变)"] --> HE["HistoryEntry<br/>(完整交互记录)"]
  end

  HE -.->|"Recent History 选取策略"| N

  Note1["Notification* 可能是多条<br/>取决于 Recent History 窗口与预算"]
  N -.-> Note1
\`\`\`

**术语说明**（restatement，详见 [llm-agent-context.md](llm-agent-context.md)）:
- **Observation**: Agent-OS 发送给 LLM 的 Message
- **History-View**: 面向"展示给 LLM"的渲染产物集合，由 Context-Projection 生成
- **Recent History**: 一种选取策略/时间窗，不是一种渲染形态

---

## Window

> **Window** 是呈现给 LLM 的当前系统状态的快照视图（Snapshot），通常渲染为 Markdown 文档，并根据注意力焦点应用不同的详细等级（LOD）。

在 Context-Projection 过程中，将各种需要向 LLM 展现的实况信息渲染为一份 Markdown 文档，作为最新的一条 Observation 的正文的一部分。在呈现给 LLM 的信息中只有最新的一份，而不在 IHistoryMessage 层中展示所有历史快照，但不排除在 HistoryEntry 层为了调试、存档或时间旅行目的而保存不可变快照。

Window 中的信息有 {Full, Summary, Gist} 三个 LOD 级别：
- **Gist** 级别保留最基本的"What"和"关键线索"信息，意在最小化 Token 占用并保留提供一个提高 LOD 级别来恢复认知的入口。
- **Summary** 级别是最常用的主要级别，是信息实用性和 Token 占用的甜点级别。展示当前节点及所有子节点的概述、重要子节点链接、重要相关节点链接、所有子节点列表链接。
- **Full** 则展现所有原始信息。

---

## Notification

> **Notification** 是呈现给 LLM 的事件流或变更历史条目，用于维持认知的连续性，通常根据时间远近应用不同的详细等级（LOD）。

Recent History 为 LLM 提供了近期的认知与思路连续性，过程性信息的各个历史动态则保存入 HistoryEntry 中，有 {Basic, Detail} 两档 LOD。

根据上下文窗口预算，较新的 HistoryEntry 渲染时取 Detail 级别，较老的 Entry 取 Basic 级别。

Agent 系统收到外部其他人或 Agent 发来的信息，就建模为一条 Notification，带有对方的关系标识和通讯渠道标识，取代 Chat 范式下唯一的直接交互用户。

时间戳也是一条 Notification，为节约 Token 数可能以较低的频率产生，进而使得并非每条 Observation 都有时间戳。

---

## LOD (Level of Detail)

> **LOD (Level of Detail)** 是信息密度的分级控制机制，用于在有限的 Context Window 预算下，平衡信息的广度与深度。

DocUI 中主要包含以下 LOD 级别：
- **Window LOD**: {Full, Summary, Gist}
- **Notification LOD**: {Detail, Basic}

---

## LOD 自动管理：候选机制（开放研究）

> **状态**: 开放问题，以下为候选机制，尚未正式采纳。

### 候选机制 1：时效性管理（FIFO/LRU）

将 LOD 展开状态视为一种资源，按时效性管理：

- **FIFO**（最简单，适合 MVP）：展开到高级别的信息具有时效性，先展开的先折叠
- **LRU**：按最近使用时间管理，长时间未访问的节点自动降级

**触发条件**：总上下文长度超过阈值时，自动折叠最低活跃度或详细状态持续时间最久的节点。

**配套机制**：
- Pin 功能：允许锁定某些节点的 LOD 级别
- 手动展开/折叠 Action

**优点**：无需显式 Focus 概念，实现简单
**适用场景**：MVP 阶段

### 候选机制 2：依赖关系自动关联展开

建模信息节点之间的链接/依赖关系：

- 当展开一个信息节点时，自动展开其依赖的其他节点到适当 LOD 级别
- 类似 IDE 的"转到定义"后自动展开相关上下文

**示例**：
- 展开一个函数定义 → 自动展开其调用的其他函数签名（Summary 级别）
- 展开一个错误日志 → 自动展开相关文件的上下文

**优点**：语义驱动，更智能
**挑战**：需要维护依赖图，实现复杂度较高

### 候选机制 3：Attention 归因热力图（远期）

利用本地开源模型的 self-attention 层信号：

- 生成归因热力图，识别 LLM 实际关注的信息区域
- 自动展开高关注区域，折叠低关注区域

**状态**：非常早期的思路，暂不具备实施条件
**价值**：真正的"LLM-Native"LOD 管理

### MVP 策略建议

**推荐 FIFO 作为 MVP 策略**：
- 实现最简单
- 核心假设：展开到高级别的信息具有时域相关性和时效性
- 后续可迭代升级到 LRU 或依赖关系方案

---


## TODO

- 关于 Window 中的 Summary 级别。考虑平铺渲染各信息节点，树形结构何时 inplace 展开，何时树形结构内保留 Gist 或 Link 而在文档其他地方平铺展开更详细的目标节点内容。
