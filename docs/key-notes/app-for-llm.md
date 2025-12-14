# App-For-LLM 与 Capability-Provider

## App-For-LLM

> **App-For-LLM** 是一种 Agent 系统的外部扩展机制，独立进程通过 RPC（PipeMux/JSON-RPC）与 Agent 通信，为 Agent 提供额外能力。

特征：
- 把关系紧密的一组数据、视图、操作封装为一个整体供 LLM 使用
- 与 LLM 之间进行双向交互的界面叫做 DocUI，包括渲染与操作两个方向
- 在 DocUI 语境下简称 **App**（注意：此简称仅指 App-For-LLM，不包括 Built-in）

---

## Capability-Provider

> **Capability-Provider** 是通过 DocUI 向 LLM 提供能力的实体的统称，包括 Built-in 和 App-For-LLM 两类。

LLM 通过统一的 DocUI 界面与所有 Capability-Provider 交互，无需区分能力来源。

---

## Built-in

> **Built-in** 是 Agent 内建功能，与 Agent 生命周期绑定，进程内直接调用，可直接访问内部状态。

特点：
- 直接访问 Agent 内部状态（Agent-History、Context），无需 RPC 序列化
- 是 Agent 的"器官"，不是"插件"

示例：Recap、History 管理、上下文统计、元认知反射

---

## DocUI 与 App-For-LLM 的分离

### 概念澄清

**DocUI** 是 LLM 与功能交互的**界面层**：
- 渲染信息（将状态呈现为 LLM 可理解的文本）
- 管理可用操作（根据上下文动态调整 Tool 列表）
- 执行功能调用（接收 LLM 的 Tool-Call，路由到实现）
- 反馈执行结果（将结果渲染为 Observation）

**App-For-LLM** 是**外部扩展机制**：
- 独立进程，通过 RPC（PipeMux/JSON-RPC）与 Agent 通信
- 为 Agent 提供额外能力（文本编辑、知识库、外部服务接入等）
- 第三方开发者（包括 LLM Agent 自身）可贡献 App

### 分层架构

```mermaid
graph TB
    LLM[LLM]
    DocUI["DocUI (交互界面)<br/>渲染 · 操作管理 · 调用路由 · 结果反馈"]
    BuiltIn["Agent 内建功能<br/>(进程内直接调用)"]
    AppForLLM["App-For-LLM (外部扩展)<br/>(RPC 调用独立进程)"]
    
    LLM -->|Tool-Call| DocUI
    DocUI -->|Observation| LLM
    
    DocUI --> BuiltIn
    DocUI --> AppForLLM
    
    BuiltIn -.->|"• Recap/History 管理<br/>• 上下文统计<br/>• 元认知反射<br/>• ..."| BuiltIn
    AppForLLM -.->|"• MemoryNotebook<br/>• TextEditor<br/>• SystemMonitor<br/>• 第三方 App..."| AppForLLM
```

### 设计原则

1. **界面统一**：无论功能来自内建还是外部扩展，LLM 看到的都是 DocUI 渲染的文本 + 可用的 Tool 列表。LLM 不需要区分功能的实现位置。

2. **内建功能是"器官"，不是"插件"**：Recap、上下文统计等自省功能是 Agent 的内在能力，与 Agent 生命周期绑定。它们直接访问 Agent 内部状态（History、Context），无需 RPC 序列化。

3. **外部扩展走 RPC**：App-For-LLM 一律是独立进程。这提供了进程级隔离（崩溃不影响 Agent）、语言无关性（任意语言实现）、热重载能力（更新 App 无需重启 Agent）。

4. **不提供内嵌插件机制**：避免"内嵌 vs 外部"的边界模糊。内建功能是 Agent 开发者维护的核心能力；外部扩展是第三方贡献的附加能力。

---

## 两类 Capability-Provider 对比

| 类型 | 特点 | 示例 |
|------|------|------|
| **Built-in** | 与 Agent 生命周期绑定，进程内直接调用，可直接访问内部状态 | Recap、History 管理、上下文统计 |
| **App-For-LLM** | 独立进程，通过 RPC 通信，进程级隔离 | MemoryNotebook、TextEditor、第三方扩展 |

---

## 术语使用约束

1. **"App" 简称仅指 App-For-LLM**（外部扩展）
   - ✅ "这个 App 需要热重载" — 正确，App 是独立进程
   - ❌ "Recap 是一个 App" — 错误，Recap 是 Built-in

2. **需要统称时使用 "Capability-Provider"**
   - ✅ "所有 Capability-Provider 都通过 DocUI 暴露能力"
   - ✅ "LLM 无需区分 Capability-Provider 的类型"

3. **连字符命名风格**
   - 复合术语统一使用连字符：Context-Projection、Capability-Provider、App-For-LLM、Agent-OS

---

## 决策记录

> **2025-12-12 架构讨论**
> 
> 原先将 DocUI 与 App-For-LLM 绑定，导致"自省功能是否需要内嵌 App"的两难。
> 
> 分离后：
> - DocUI 是交互界面，与实现位置无关
> - Agent 内建功能直接通过 DocUI 暴露，不是 App
> - App-For-LLM 专指外部进程扩展
> 
> 参见：agent-team/meeting/2025-12-12-app-process-architecture.md
