# Micro-Wizard

## Micro-Wizard

> **Micro-Wizard**（微向导）是一种在 LLM 驱动的智能体中使用的轻量级、多步骤交互模式。它通过在局部引入动态的、上下文感知的引导步骤，帮助 LLM 渐进解决局部复杂性，并在完成交互后自动修剪中间状态，以保持上下文的简洁和有效性。

---

## 典型场景

以文本编辑工具 `str_replace(oldText, newText)` 为例：

**传统行为**：遇到多匹配时简单报错了事。

**Micro-Wizard 强化后**：

1. 检测到 `oldText` 有 3 个匹配
2. 返回引导性 Observation：
   ```
   oldText 有 3 个匹配，分别是：
   1. [第23行](obj:match:1): "function foo() {...}"
   2. [第45行](obj:match:2): "function foo(x) {...}"  
   3. [第78行](obj:match:3): "const foo = () => {...}"
   
   请选择你想替换的选区序号（1-3），或输入 "all" 全部替换。
   ```
3. LLM 只需输出一个序号（如 `2`），无需再次复述大段原始文本
4. 编辑完成后，中间对话自动折叠，只保留最终结果和简要印象

---

## Wizard Trigger Protocol

Micro-Wizard 的触发分为两类：

### Error Recovery（错误恢复）

当 Action 调用遇到问题时自动触发：

| 触发条件 | Wizard 行为 |
|----------|------------|
| 参数缺失 | 提示补全必需参数 |
| 类型不匹配 | 提示正确的类型/格式 |
| 多匹配歧义 | 列出候选项供选择 |
| 权限不足 | 提示所需权限或替代方案 |

**交互流**：
1. LLM 调用 `attack(target='slime')` — 歧义，有多个史莱姆
2. Agent-OS 捕获歧义，返回 `WizardView` Observation
3. DocUI 渲染选择列表（带 Object-Anchor）
4. LLM 选择具体 ID
5. Wizard 完成，中间对话折叠

### Deliberate Confirmation（刻意确认）

对高危操作**强制**触发确认，作为 UX 中的"减速带"：

| 触发条件 | Wizard 行为 |
|----------|------------|
| 不可逆操作（删除、覆盖） | 要求确认，显示影响范围 |
| 批量操作 | 显示将受影响的条目列表 |
| 敏感操作（发布、提交） | 要求最终确认 |

**示例 Action-Link**：
```markdown
[执行批量删除...](link:99 "batch_delete(items=selected)" confirm=wizard)
```

此链接即使参数完备，也会触发 Wizard 要求确认。

---

## 核心特性

### 自动修剪（Auto-Pruning）

Wizard 完成后，中间对话从 Agent-History 中"折叠"：

- **保留**：最终成功的 Action 记录
- **折叠**：中间的引导步骤和选择过程
- **留痕**：简要印象（如"经过 1 轮选择确定了目标"）

这利用了"无后效性"原则——中间步骤对后续推理没有价值，应释放 token 预算。

### 软着陆反馈

Wizard 的错误信息应包含 **Recovery Affordance**（恢复示能）：

| 错误类型 | 反馈示例 |
|----------|---------|
| ❌ 死胡同 | `Error: Multiple matches found.` |
| ✅ 可恢复 | `Error: Multiple matches found. [Select from list](wizard:select_match)` |

---

## 与 UI-Anchor 的协同

Micro-Wizard 与 [UI-Anchor](UI-Anchor.md) 紧密协作：

| UI-Anchor 概念 | Wizard 中的应用 |
|----------------|----------------|
| Object-Anchor | Wizard 选项列表中的每个候选项 |
| Action-Link | "确认"/"取消"等 Wizard 操作按钮 |
| Action-Prototype | Wizard 完成后执行的目标动作 |

---

## 实现要点

1. **Wizard 是一种特殊的 Observation 结构**，Agent-OS 识别后进入"等待用户选择"状态
2. **Wizard 结果直接绑定原 Action**，无需 LLM 重新构造完整调用
3. **折叠策略在 Context-Projection 阶段执行**，而非立即修改 History

---

## 参考

- [UI-Anchor](UI-Anchor.md) — 锚点机制
- [研讨会记录](../../../agent-team/meeting/2025-12-14-ui-anchor-workshop.md) — Wizard Trigger Protocol 讨论

---

*本文档基于 2025-12-14 UI-Anchor 研讨会共识修订*
