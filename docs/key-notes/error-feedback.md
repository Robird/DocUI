# Error-Feedback

## Error-Feedback

> **Error-Feedback**（错误反馈）是 DocUI 中引导 LLM 从错误状态恢复的交互模式。核心精神是：给出引导和候选，帮助 LLM 理解错误并解决错误，而非简单报错了事。

---

## 动机

**问题**：传统错误处理对 LLM 不友好
- 简单的错误消息（如 "Target not found"）无法帮助 LLM 理解如何恢复
- LLM 可能陷入重试死循环，或产生幻觉填补信息空白
- 缺乏标准化的恢复路径

**核心比喻**：
> 传统错误处理像是"红灯"——停下来。
> DocUI 的错误处理应该像"GPS 重新规划"——告诉你哪里不通，同时给你三条新路线。

**设计原则**：
- **错误是分支点，不是死胡同**——每个错误都提供恢复选项
- **可恢复优先**——尽可能让 LLM 自己解决，而非上报人类
- **教学时刻**——每次错误都是强化学习的正样本

---

## 分层错误响应

根据**严重度 × 恢复复杂度**，错误响应分为三个层次：

| 层次 | 适用场景 | 响应形式 |
|------|----------|----------|
| **Level 0: Hint** | 简单错误，单一恢复路径 | 单行提示 + 自动修正建议 |
| **Level 1: Choice** | 可恢复错误，多个候选路径 | 候选列表 + Action-Link |
| **Level 2: Wizard** | 复杂错误，需要多步引导 | Micro-Wizard 流程 |

```
                      恢复复杂度
                 低            高
              ┌─────────┬─────────┐
         低   │ Level 0 │ Level 1 │   
  严重度      │  Hint   │  Choice │  
              ├─────────┼─────────┤
         高   │ Level 1 │ Level 2 │
              │  Guard  │  Wizard │
              └─────────┴─────────┘
```

---

## 错误响应结构

### 标准格式

```markdown
## ⚠️ 操作未完成

**原因**: 目标 `obj:enemy:23` 已不存在（可能被前序操作 `attack()` 移除）

**建议操作**:
- [刷新当前视图](link:refresh "refresh_context()")
- [查看战斗日志](link:log "show_log(filter='combat')")
- [返回上一步](link:undo "undo()")

**技术详情** (折叠):
> Anchor resolution failed at step 2 of 3. Previous action `attack(obj:enemy:23)` 
> returned success with side effect: target eliminated.
```

### 关键元素

| 元素 | 作用 | 示例 |
|------|------|------|
| **情绪标记** | 帮助 LLM 校准注意力 | 😅 小问题 / 🤔 需澄清 / ⚠️ 请注意 / 🚨 严重 |
| **原因说明** | 解释为什么出错 | "目标已不存在（可能被前序操作移除）" |
| **因果链** | 状态变更的时间线 | "T-1: attack() 击杀了目标" |
| **恢复选项** | Action-Link 列表 | `[刷新视图](link:1 "refresh()")` |
| **热修复代码** | 可直接执行的修复片段 | `attack(target="obj:enemy:45")` |
| **技术详情** | 调试信息（可折叠） | 错误码、堆栈、上下文 |

### 情绪调性 (Tone)

错误信息应有情绪色彩，作为 LLM 的语义信号：

```markdown
## 😅 小问题 —— 参数格式不对
你写的 `count="five"` 应该是数字。[用 5 替换](link:1 "fix_param(value=5)")

## 🤔 需要澄清 —— 找到多个匹配
你说的 "config" 可能是指：[config.json](obj:file:1) 或 [config.yaml](obj:file:2)

## ⚠️ 请注意 —— 这个操作不可逆
你正要删除 47 个文件。[查看列表](link:1) | [确认删除](link:2 "confirm()")

## 🚨 出大事了 —— 系统状态异常
数据库连接已断开。[检查历史](link:1) → [重新连接](link:2)
```

---

## 特殊场景处理

### 锚点失效

当 LLM 引用的锚点已失效时（常见于脚本式执行）：

```markdown
## ⚠️ 目标已变更

**原因**: `obj:enemy:23` 在你发出指令时已不存在

**因果分析**:
```
T-2: look() ───> 👁️ 看到 slime_23 (HP: 1)
T-1: attack() ──> ⚔️ 队友击杀了 slime_23  
T-0: 你的操作 ──> ❌ 目标不存在
```

**建议操作**:
- [刷新视图获取最新状态](link:1 "refresh_context()")
- [选择新目标](link:2 "select_target()")
```

### 参数歧义

当参数匹配多个候选时：

```markdown
## 🤔 需要澄清

你调用了 `attack(target='slime')`，但场上有 3 个史莱姆。

**💡 小贴士**：下次可以用 Object-Anchor 精确指定：`attack(target='obj:enemy:23')`

**请选择目标**:
1. [绿色史莱姆](obj:enemy:23) HP: 10/10
2. [蓝色史莱姆](obj:enemy:45) HP: 8/10 ← 最弱
3. [金色史莱姆](obj:enemy:67) HP: 15/15 ⭐ 稀有
```

### 连续失败熔断

如果 LLM 连续在同一处失败 3 次以上：

```markdown
## 🛑 建议暂停

你已在此操作上连续失败 3 次。

**可能的原因**:
- 前置条件未满足
- 系统状态异常
- 需要人工干预

**建议**:
- [转向其他任务](link:1 "switch_task()")
- [请求人工协助](link:2 "request_help()")
- [查看完整日志](link:3 "show_full_log()")
```

---

## 与 Micro-Wizard 的整合

Error-Feedback 与 [Micro-Wizard](micro-wizard.md) 紧密协作：

| Error-Feedback 层次 | Micro-Wizard 角色 |
|---------------------|-------------------|
| Level 0 (Hint) | 不触发 Wizard |
| Level 1 (Choice) | 可选触发（如需要多选） |
| Level 2 (Wizard) | 必定触发完整 Wizard 流程 |

**关键特性**：Wizard 完成后，中间错误处理过程会被折叠，只保留最终结果和简要印象。这意味着**不怕错误响应文本详细**——详细的引导不会永久占用 token 预算。

---

## 实现指南

### 工具返回值扩展

扩展 `ToolExecutionStatus`：

```csharp
public enum ToolExecutionStatus {
    Success,
    Failed,
    NeedsClarification,    // 需要 LLM 澄清
    NeedsRecoveryChoice,   // 需要 LLM 选择恢复路径
}
```

### 声明式 Wizard 规格

推荐声明式而非迭代器，便于序列化和预览：

```csharp
var wizard = new WizardSpec {
    Tone = WizardTone.Clarifying,
    Steps = [
        new InfoStep("目标已不存在"),
        new ChoiceStep("请选择下一步", [
            new Option("刷新视图", "refresh_context()"),
            new Option("查看日志", "show_log()"),
            new Option("放弃", null)
        ])
    ],
    FallbackAction = "refresh_context()"  // 兜底
};
```

### JSON 序列化格式

```json
{
  "status": "needs_recovery",
  "error": {
    "code": "ANCHOR_NOT_FOUND",
    "message": "目标 obj:enemy:23 已不存在",
    "cause": "可能被前序操作移除"
  },
  "recovery": {
    "tone": "clarifying",
    "prompt": "请选择下一步",
    "options": [
      { "label": "刷新视图", "action_hint": "refresh_context()", "confidence": "high" },
      { "label": "查看日志", "action_hint": "show_log()", "confidence": "medium" },
      { "label": "放弃", "action_hint": null, "confidence": "low" }
    ],
    "default_option": 0
  }
}
```

---

## 反模式

| 反模式 | 问题 | 正确做法 |
|--------|------|----------|
| **谜之沉默** | 返回空/通用错误，LLM 会产生幻觉 | 明确说明原因和恢复路径 |
| **指责语气** | "你错了"让 LLM 困惑 | 保持中立："无法处理" |
| **格式不一致** | 每次报错格式不同 | 统一结构，让 LLM 形成预期 |
| **无恢复路径** | 只说"失败了" | 必须提供至少一个恢复选项 |
| **信息过载** | 一次展示所有技术细节 | 分层：摘要 + 可折叠详情 |

---

## 参考

- [Micro-Wizard](micro-wizard.md) — 多步骤交互模式
- [UI-Anchor](UI-Anchor.md) — 锚点机制
- 畅谈记录：`agent-team/meeting/2025-12-15-error-feedback-jam.md`

---

*本文档基于 2025-12-15 秘密基地畅谈共识创建*

