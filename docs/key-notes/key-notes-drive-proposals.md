# Key-Notes Drive Proposals

Key-Notes 相当于"宪法与关键帧"：定义 DocUI 设计的关键轮廓，用于指导 Proposals 的撰写方向。

- **定位**: 定义核心概念边界、命名与包含关系，约束后续设计演进。
- **写作主体**: 以人类为主；AI 可以辅助润色、对齐术语与一致性审计，但应避免替代核心决策。
- **文档风格**: 宁可短，但要精确；宁可给出指针，也不要在多处重复定义。

---

## 术语治理规则

本章节规定 Key-Notes 与 Proposals 的术语治理方式。

### Primary Definition 原则

每个核心术语在**首次引入它的 Key-Note** 中定义。这是该术语的权威、完整定义。

**定义块格式**（必须遵守）：
- 使用 \`## Term\` 标题（H2 或 H3，确保可寻址）
- 标题下第一段使用引用块 \`> **Term** ...\` 给出一句话定义
- 定义块之后可以展开动机、示例、实现映射等解释性内容

**示例**：
\`\`\`markdown
## Agent

> **Agent** 是能感知环境、为达成目标而行动、并承担行动后果的计算实体。

Agent 系统有内部和外部两个状态转移函数...
\`\`\`

### Glossary-as-Index 原则

[glossary.md](glossary.md) 是术语的**索引**而非定义存放地。

**glossary 只记录**：
- 术语名称（作为链接）
- 一句话摘要（直接复制定义块内容）
- 定义位置链接
- 状态（Stable / Draft / Deprecated）

### Restatement 规则

非 Primary Definition 文档中**允许重述**术语含义，但：
- 必须显式标注为"重述"或使用"参见"链接
- 不得改变术语的定义边界
- 重述仅用于帮助阅读，不作为权威来源

**示例**：
> Agent（参见 [llm-agent-context.md#agent](llm-agent-context.md#agent)）是能感知环境并行动的计算实体。本文讨论 Agent 与 App 的交互...

### 引入新术语的流程

1. 在**引入该术语的 Key-Note** 中创建 Primary Definition（含定义块）
2. 在 glossary.md 添加一行索引
3. 如有命名竞争，在定义块后记录"曾用名 / 别名"

### 迁移规则

- **改名**: 旧术语在 glossary 标记为 Deprecated，指向新术语；旧名永不复用
- **迁居**: 原位置保留 Redirect Stub（\`## OldTerm\` + "Moved to ..."），不破坏既有链接

### 命名与简称约束

- 复合术语使用连字符风格（示例：Context-Projection、Capability-Provider、App-For-LLM）
- 简称规则以 glossary 为准；任何偏离都必须在 Proposal 中显式声明并给出迁移策略

---

## CI 校验规格（待实施）

以下规则可由自动化工具校验：

1. **索引校验**: glossary 表格里的每个链接都可达、锚点唯一
2. **唯一性校验**: 同一个术语名的定义块只能出现在一个文件的一个标题下
3. **摘要一致性**: glossary 摘要与 Primary Definition 的定义块一致（文本等价）
4. **状态校验**: Draft/Deprecated 不能被标注为"稳定依赖"

---

*术语治理规则于 2025-12-14 术语治理架构研讨会后重写*
