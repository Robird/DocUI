# Proposal-0000: Proposal 流程规范

- **Status**: Active
- **Author**: Team Leader (刘德智)
- **Created**: 2025-12-10
- **Impact**: High (Breaking: No)

## Abstract

本文档定义 DocUI 设计提案（Proposal）的格式规范、状态流转、评审流程和版本策略。所有后续 Proposal 必须遵循本文档定义的流程。

## Motivation

DocUI 是一个面向 LLM Agent 的 UI 框架，涉及多个相互关联的设计决策。为了：

1. **确保设计一致性**：避免相互矛盾的设计决策
2. **支持异步协作**：不同 Specialist 可以独立审阅和贡献
3. **保留设计历史**：记录为什么选择 A 而非 B
4. **管理演进兼容**：控制 Breaking Changes 的影响

我们需要一个规范化的 Proposal 流程。

## Proposed Design

### 1. 编号规则

| 编号范围 | 用途 |
|----------|------|
| 0000 | 元文档（本文档） |
| 0001-0009 | 基础概念 |
| 0010-0019 | 协议层 |
| 0020-0029 | 机制层 |
| 0030-0099 | Feature Proposals |
| 0100+ | 未来扩展 |

编号一旦分配，永不重用。即使 Proposal 被拒绝，其编号也保留作为历史记录。

### 2. 文档格式

每个 Proposal 必须包含以下章节：

```markdown
# Proposal-XXXX: <Title>

- **Status**: Draft | Active | Accepted | Implemented | Rejected
- **Author**: <name>
- **Created**: <date>
- **Depends-On**: Proposal-YYYY (可选)
- **Impact**: Low | Medium | High (Breaking: Yes/No)

## Abstract
（一段话摘要，50 字以内）

## Motivation
（为什么需要这个提案，包含问题陈述和预期收益）

## Proposed Design
（具体设计，必须包含以下子节）

### Data Structures
（核心接口和数据模型定义）

### Invariants
（不变式：必须始终保持的性质）

### Error Handling
（错误类型枚举和恢复策略）

## Alternatives Considered
（考虑过的替代方案及其优缺点）

## Security Considerations
（安全与滥用场景分析）

## Compatibility
（版本兼容承诺，向后/向前兼容说明）

## Test Vectors
（参考输入/输出样例，至少 3 个）

## Open Questions
（待解决问题列表）

## References
（相关文档和外部参考）
```

### 3. 状态流转

```
                    ┌──────────┐
                    │  Draft   │
                    └────┬─────┘
                         │ 提交评审
                         ▼
                    ┌──────────┐
              ┌─────│  Active  │─────┐
              │     └──────────┘     │
              │ 通过                  │ 拒绝
              ▼                      ▼
        ┌──────────┐          ┌──────────┐
        │ Accepted │          │ Rejected │
        └────┬─────┘          └──────────┘
             │ 实现完成
             ▼
        ┌──────────────┐
        │ Implemented  │
        └──────────────┘
```

**状态定义**：
- **Draft**: 作者正在撰写，可随时修改
- **Active**: 提交评审，开放讨论
- **Accepted**: 设计已批准，待实现
- **Implemented**: 已在代码中实现
- **Rejected**: 设计被拒绝，保留历史记录

### 4. 评审流程

1. **提交**: 作者将 Status 改为 Active，在 meeting/ 或 handoffs/ 中发起评审请求
2. **讨论**: 至少 2 位 Specialist 发表意见
3. **修订**: 根据反馈修改 Proposal
4. **决议**: Team Leader 做出 Accept/Reject 决定
5. **记录**: 在 Proposal 末尾追加评审记录

### 5. 版本策略

#### 5.1 语义化版本

每个 Proposal 可选择性地定义版本号：
- **MAJOR**: 不兼容的变更
- **MINOR**: 向后兼容的新功能
- **PATCH**: 向后兼容的 Bug 修复

#### 5.2 Breaking Change 处理

当 Proposal 引入 Breaking Change 时：
1. **Impact** 字段必须标记为 `High (Breaking: Yes)`
2. **Compatibility** 章节必须说明迁移路径
3. 评审时需要额外关注影响范围

#### 5.3 弃用流程

1. 标记为 Deprecated（在 Proposal 顶部添加警告）
2. 保留至少一个版本周期
3. 在后续 Proposal 中正式移除

### 6. 交互锚点注册流程

交互锚点（Button/Form）的注册：

#### 6.1 锚点分类

| 类型 | 格式 | 语义 | 示例 |
|------|------|------|------|
| **Button** | `[button:cmd]` | 无参动作，点击即执行 | `[button:fold]`, `[button:save]` |
| **Form** | `[form:cmd param=value]` | 有参动作，需要填参数 | `[form:goto line=10]`, `[form:filter tag=project]` |

> **注**: Button 是 Form 的别名（零参数的 Form）

#### 6.2 注册流程

1. **预定义 Button/Form**: 在 Proposal-0003 中定义核心交互锚点集合
2. **扩展 Button/Form**: 新锚点需要新的 Proposal（Proposal-0012+）
3. **命名空间**: 
   - 核心锚点: `fold`, `expand`, `goto`
   - 扩展锚点: `docui_xxx` 前缀
   - 应用锚点: `app_xxx` 前缀

### 7. Proposal 类型

| 类型 | 描述 | 示例 |
|------|------|------|
| **Core** | 定义核心协议/数据结构 | Proposal-0001 Context 模型 |
| **Protocol** | 定义交互协议 | Proposal-0010 渲染协议 |
| **Mechanism** | 定义机制实现 | Proposal-0020 状态同步 |
| **Feature** | 组合现有协议描述交互场景 | Proposal-0030+ |
| **Process** | 定义流程本身 | Proposal-0000 本文档 |

## Alternatives Considered

### A. 使用 GitHub Issues
- 优点：熟悉的工作流，内置讨论
- 缺点：不利于长期维护，难以版本化

### B. 使用 RFC 格式
- 优点：业界标准，结构严谨
- 缺点：过于正式，不适合快速迭代

### C. 使用 ADR (Architecture Decision Records)
- 优点：轻量级，聚焦决策
- 缺点：缺少版本策略和评审流程

**选择理由**: 采用 PEP 风格，因为它平衡了正式性和灵活性，且有成熟的实践经验。

## Security Considerations

### 潜在风险

1. **Proposal 注入**: 恶意 Proposal 可能引入后门
   - 缓解：所有 Proposal 需要评审
   
2. **锚点命名冲突**: 不同 Proposal 定义同名 Button/Form
   - 缓解：强制命名空间前缀

### 核查要求

- 所有 Breaking Change 需要显式标记
- 安全相关 Proposal 需要额外审阅

## Compatibility

- 本文档是初始版本，无向后兼容需求
- 后续修改需要保持与现有 Proposal 的兼容

## Test Vectors

### 示例 1: 最小 Proposal

```markdown
# Proposal-0099: 示例提案

- **Status**: Draft
- **Author**: Example
- **Created**: 2025-12-10
- **Impact**: Low (Breaking: No)

## Abstract
这是一个示例提案。

## Motivation
演示最小格式。

## Proposed Design
（略）

## Alternatives Considered
无。

## Security Considerations
无特殊考虑。

## Compatibility
向后兼容。

## Test Vectors
N/A

## Open Questions
无。
```

### 示例 2: 状态流转

```
Proposal-0001 状态变化历史:
- 2025-12-10: Draft (创建)
- 2025-12-11: Active (提交评审)
- 2025-12-12: Active (修订 v2)
- 2025-12-15: Accepted (评审通过)
- 2025-12-20: Implemented (代码合并)
```

### 示例 3: Breaking Change 标记

```markdown
- **Impact**: High (Breaking: Yes)

## Compatibility

**Breaking Changes**:
- 移除 `[button:legacy]` 支持
- 修改 Context.metadata 字段类型

**迁移路径**:
1. 将 `[button:legacy]` 替换为 `[button:new]`
2. 更新 metadata 解析逻辑

**弃用时间线**:
- v0.2: 标记 deprecated
- v0.3: 移除支持
```

## Open Questions

1. **评审周期**: 是否需要设定评审截止日期？
2. **投票机制**: 是否需要正式投票，还是 Team Leader 直接决定？
3. **回溯修改**: Implemented 状态的 Proposal 是否允许修改？

## References

- [PEP 1 -- PEP Purpose and Guidelines](https://peps.python.org/pep-0001/)
- [RFC 2026 -- The Internet Standards Process](https://www.rfc-editor.org/rfc/rfc2026)
- [Swift Evolution Process](https://github.com/apple/swift-evolution/blob/main/process.md)
- [研讨会记录](../../../agent-team/meeting/seminar-docui-proposals-2025-12-10.md)

---

## 评审记录

*(待评审后追加)*
