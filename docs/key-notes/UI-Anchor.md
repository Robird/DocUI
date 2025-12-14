
## UI-Anchor (Draft)
> **UI-Anchor** 为LLM提供引用和操作DocUI中可见元素的可靠锚点。

## 动机

DocUI要完成与LLM互动的职能，就需要向LLM提供引用UI中元素的锚点，实现“句柄”/“ID”的职能。
UI-Anchor 很有用，比如：
  - 让LLM可以无需复述文本就能实现大段的复制粘贴、调整段落顺序。
  - 让LLM可以操作难以复述的信息，比如多模态上下文中的音频和图像。
  - 让LLM可以快速执行具有上下文和预绑定参数的操作。

## 设想

### 场景 1: 虚拟世界 (MUD/RPG)

````docui
## 敌人列表
| Name(Anchor) | Level | HP | State |
| --- | --- | --- | --- |
| [史莱姆1](obj:23) | 1 | 11 | 正在舔舐伤口 |
| [史莱姆2](obj:24) | 1 | 15 | 惊恐的看着你 |
| [强盗](obj:25) | 2 | 25 | 做出战斗的架势 |

## 技能栏 (Action Prototypes)

```typescript
// 物理攻击
function attack(target: Anchor<Obj>): void;

// 魔法攻击
// @param mana (Default: 10)
function cast_fireball(target: Anchor<Obj>, mana: int = 10): void;

// 治疗
function heal_all(targets: List<Anchor<Obj>>): void;
```

## 快捷栏 (Action Links)
- [攻击史莱姆1](link:42 "attack(target='obj:23')")
- [逃跑](link:43 "flee()")
````

### 场景 2: 现实世界 (IDE/Editor)

````docui
## 文件浏览器
- [src/main.ts](file:src/main.ts)
- [src/utils.ts](file:src/utils.ts)

## 常用操作 (Action Prototypes)

```typescript
/** 提取选中代码为新方法 */
function extract_method(range: Selection, new_name: string): void;

/** 提交更改 */
function git_commit(message: string, files: List<Anchor<File>>): void;
```

## 快速操作 (Action Links)
- [Git: Commit All](link:101 "git_commit(message='Update', files=git_staged())")
````

## 核心概念

### UI-Anchor
> **UI-Anchor** 是 DocUI 中用于标识可引用或可操作元素的统一机制。它为 LLM 提供了无需复述内容即可精确指代目标的"句柄"。

### Object-Anchor (Entity Handle)
> **Object-Anchor** 用于标识界面中的实体对象（名词）。
> **语法**: `[Label](obj:<id>)`
> **用途**: 作为 Action 的参数（如 `target='obj:23'`）。

### Action-Prototype (Live API)
> **Action-Prototype** 以函数原型的形式直接披露操作接口。它将 UI 转化为 **Live API Documentation**。
> **语法**: Markdown Code Block (TypeScript/Python Signature)
> **用途**: 供 LLM 阅读并编写代码调用（REPL 模式）。

### Action-Link (Pre-filled Call)
> **Action-Link** 是预先填充好参数（或无参）的快捷操作链接。
> **语法**: `[Label](link:<id> "code_snippet")`
> **用途**: 点击即执行（通过 `click(id)` 工具），相当于 GUI 中的 Button。

## 交互模式 (The REPL Paradigm)

DocUI 不再依赖繁琐的 JSON Schema Tool-Calling，而是转向 **REPL (Read-Eval-Print Loop)** 范式：

1.  **Read**: LLM 阅读文档中的 `Action-Prototype` 和 `Object-Anchor`。
2.  **Eval**: LLM 编写一段代码（如 `cast_fireball(target='obj:23')`）。
3.  **Execute**: Agent-OS 在沙箱中执行代码，或通过 `click(id)` 触发 `Action-Link`。

## 开放问题

两类候选Anchor生存期策略：
1. 持久、可存储。这预示着可能会用UUID/GUID。代价是长，悬空引用问题。
2. 临时、不可存储。[Context-Projection]过程中动态分配并管理，生存期与对应DocUI元素的可见性绑定。好处是可以非常短，利用了LLM的“健忘”特性。分析下来，这似乎是更优的选择，对于那些需要可被持久化引用的信息可以另有机制。

## TODO
- 各定义追加到“DocUI/docs/key-notes/glossary.md”
