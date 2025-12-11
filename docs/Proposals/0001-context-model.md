# Proposal-0001: Context 模型

- **Status**: Draft
- **Author**: Team Leader (刘德智)
- **Created**: 2025-12-10
- **Impact**: High (Breaking: No)

## Abstract

定义 DocUI 的核心抽象——Context：呈现给 LLM Agent 的完整文本内容。Context 是状态的纯函数，支持冻结/缓存机制，保证多轮交互的一致性。

## Motivation

LLM Agent 通过 Context（上下文窗口）感知外部世界。DocUI 需要一个统一的模型来描述：

1. **Context 的结构**：包含哪些组成部分
2. **Context 的生成**：如何从应用状态渲染出 Context
3. **Context 的缓存**：何时冻结、何时失效
4. **Context 的约束**：协议层所需的最小字段集

### 问题陈述

当前概念原型（MemoryNotebook、TextEditor、SystemMonitor）各自定义了渲染逻辑，缺乏统一抽象：
- 无法复用渲染组件
- 无法保证多轮交互一致性
- 无法定义协议层依赖的稳定接口

### 预期收益

- 统一的 Context 模型，所有应用基于此构建
- 明确的冻结/失效语义，支持缓存优化
- 清晰的协议最小字段集，支持协议层设计

## Proposed Design

### 1. Context 结构

```
Context
├── Header          # 元信息区
│   ├── AppId       # 应用标识
│   ├── SessionId   # 会话标识  
│   ├── Version     # 状态版本号
│   └── Timestamp   # 渲染时间戳
├── State           # 状态区
│   ├── CurrentLod  # 当前 LOD 级别
│   ├── FocusId     # 当前焦点对象
│   └── Custom      # 应用自定义状态
├── Content         # 内容区
│   └── Markdown    # 渲染后的 Markdown 文本
├── Anchors         # 锚点注册表
│   └── [id] → {type, actions, target}
└── History         # 历史区（可选）
    └── [round] → {action, result, timestamp}
```

### 2. Context 公式

$$Context = f(SourceData, LodState, InteractionHistory)$$

**幂等性要求**：给定相同的输入，必须产生相同的输出。

```csharp
public interface IContextBuilder
{
    /// <summary>
    /// 构建 Context，必须是纯函数
    /// </summary>
    Context Build(SourceData data, LodState lod, InteractionHistory? history);
}
```

### 3. 冻结/缓存机制

借鉴 copilot-chat 的 `FrozenContent` 机制：

#### 3.1 冻结点 (Freeze Point)

| 冻结点 | 时机 | 语义 |
|--------|------|------|
| **Header** | 会话开始 | 整个会话不变 |
| **State.CurrentLod** | LOD 切换后 | 直到下次切换 |
| **Content** | 首次渲染后 | 除非状态变化 |

#### 3.2 失效条件 (Invalidation)

| 字段 | 失效条件 |
|------|----------|
| Content | SourceData 变化、LodState 变化 |
| Anchors | 锚点被执行、新锚点注册 |
| History | 新的交互轮次 |

#### 3.3 版本号机制

```csharp
public record ContextVersion
{
    public int Major { get; }      // SourceData 版本
    public int Minor { get; }      // LodState 版本
    public int Patch { get; }      // 渲染细节版本
    
    public bool IsCompatibleWith(ContextVersion other) =>
        Major == other.Major && Minor == other.Minor;
}
```

### 4. 协议最小字段集

协议层（Proposal-0010/0011）依赖以下字段，Context 实现必须保证：

| 字段 | 类型 | 约束 |
|------|------|------|
| `Header.AppId` | string | 非空，唯一标识应用 |
| `Header.SessionId` | string | 非空，唯一标识会话 |
| `Header.Version` | ContextVersion | 非空，支持兼容性检查 |
| `State.CurrentLod` | LodLevel | 枚举值，不可为 null |
| `Anchors` | Dictionary | 可为空字典，不可为 null |

### Data Structures

```csharp
/// <summary>
/// Context 核心结构
/// </summary>
public record Context
{
    public required ContextHeader Header { get; init; }
    public required ContextState State { get; init; }
    public required string Content { get; init; }
    public required IReadOnlyDictionary<string, AnchorInfo> Anchors { get; init; }
    public InteractionHistory? History { get; init; }
}

public record ContextHeader
{
    public required string AppId { get; init; }
    public required string SessionId { get; init; }
    public required ContextVersion Version { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
}

public record ContextState
{
    public required LodLevel CurrentLod { get; init; }
    public string? FocusId { get; init; }
    public IReadOnlyDictionary<string, object>? Custom { get; init; }
}

public record AnchorInfo
{
    public required AnchorType Type { get; init; }
    public required IReadOnlyList<string> Params { get; init; }  // Form 参数列表
    public string? Target { get; init; }
}

/// <summary>
/// 锚点类型
/// - Button: 无参动作，点击即执行
/// - Form: 有参动作，需要填参数
/// - Reference: 引用锚点，用于定位而非操作
/// </summary>
public enum AnchorType { Button, Form, Reference }

public enum LodLevel { Gist, Summary, Full }
```

### Invariants

1. **幂等性**: `Build(d, l, h) == Build(d, l, h)` 对于相同输入
2. **版本单调性**: `Version.Major` 只增不减
3. **锚点唯一性**: `Anchors` 字典中 key 必须唯一
4. **最小字段完整性**: 协议最小字段集永不为 null
5. **冻结一致性**: 冻结后的字段不可变

### Error Handling

| 错误类型 | 触发条件 | 恢复策略 |
|----------|----------|----------|
| `InvalidSourceDataError` | SourceData 格式错误 | 返回错误 Context，包含诊断信息 |
| `VersionMismatchError` | 版本不兼容 | 强制重新渲染 |
| `AnchorConflictError` | 锚点 ID 冲突 | 保留第一个，记录警告 |
| `ContextTooLargeError` | 超过 Token 限制 | 自动降级 LOD |

## Alternatives Considered

### A. 扁平结构（无分区）
- 优点：简单
- 缺点：无法分别缓存不同部分

### B. 完全不可变（每次全新构建）
- 优点：无缓存复杂性
- 缺点：性能差，无法保证多轮一致性

### C. 响应式模型（类 React）
- 优点：自动追踪依赖
- 缺点：增加复杂性，不适合批量渲染

**选择理由**: 分区结构 + 显式冻结点，平衡了灵活性和可控性。

## Security Considerations

### 潜在风险

1. **信息泄露**: Context 可能包含敏感信息
   - 缓解：应用负责过滤敏感数据
   
2. **版本欺骗**: 恶意版本号绕过缓存
   - 缓解：版本号由框架生成，不可外部设置

3. **锚点注入**: 用户内容包含锚点格式文本
   - 缓解：内容区转义，锚点只能通过 Anchors 注册表声明

## Compatibility

- 本文档定义初始版本 1.0
- 向后兼容承诺：
  - 不会移除最小字段集中的字段
  - 新增字段使用可选类型
  - 弃用字段保留 2 个版本周期

## Test Vectors

### 示例 1: 最小有效 Context

```json
{
  "header": {
    "appId": "notebook",
    "sessionId": "sess-001",
    "version": { "major": 1, "minor": 0, "patch": 0 },
    "timestamp": "2025-12-10T10:00:00Z"
  },
  "state": {
    "currentLod": "Summary",
    "focusId": null,
    "custom": null
  },
  "content": "# Notebook\n\n[SUMMARY] Entry 1",
  "anchors": {},
  "history": null
}
```

### 示例 2: 带锚点的 Context

```json
{
  "header": { "appId": "notebook", "sessionId": "sess-002", "version": { "major": 1, "minor": 1, "patch": 0 }, "timestamp": "2025-12-10T10:05:00Z" },
  "state": { "currentLod": "Summary", "focusId": "entry-1", "custom": null },
  "content": "# Notebook\n\n[SUMMARY] **[entry-1]** PipeMux 概览 [button:expand]",
  "anchors": {
    "expand-entry-1": {
      "type": "Button",
      "params": [],
      "target": "entry-1"
    },
    "entry-1": {
      "type": "Reference",
      "params": [],
      "target": null
    }
  },
  "history": null
}
```

### 示例 3: 版本兼容性检查

```csharp
var v1 = new ContextVersion(1, 0, 0);
var v2 = new ContextVersion(1, 1, 0);
var v3 = new ContextVersion(2, 0, 0);

v1.IsCompatibleWith(v2); // true (minor change)
v1.IsCompatibleWith(v3); // false (major change)
```

## Open Questions

1. **History 上限**: InteractionHistory 最多保留多少轮？
2. **压缩策略**: 超过 Token 限制时，如何选择压缩哪些部分？
3. **跨会话共享**: 不同 SessionId 的 Context 是否可以共享 SourceData 缓存？

## References

- [copilot-chat Tool Calling Loop](../../copilot-chat-deepwiki/13_Tool_Calling_Loop.md)
- [研讨会记录: LLM Context as UI](../../../agent-team/meeting/seminar-docui-as-llm-ui-2025-12-10.md)
- [研讨会记录: Proposal 规划](../../../agent-team/meeting/seminar-docui-proposals-2025-12-10.md)

---

## 评审记录

*(待评审后追加)*
