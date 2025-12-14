# UI-Anchor

## UI-Anchor

> **UI-Anchor** 为 LLM 提供引用和操作 DocUI 中可见元素的可靠锚点。

---

## 动机

DocUI 要完成与 LLM 互动的职能，就需要向 LLM 提供引用 UI 中元素的锚点，实现"句柄"/"ID"的职能。

UI-Anchor 很有用，比如：
- 让 LLM 可以无需复述文本就能实现大段的复制粘贴、调整段落顺序
- 让 LLM 可以操作难以复述的信息，比如多模态上下文中的音频和图像
- 让 LLM 可以快速执行具有上下文和预绑定参数的操作

---

## 核心概念

### Object-Anchor

> **Object-Anchor** 用于标识界面中的实体对象（名词）。

**语法**: `[Label](obj:<type>:<id>)` 或简写 `[Label](obj:<id>)`

**示例**:
- `[史莱姆1](obj:enemy:23)` — 带类型提示
- `[src/main.ts](obj:file:src/main.ts)` — 文件锚点
- `[78%](obj:state:cpu_load)` — 状态值锚点

**用途**: 作为 Action 的参数（如 `target='obj:enemy:23'`）。

### Action-Prototype

> **Action-Prototype** 以函数原型的形式直接披露操作接口，将 UI 转化为 **Live API Documentation**。

**语法**: Markdown Code Block（TypeScript/C# Signature）

**示例**:
```typescript
/** 物理攻击 */
function attack(target: Anchor<Obj>): void;

/** 魔法攻击 @param mana (Default: 10) */
function cast_fireball(target: Anchor<Obj>, mana: int = 10): void;
```

**用途**: 供 LLM 阅读并编写代码调用。

### Action-Link

> **Action-Link** 是预先填充好参数（或无参）的快捷操作链接，相当于 GUI 中的 Button。

**语法**: `[Label](link:<id> "code_snippet")`

**示例**:
- `[攻击史莱姆1](link:42 "attack(target='obj:enemy:23')")`
- `[逃跑](link:43 "flee()")`

**用途**: 通过 `click(link:42)` 一键执行。

---

## 锚点生命周期

### 设计决策：临时优先（Ephemeral by Default）

锚点采用**临时生存期**策略，与 Context-Projection 阶段绑定：

| 时机 | 行为 |
|------|------|
| Context-Projection | 为可见元素分配短整数 ID，构建 AnchorTable |
| 下一轮 Projection | 旧 AnchorTable 失效，重新分配 |
| LLM 引用锚点 | 查表解引用，校验有效性 |

**理由**：
- 短整数 ID 利于 token 经济性（`obj:23` vs UUID）
- 利用 LLM 的"健忘"特性，避免悬空引用
- 迫使 LLM 关注**当下**状态，减少基于过时记忆的幻觉

### AnchorId 结构

**内部结构**（四元组）:
```
kind + providerId + sessionId + localId
```

**外部呈现**（两种形式）:
- 显示层：`obj:23`（短，利于 token）
- 传输/执行层：`obj:23@e17`（含 epoch，用于校验）

> **类比**：AnchorId 类似 IPv6 地址——有完整形式和压缩形式，内部路由用完整形式，用户界面显示压缩形式。

### 软着陆（Graceful Degradation）

当 LLM 引用失效锚点时，返回**可恢复的错误信息**而非崩溃：

| 情况 | 错误响应 |
|------|---------|
| epoch 不匹配 | "Anchor obj:23 is stale. Please refresh to get current IDs." |
| scope 不匹配 | "Anchor obj:23 is out of view. Navigate or expand to access." |
| 不存在 | "Anchor obj:23 not found in current context." |

---

## 动作执行语义

### 脚本式顺序执行 + Short-Circuit

动作序列采用**脚本执行**心智模型（而非数据库事务）：

```csharp
attack(obj:enemy:23);   // 执行，敌人死亡
loot(obj:enemy:23);     // 执行时重新解引用，发现目标失效 → 报错，后续中断
```

**规则**：
1. 单个 Invocation 是原子执行单元
2. 批量动作按顺序执行
3. 每步"用时解引用"（resolve at invocation time）
4. 前一步失败则后续中断（short-circuit）

**理由**：符合编程直觉（`rm file && cat file` 第二步会失败），避免复杂的事务/快照隔离。

---

## 交互模式

### REPL 范式（愿景）

DocUI 的长期目标是从 JSON Schema Tool-Calling 转向 **REPL (Read-Eval-Print Loop)** 范式：

1. **Read**: LLM 阅读文档中的 Action-Prototype 和 Object-Anchor
2. **Eval**: LLM 编写调用表达式（如 `cast_fireball(target='obj:23')`）
3. **Execute**: Agent-OS 解析并执行

### MVP 务实方案

在 MVP 阶段，采用**渐进式迁移**策略：

#### 方案 A：run_code_snippet 工具（推荐起步）

底层仍用 tool calling，将代码执行封装为一个 tool：

```json
{
  "name": "run_code_snippet",
  "parameters": {
    "code": "attack(target='obj:enemy:23')"
  }
}
```

**优点**：
- 实现简单，不用写解析器
- 回避"提及代码"vs"执行代码"的语法设计问题
- 与现有 LLM API 完全兼容

**缺点**：
- JSON 导致多套一层转义序列
- 多一层间接

#### 方案 B：Expression Tree 作为 IR

当需要真正的 REPL 执行时：

```
Roslyn 解析 → AST → Expression Tree → 执行
```

相当于写一个**简易解释器**：
- 只接受调用表达式（InvocationExpression）
- 白名单校验（只允许注册的 Action）
- 参数类型检查

后续工程阶段可复用前人成果（如 Roslyn Scripting API）优化。

### 渐进式披露（操作风险分级）

| 操作类型 | 交互方式 | 理由 |
|----------|---------|------|
| 高频/低风险（导航、查看） | Action-Link（点击即执行） | 低 Token 消耗 |
| 低频/高风险（删除、重构） | Action-Prototype（编写代码） | 触发思维链，更精确 |

---

## 与 Micro-Wizard 的协同

Action-Link 与 [Micro-Wizard](micro-wizard.md) 形成互补：

| 情况 | 行为 |
|------|------|
| 参数完备 | 直接执行 |
| 参数缺失 | 触发 Micro-Wizard 补全 |
| 高危操作 | 触发 Micro-Wizard 确认（Deliberate Confirmation） |

---

## 场景示例

### 场景 1: 虚拟世界 (MUD/RPG)

```docui
## 敌人列表
| Name | Level | HP | State |
|------|-------|-----|-------|
| [史莱姆1](obj:enemy:23) | 1 | 11 | 正在舔舐伤口 |
| [史莱姆2](obj:enemy:24) | 1 | 15 | 惊恐的看着你 |
| [强盗](obj:enemy:25) | 2 | 25 | 做出战斗的架势 |

## 快捷栏 (Action Links)
- [攻击史莱姆1](link:42 "attack(target='obj:enemy:23')")
- [火球术](link:44 "cast_fireball(target='obj:enemy:25', mana=20)")
- [逃跑](link:43 "flee()")
```

### 场景 2: 现实世界 (IDE/Editor)

```docui
## 文件浏览器
- [src/main.ts](obj:file:src/main.ts)
- [src/utils.ts](obj:file:src/utils.ts)

## 快速操作 (Action Links)
- [Git: Commit All](link:101 "git_commit(message='Update', files=git_staged())")
- [提取方法...](link:102 "extract_method(range=selection)")
```

---

## 实现路径（MVP）

| 阶段 | 内容 | 估算 |
|------|------|------|
| MVP-0 | Object-Anchor + Action-Link + AnchorTable（click only） | 1-2 天 |
| MVP-1 | `[DocUIAction]` Attribute + Roslyn Source Generator | 2 天 |
| MVP-1.5 | run_code_snippet tool + Expression Tree 执行器 | 1 天 |
| MVP-2a | Call-Only DSL（Roslyn 解析调用表达式） | 1.5 天 |
| MVP-2b | Dual-Mode Listener（tool-call + code block 双入口） | 1.5 天 |
| MVP-3 | 进程隔离 + PipeMux 协议 | 2 天 |

---

## 参考

- [Micro-Wizard](micro-wizard.md) — 多步骤交互模式
- [研讨会记录](../../../agent-team/meeting/2025-12-14-ui-anchor-workshop.md) — 详细讨论过程

---

*本文档基于 2025-12-14 UI-Anchor 研讨会共识修订*
