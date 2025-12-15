# Cursor-And-Selection

## Cursor-And-Selection

> **Cursor-And-Selection** 是 DocUI 中向 LLM 展示光标位置和选区范围的机制。通过在代码围栏内插入选区标记（Overlay 元素），LLM 无需复述目标文本即可精确指定编辑位置。

---

## 动机

光标和选区在传统 TUI/GUI 中具有关键作用，特别是文本编辑类场景。然而现有的典型 Agent 环境并未向 LLM 提供同类型能力，更多依赖模型精确和完整地复述目标文本。

**痛点**：
- 复述长文本消耗大量 token
- 复述过程容易出错（遗漏空格、换行等）
- 无法表达"光标在此处"这样的位置信息

**本机制的价值**：在不向 tokenizer 引入新 token 的情况下，向 LLM 展示光标和选区。

---

## 核心概念

### Selection-Marker

> **Selection-Marker** 是插入到代码围栏内的内联标记，用于标识选区的起止位置。

**语法**: `<sel:N>...</sel:N>`（N 为序号，用于区分多个选区）

**示例**:
```csharp
class MyClass {
    <sel:1>public const</sel:1> string DefaultName = "some-name";
}
```

### Selection-Legend

> **Selection-Legend** 是代码围栏外的图例说明，解释各选区标记的语义。

**示例**:
```markdown
Legend of content:
  - `<sel:1>`: 即将被替换掉的 old text
  - `<sel:2>`: 即将被替换成的 new text
```

---

## 完整示例

以下展示了 str_replace 操作的预览界面：

```markdown
## TextEditor
  - file: `/example/SomeCode.cs`

Legend of content:
  - `<sel:1>`: 即将被替换掉的 old text
  - `<sel:2>`: 即将被替换成的 new text

Content:
` ` `csharp
class MyClass {
    <sel:1>public const</sel:1><sel:2>private static readonly</sel:2> string DefaultName = "some-name";
    public const int DefaultValue = 42;
}
` ` `

请选择下一步行动：
  - [确认执行文本替换](link:7 "commit_str_replace(context:0x1234ABCD)")
  - [放弃文本替换](link:8 "cancel(context:0x1234ABCD)")
  - [选取下一匹配](link:9 "select_next_match(context:0x1234ABCD)")
```

**注**：此示例同时展示了与 [UI-Anchor](UI-Anchor.md) 的协作——Action-Link 提供了操作入口。

---

## 实现要点

### 避免标记碰撞

1. **序号区分**：使用 `<sel:1>`, `<sel:2>` 等序号避免多选区混淆
2. **样式变化**：可选用不同样式（如 `<cursor>`, `<highlight:N>`）表达不同语义
3. **围栏符号检测**：包裹代码围栏前，先统计最大连续 `` ` `` 长度，避免符号碰撞

### 与 Micro-Wizard 的协同

Selection-Marker 是 [Micro-Wizard](micro-wizard.md) 的典型渲染输出：

| Wizard 场景 | Selection-Marker 用法 |
|-------------|----------------------|
| str_replace 多匹配 | 标记各候选匹配位置 |
| 提取方法 | 标记将被提取的代码范围 |
| 重命名符号 | 标记所有将被修改的引用位置 |

---

## 与 UI-Anchor 的关系

| UI-Anchor 概念 | Cursor-And-Selection 对应 |
|----------------|--------------------------|
| Object-Anchor | Selection-Marker（内联形式的位置锚点） |
| Action-Link | 确认/取消/下一匹配等操作链接 |
| AnchorTable | 选区上下文（如 `context:0x1234ABCD`） |

**区别**：
- Object-Anchor 标识**实体**（名词）
- Selection-Marker 标识**位置/范围**（在文本流中的区间）

---

## 扩展：Cursor 表示

除了选区，还可以表示光标位置：

```csharp
class MyClass {
    public const string DefaultName = "<cursor/>some-name";
}
```

**语义**：`<cursor/>` 表示光标所在的插入点（零宽度位置）。

---

## 参考

- [UI-Anchor](UI-Anchor.md) — 锚点机制
- [Micro-Wizard](micro-wizard.md) — 多步骤交互模式

---

*本文档创建于 2025-12-15*

---

## 实现备注

### 与 PieceTreeSharp 的关联

PieceTreeSharp 项目已实现了完整的 Cursor/Selection 和 Decorations 系统：

- `CursorState` / `SingleCursorState` — 光标状态管理
- `Selection` / `Range` — 选区表示
- `ModelDecorationOptions` — 装饰器配置（可用于渲染高亮）

在实现 DocUI 的 Selection-Marker 渲染时，可以直接复用这些数据结构，将 PieceTreeSharp 的内部表示转换为 Markdown 内联标记。

**转换流程**：
```
PieceTreeSharp Selection → DocUI Selection-Marker → Markdown 输出
```

这意味着 TextEditor 类型的 App-For-LLM 可以原生支持 Cursor-And-Selection 展示。
