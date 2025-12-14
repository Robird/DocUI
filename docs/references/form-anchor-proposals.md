# Form-Anchor 语法演进提案

> **状态**: Proposal / Draft
> **关联文档**: [UI-Anchor.md](UI-Anchor.md)
> **动机**: 现有的行内链接语法 `[Label](form:id "sig")` 在表达复杂操作时存在自明性不足（缺乏参数说明）和冗余（Label 与 Function Name 重复）的问题。

---

## 问题分析

以“火球术”为例，当前语法的局限性：

```markdown
[火球术](form:fireball "cast_fireball(target: Anchor<Obj>, mana: int = 10)")
```

1.  **信息压缩过高**: 参数说明（如 "target 是什么"）只能塞进 `title` 属性，LLM 阅读困难，且容易被截断。
2.  **视觉层级弱**: 作为一个行内元素，它在视觉上不像一个“可操作的表单”，容易淹没在文本中。
3.  **冗余**: `[火球术]` (UI Label) 和 `cast_fireball` (Function Name) 需要重复定义。

---

## 提案 A: 块级定义 (The "Doc-Form" Pattern)

**核心思想**: 放弃行内元素，利用 Markdown 的 **引用块 (Blockquote)** 或 **列表 (List)** 组合出表单的视觉结构。

### 语法示例

```markdown
> **Action: Cast Fireball** (`id: spell_01`)
> *Hurls a fiery ball that causes 50 Fire damage.*
>
> - **target**: `Anchor<Obj>` — The enemy unit to hit.
> - **mana**: `int` (Default: 10) — Mana cost.
>
> [🚀 EXECUTE](trigger:spell_01)
```

### 优势
1.  **Markdown Native**: 完全使用标准 Markdown 语法，渲染器无需特殊支持即可显示良好的层级。
2.  **自明性强**: 有足够的空间撰写 Description 和 Parameter Doc。
3.  **视觉显著**: 引用块天然形成了 UI 上的“卡片”或“区域”感。

## 提案 B: 函数原型风格 (The "Prototype" Pattern)

**核心思想**: 利用 LLM 对代码和 API 文档的极致敏感度，直接以**函数原型 (Function Prototype)** 的形式展示表单。

### 语法示例

```markdown
### [Action] Cast Fireball
> **ID**: `spell_01`

```typescript
/**
 * Hurls a fiery ball that causes 50 Fire damage.
 */
function cast_fireball(
    target: Anchor<Obj>, // The enemy unit to hit
    mana: int = 10       // Mana cost
): void;
```
```

### 优势
1.  **LLM Native**: 代码是 LLM 的母语。相比于 HTML 或自然语言，函数签名提供了最高的信噪比。
2.  **零歧义**: 类型系统（如 `Anchor<Obj>`）、默认值、参数名一目了然。
3.  **Intent Alignment**: LLM 的 Tool-Use 本质就是函数调用。看到原型，它能立即构建出 `call`，跳过了"阅读理解"步骤。
4.  **标准复用**: 直接复用 JSDoc/TSDoc 标准来承载 Description 和 Help Text。

### 劣势
1.  **非技术用户门槛**: 如果有人类观察者，这种界面显得过于"硬核"。但在 DocUI 场景下，LLM 才是第一用户。

---

## 建议方向

推荐采用 **混合策略**：

1.  **简单操作 (Button)**: 继续使用行内链接 `[Label](form:id)`。
2.  **复杂操作 (Form)**: 采用 **提案 B (函数原型风格)**。这实际上是将 DocUI 变成了一个 **Live API Documentation**。
