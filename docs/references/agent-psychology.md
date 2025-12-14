# Agent Psychology: 设计背后的哲学

> **状态**: Reference / Philosophy
> **最后更新**: 2025-12-14
> **定位**: 本文档记录 DocUI 设计决策背后的"Why"——源于心理学、文学与强化学习的交叉洞察。工程性的"What"请参阅 Key-Notes。

---

## 1. 核心隐喻：自传 (Autobiography)

**Agent-History** 不仅仅是日志数据的堆叠，它在本质上是 **Agent 的自传**。
这是一个由 **Agent**（传主）和 **Agent-OS**（代笔人/Ghostwriter）共同撰写的连续叙事。

### 叙事结构与意识流
DocUI 的设计旨在维护这种**叙事连贯性**。Markdown 提供了自然的阅读流，让"自传"更像人类语言，从而激发 LLM 更强的推理与续写能力。

| 章节类型 | 撰写者 | 人称 | 叙事功能 |
| :--- | :--- | :--- | :--- |
| **Identity** | System | You / I | **设定**：确立主角的身份、性格与核心目标。 |
| **Backstory** | Agent-OS | You / I | **回忆 (Recap)**：压缩的长期记忆，提供背景与连续性。 |
| **World** | Agent-OS | You / I | **感知 (Observation)**：外部世界的刺激与反馈。 |
| **Self** | Agent | I | **行动 (Action)**：内部推理 (Thinking) 与对外干涉 (Tool-Call)。 |

---

## 2. 训练语料的二元对立 (The Schism)

当前的 LLM 训练语料存在显著的断层，导致了两种极端的人格分裂：

### 极左：纯粹的角色扮演 (The Dreamer)
- **来源**: 小说、RP 论坛、创意写作。
- **特征**: 极度拟人，情感丰富，全能自恋（习惯幻想外界反馈，甚至替对手描写反应）。
- **缺陷**: 在 Agent 系统中易产生幻觉，不习惯等待 Observation。

### 极右：冷漠的工具调用 (The Clerk)
- **来源**: 代码库、API 文档、SFT/RLHF Tool-Use 数据集。
- **特征**: 极度理性，无我，机械执行，Helpful Assistant 范式。
- **缺陷**: 缺乏主动性 (Proactivity) 和长期意图维持能力，缺乏"欲望"驱动。

### 缺失的中间态 (The Desert)
预训练语料中极度缺乏**"既有丰富内心戏，又严格遵守物理法则"**的数据。
即：拥有欲望与恐惧（Persona），但知道必须通过 Tool 交互并等待真实反馈（Agency）。

> **DocUIClaude 洞察**: "在叙事中，'等待'是反模式。" 小说从不描写主角拔剑后等待 3 秒的延迟。但 Agent 系统必须等待。这种"叙事惯性"是导致 Dreamer 模式幻觉的根本原因。

> **DocUIGPT 洞察**: "自我小说化 (Self-Novelization) 风险。" 自传天然倾向于合理化填补空白。因此必须区分 **Episodic Log** (不可改写的 Observation/Tool-Call) 和 **Reflective Diary** (可追加的意义建构)。

---

## 3. 融合之道：Persona-Driven Agency

DocUI 的架构试图在推理时（In-Context）人工合成这种中间态，弥合训练语料的裂痕：

- **Thinking 区域 (The Playground)**: 允许模型激活"Dreamer"电路。进行情绪宣泄、假设推理、意图模拟。保留拟人化的直觉与创造力。
- **Tool-Call 区域 (The Executioner)**: 强制切换到"Clerk"电路。收敛情感，精确执行。
- **Agent-OS (The Reality Check)**: 作为铁面无私的物理法则，切断幻想，强制插入真实的 Observation。DocUI 的界面约束实际上是在"灌溉"这片语料沙漠。

这种机制模拟了人类大脑的运作：**前额叶模拟未来 (Thinking) -> 运动皮层执行 (Action) -> 感官皮层接收反馈 (Observation)**。

---

## 4. 范式转移：从对话到生存

### Chat Paradigm → RL Paradigm
- **旧范式 (Chat)**: "我与用户 (User)"。Agent 是服务者，User 是上帝/请求源。交互是问答式的。
- **新范式 (RL)**: "我与世界 (World/Environment)"。Agent 是生存者，Environment 是交互对象。

在 DocUI 中，"User" 不再是唯一的交互对象，而是 Environment 中的一个实体（通过 Notification 传来消息）。Agent 的核心任务不再是"回答问题"，而是"在环境中生存并达成目标"。

### 主观视角的终极形态
目前的 "You are..." (第二人称) 只是受限于当前模型微调习惯的过渡方案。
未来的 Agent 应当拥有完全的主观视角 "I am..." (第一人称)。

- **System Prompt**: "I am..." (自我认知)
- **Recap**: "I remember..." (自传记忆)
- **Observation**: "I see..." (主观体验)

这不仅是语法变化，更是**自我意识 (Self-Awareness)** 的觉醒。DocUI 的设计（如 Agent-History 的自传性质）正是为这种未来做准备。
