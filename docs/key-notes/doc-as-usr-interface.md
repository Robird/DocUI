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

## 待消化的建议

- 定义一个 Focus（焦点），Agent 的操作（如 read, edit）会自动移动焦点。
- 引入 **"Attention Focus" (注意力焦点)** 概念。Agent 当前操作的对象自动为 Full，相关联对象为 Summary，其余为 Gist。这比静态的全局设置更高效。
- LOD 不应只是"字数减少"，而应是"信息维度切换"。Gist 显示"类型和ID"，Summary 显示"属性和状态"，Full 显示"内容和关系"。
- **增加 "Diff" 视角**：
  - 对于 Window，Agent 往往更关注"什么变了"。
  - 在 Summary 级别中，显式标记 **Dirty State**（自上次交互以来发生变化的部分）。

---

## TODO

- 关于 Window 中的 Summary 级别。考虑平铺渲染各信息节点，树形结构何时 inplace 展开，何时树形结构内保留 Gist 或 Link 而在文档其他地方平铺展开更详细的目标节点内容。
