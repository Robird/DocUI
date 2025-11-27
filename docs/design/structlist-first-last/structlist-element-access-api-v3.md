````markdown
# StructList\<T\> 元素访问 API 设计方案 (V3)

> **文档状态**: 草案
> **创建日期**: 2025-11-27
> **作者**: API 设计评审
> **设计原则**: 极简主义 - 最小化 API 表面积，最大化语义清晰度

---

## 1. 分析阶段

### 1.1 现有问题诊断

当前 `StructList<T>` 的元素访问 API：

```csharp
public readonly ref T Peek()           // 查看尾部，异常版
public readonly bool TryPeek(out T item)  // 查看尾部，Try 版
public readonly ref T First()          // 查看头部，异常版
public readonly ref T Last() => ref Peek();  // Peek 的别名
```

| 问题 | 严重程度 | 说明 |
|------|----------|------|
| **冗余** | 高 | `Last()` 是 `Peek()` 的纯别名，增加认知负担 |
| **命名冲突** | 高 | `Peek` (栈语义) 与 `First`/`Last` (序列语义) 混用 |
| **功能不对称** | 中 | 有 `TryPeek` 但无 `TryFirst` |
| **自明性差** | 中 | `Peek()` 访问哪一端需要查阅文档 |

### 1.2 主流语言/库的设计调研

#### 1.2.1 C# 标准库

| 类型 | 首元素 | 尾元素 | Try 模式 | 返回类型 |
|------|--------|--------|----------|----------|
| `List<T>` | `[0]` | `[^1]` | 无 | T (索引器) |
| `Span<T>` | `[0]` | `[^1]` | 无 | ref T |
| `Stack<T>` | N/A | `Peek()` | `TryPeek()` | T |
| `Queue<T>` | `Peek()` | N/A | `TryPeek()` | T |
| LINQ | `First()` | `Last()` | `FirstOrDefault()` | T |

**关键洞察**:
- **通用列表** (`List<T>`, `Span<T>`) 依赖索引访问，无专用首尾方法
- **专用容器** (`Stack`, `Queue`) 使用 `Peek` 表示"查看将被移除的元素"
- LINQ 的 `First()`/`Last()` 是扩展方法，返回值拷贝

#### 1.2.2 Rust

```rust
// Vec / slice
vec.first()       // Option<&T>
vec.last()        // Option<&T>
vec.pop()         // Option<T> - 移除最后一个

// VecDeque
deque.front()     // Option<&T>
deque.back()      // Option<&T>
```

**关键洞察**:
- **命名明确**: `first`/`last` 用于序列，`front`/`back` 用于双端队列
- **统一安全语义**: 全部返回 `Option`，无异常
- **引用与值分离**: 查看返回 `&T`，移除返回 `T`

#### 1.2.3 C++ STL

```cpp
// std::vector
vec.front()       // T& (UB if empty)
vec.back()        // T& (UB if empty)

// std::deque
deque.front()     // T&
deque.back()      // T&
```

**关键洞察**:
- **命名**: `front`/`back` 表示"物理位置"
- **无安全版本**: 依赖调用者预检查

#### 1.2.4 Swift / Kotlin

```swift
// Swift Array
array.first       // Optional<Element> (属性)
array.last        // Optional<Element> (属性)

// Kotlin List
list.first()      // T (异常版)
list.firstOrNull() // T? (安全版)
list.last()       // T
list.lastOrNull() // T?
```

**关键洞察**:
- **属性 vs 方法**: Swift 使用属性表达"状态访问"
- **`OrNull` 模式**: Kotlin 的 `firstOrNull()` 对应 C# 的 `FirstOrDefault()`

#### 1.2.5 设计模式总结

| 模式 | 代表 | 优点 | 缺点 |
|------|------|------|------|
| **索引访问** | Span, List | 极简，零方法膨胀 | 需手动处理边界 |
| **First/Last 方法** | LINQ, Kotlin | 语义清晰 | 可能产生拷贝 |
| **First/Last 属性** | Swift | 简洁，语义自然 | C# 属性不常返回 ref |
| **Front/Back** | C++ STL | 物理位置明确 | 非 C# 惯用 |
| **Peek (栈/队列专用)** | C# Stack/Queue | 与 Pop 配对 | 仅适用于单端操作语义 |

---

### 1.3 核心矛盾识别

`StructList<T>` 存在两个设计张力：

1. **零拷贝 vs Try 模式**
   - `StructList` 的核心价值是 `ref` 返回避免拷贝
   - C# 的 `TryXxx(out T)` 模式**强制产生拷贝**
   - 两者不可兼得

2. **栈语义 vs 序列语义**
   - `Pop`/`Peek` 暗示栈操作
   - `First`/`Last` 暗示通用序列访问
   - 两套命名不应混用

---

## 2. 决策阶段

### 2.1 可行设计方向

#### 方向 A: 完整 Try 模式 (牺牲零拷贝)

```csharp
ref T First()
bool TryFirst(out T item)   // 产生拷贝

ref T Last()
bool TryLast(out T item)    // 产生拷贝
```

| 评分 | 一致性 ⭐⭐⭐⭐⭐ | 零拷贝 ⭐⭐ | C# 契合度 ⭐⭐⭐⭐⭐ | 极简性 ⭐⭐⭐ |
|------|-------------------|-------------|----------------------|---------------|

**问题**: `TryFirst(out T)` 违背 `StructList` 的零拷贝设计初衷。

---

#### 方向 B: 属性风格 + IsEmpty 预检查 (极简)

```csharp
ref T First { get; }    // 属性，抛异常
ref T Last { get; }     // 属性，抛异常

// 安全访问模式:
// if (!list.IsEmpty) { ref var x = ref list.First; }
```

| 评分 | 一致性 ⭐⭐⭐⭐ | 零拷贝 ⭐⭐⭐⭐⭐ | C# 契合度 ⭐⭐⭐ | 极简性 ⭐⭐⭐⭐⭐ |
|------|----------------|-------------------|------------------|-------------------|

**问题**: C# 中 `ref` 返回属性较少见，可能造成用户困惑。

---

#### 方向 C: 方法风格 + 无 Try 模式 (极简 + 惯用)

```csharp
ref T First()           // 方法，抛异常
ref T Last()            // 方法，抛异常

// 安全访问模式:
// if (!list.IsEmpty) { ref var x = ref list.First(); }
```

| 评分 | 一致性 ⭐⭐⭐⭐ | 零拷贝 ⭐⭐⭐⭐⭐ | C# 契合度 ⭐⭐⭐⭐ | 极简性 ⭐⭐⭐⭐⭐ |
|------|----------------|-------------------|-------------------|-------------------|

**优势**: 保留方法语法，与现有代码兼容性好。

---

#### 方向 D: First/Last + TryGetFirst/TryGetLast (完整但臃肿)

```csharp
ref T First()
bool TryGetFirst(out T item)

ref T Last()
bool TryGetLast(out T item)
```

| 评分 | 一致性 ⭐⭐⭐⭐⭐ | 零拷贝 ⭐⭐ | C# 契合度 ⭐⭐⭐⭐⭐ | 极简性 ⭐⭐ |
|------|-------------------|-------------|----------------------|-------------|

**问题**: API 膨胀，且 `TryGet` 命名略显冗长。

---

### 2.2 决策: 方向 C (方法风格 + 无 Try 模式)

**选择理由**:

1. **零拷贝是不可妥协的**
   - `StructList<T>` 的文档明确标注"通过 `ref` 返回避免元素复制"
   - 引入 `TryXxx(out T)` 会动摇这一核心价值

2. **极简优于完备**
   - `IsEmpty` + `First()`/`Last()` 模式足够简洁：
     ```csharp
     if (!list.IsEmpty) { ref var x = ref list.First(); }
     ```
   - 与 `Span<T>` 的使用模式一致

3. **方法优于属性**
   - 当前代码已是方法风格 (`First()`, `Last()`)
   - 方法语法在 C# 中更直观表达"可能抛异常"

4. **删除 Peek 消除歧义**
   - `Peek` 仅在栈/队列上下文有意义
   - `StructList` 是通用列表，`Last()` 更自明

5. **保留 Pop/TryPop 的合理性**
   - `Pop` 是**移除**操作，返回值拷贝是必然的（元素已不在列表中）
   - `TryPop(out T)` 的拷贝是语义正确的，与查看操作本质不同

---

### 2.3 API 表面积对比

| 方案 | 方法数 | 说明 |
|------|--------|------|
| 当前 | 4 | `Peek`, `TryPeek`, `First`, `Last` |
| 方向 A | 4 | `First`, `TryFirst`, `Last`, `TryLast` |
| 方向 B | 2 属性 | `First`, `Last` |
| **方向 C** | **2** | **`First`, `Last`** |
| 方向 D | 4 | `First`, `TryGetFirst`, `Last`, `TryGetLast` |

**方向 C 实现最小 API 表面积**。

---

## 3. 目标设计

### 3.1 完整方法签名

```csharp
#region 元素访问 (查看)

/// <summary>
/// 返回对第一个元素的引用。
/// </summary>
/// <returns>第一个元素的引用。</returns>
/// <exception cref="InvalidOperationException">列表为空时抛出。</exception>
/// <remarks>
/// 调用前请通过 <see cref="IsEmpty"/> 检查以避免异常:
/// <code>
/// if (!list.IsEmpty) {
///     ref var first = ref list.First();
/// }
/// </code>
/// </remarks>
public readonly ref T First();

/// <summary>
/// 返回对最后一个元素的引用。
/// </summary>
/// <returns>最后一个元素的引用。</returns>
/// <exception cref="InvalidOperationException">列表为空时抛出。</exception>
/// <remarks>
/// 调用前请通过 <see cref="IsEmpty"/> 检查以避免异常:
/// <code>
/// if (!list.IsEmpty) {
///     ref var last = ref list.Last();
/// }
/// </code>
/// </remarks>
public readonly ref T Last();

#endregion

#region 元素访问 (移除)

/// <summary>
/// 移除并返回最后一个元素。
/// </summary>
/// <returns>被移除的元素。</returns>
/// <exception cref="InvalidOperationException">列表为空时抛出。</exception>
public T Pop();

/// <summary>
/// 尝试移除并返回最后一个元素。
/// </summary>
/// <param name="item">如果列表非空，则为被移除的元素；否则为 <c>default(T)</c>。</param>
/// <returns>如果成功移除则为 <c>true</c>；否则为 <c>false</c>。</returns>
public bool TryPop(out T item);

#endregion
```

### 3.2 变更清单

| 方法 | 变更类型 | 说明 |
|------|----------|------|
| `First()` | **保留** | 无变化 |
| `Last()` | **修改** | 改为独立实现，不再调用 `Peek()` |
| `Peek()` | **删除** | 功能由 `Last()` 承担 |
| `TryPeek(out T)` | **删除** | 使用 `IsEmpty` + `Last()` 替代 |
| `Pop()` | **保留** | 无变化 |
| `TryPop(out T)` | **保留** | 无变化 |

### 3.3 实现代码

```csharp
#region 元素访问 (查看)

/// <summary>
/// 返回对第一个元素的引用。
/// </summary>
/// <returns>第一个元素的引用。</returns>
/// <exception cref="InvalidOperationException">列表为空时抛出。</exception>
public readonly ref T First() {
    if (_count == 0)
        ThrowEmptyList();
    return ref _items[0];
}

/// <summary>
/// 返回对最后一个元素的引用。
/// </summary>
/// <returns>最后一个元素的引用。</returns>
/// <exception cref="InvalidOperationException">列表为空时抛出。</exception>
public readonly ref T Last() {
    if (_count == 0)
        ThrowEmptyList();
    return ref _items[_count - 1];
}

#endregion
```

### 3.4 使用示例

```csharp
var list = new StructList<Point>(capacity: 10);
list.Add(new Point(1, 1));
list.Add(new Point(2, 2));
list.Add(new Point(3, 3));

// ✅ 直接访问（可能抛异常）
ref var first = ref list.First();
ref var last = ref list.Last();

// ✅ 安全访问模式
if (!list.IsEmpty) {
    ref var safeFirst = ref list.First();
    safeFirst.X = 100;  // 直接修改，零拷贝
}

// ✅ 通过索引访问（等效）
ref var alsoFirst = ref list[0];
ref var alsoLast = ref list[list.Count - 1];

// ✅ 栈操作
var popped = list.Pop();
if (list.TryPop(out var item)) {
    Console.WriteLine(item);
}

// ❌ 不再支持（编译错误）
// list.Peek()      → 使用 list.Last()
// list.TryPeek()   → 使用 if (!list.IsEmpty) list.Last()
```

### 3.5 迁移指南

#### Breaking Changes

| 旧 API | 新 API | 迁移方式 |
|--------|--------|----------|
| `Peek()` | `Last()` | 直接替换 |
| `TryPeek(out T item)` | `if (!list.IsEmpty) list.Last()` | 重构为显式检查 |

#### 迁移脚本 (正则表达式)

```regex
# Peek() → Last()
查找: \.Peek\(\)
替换: .Last()

# TryPeek 需要手动重构
查找: \.TryPeek\s*\(
提示: 改为 if (!xxx.IsEmpty) { ... = xxx.Last(); }
```

#### TryPeek 迁移示例

**旧代码:**
```csharp
if (list.TryPeek(out var item)) {
    DoSomething(item);
}
```

**新代码 (零拷贝):**
```csharp
if (!list.IsEmpty) {
    ref var item = ref list.Last();
    DoSomething(in item);  // 使用 in 传递引用
}
```

**新代码 (值拷贝，如果需要):**
```csharp
if (!list.IsEmpty) {
    var item = list.Last();  // 隐式拷贝
    DoSomething(item);
}
```

---

## 4. 设计原理

### 4.1 为什么不提供 TryFirst / TryLast？

| 因素 | 说明 |
|------|------|
| **零拷贝原则** | `TryXxx(out T)` 强制产生拷贝，与 `StructList` 设计冲突 |
| **语言限制** | C# 无法表达"可能不存在的引用" (`ref T?` 不合法) |
| **IsEmpty 足够** | `if (!list.IsEmpty) list.First()` 只多一行，换来零拷贝 |
| **与 Span 一致** | `Span<T>` 也无 Try 方法，依赖 `Length` 预检查 |

### 4.2 为什么删除 Peek 而非保留别名？

| 因素 | 说明 |
|------|------|
| **语义歧义** | `Peek` 在栈中指顶部，在队列中指头部，对列表有歧义 |
| **冗余** | `Last()` 完全覆盖 `Peek()` 的功能 |
| **极简原则** | 同功能仅保留一个 API |
| **破坏性可控** | `StructList<T>` 是 `internal` 类型，影响范围有限 |

### 4.3 为什么 Pop/TryPop 返回值拷贝是正确的？

`Pop` 与 `First`/`Last` 的本质区别：

| 操作 | 语义 | 返回类型 | 理由 |
|------|------|----------|------|
| `First()`/`Last()` | **查看** | `ref T` | 元素仍在列表中，返回引用安全 |
| `Pop()` | **移除** | `T` | 元素已从列表移除，返回引用不安全 |

返回已移除元素的引用会导致悬空引用，因此 `Pop` 必须返回值拷贝。

---

## 5. 附录

### 5.1 与前两版方案对比

| 维度 | V1 | V2 | V3 (本方案) |
|------|-----|-----|-------------|
| First/Last 形式 | 方法 | 属性 | **方法** |
| 提供 TryFirst/TryLast | ✅ | ❌ | **❌** |
| 新增 Index 索引器 | ❌ | ✅ | ❌ |
| 删除 Peek/TryPeek | ✅ | ✅ | **✅** |
| API 数量变化 | 4→4 | 4→2 属性 | **4→2** |

### 5.2 未来扩展预留

如果将来需要双端队列功能，可考虑添加：

```csharp
T PopFirst();             // 移除首元素
bool TryPopFirst(out T);  // 安全版
void Prepend(in T);       // 头部添加
```

但这超出当前范围，仅作记录。

---

## 6. 结论

本方案采用**极简主义**设计：

- **删除**: `Peek()`, `TryPeek(out T)` — 消除冗余和歧义
- **保留**: `First()`, `Last()`, `Pop()`, `TryPop(out T)` — 核心功能
- **不添加**: `TryFirst`, `TryLast` — 避免破坏零拷贝原则

最终 API 表面积从 4 个方法减少到 2 个查看方法 + 2 个移除方法，语义清晰，零拷贝保证。

---

**文档结束**

````
