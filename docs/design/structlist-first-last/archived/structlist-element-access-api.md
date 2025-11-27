# StructList\<T\> 元素访问 API 设计方案

> **文档状态**: 草案
> **创建日期**: 2025-11-27
> **作者**: API 设计评审

---

## 1. 分析阶段

### 1.1 现有问题回顾

当前 `StructList<T>` 的元素访问 API 存在以下问题：

| 问题 | 描述 |
|------|------|
| **冗余** | `Last()` 是 `Peek()` 的纯别名，两者功能完全重叠 |
| **功能缺失** | 有 `TryPeek` (尾部) 但没有 `TryFirst` / `TryPeekFirst` (头部) |
| **命名不一致** | `Peek` 暗示栈语义 (LIFO)，而 `First`/`Last` 暗示序列语义 |
| **自明性差** | 用户无法直观判断 `Peek` 访问的是头部还是尾部 |

### 1.2 主流语言/库的类似设计

#### 1.2.1 C# BCL (List\<T\>, Span\<T\>, LINQ)

**List\<T\>:**
```csharp
// 无内置 First/Last，依赖索引访问
list[0]           // 第一个
list[list.Count - 1]  // 最后一个
list[^1]          // C# 8+ Index 语法
```

**Span\<T\>:**
```csharp
span[0]           // 第一个
span[^1]          // 最后一个 (Index 语法)
```

**LINQ (IEnumerable\<T\>):**
```csharp
First()           // 异常版
FirstOrDefault()  // 返回 default(T)
Last()            // 异常版
LastOrDefault()   // 返回 default(T)
// 注意：LINQ 的 Last() 对非 IList<T> 可能 O(n)
```

**Stack\<T\>:**
```csharp
Peek()            // 查看栈顶，异常版
TryPeek(out T)    // .NET 6+ 新增
Pop()             // 弹出栈顶
TryPop(out T)     // .NET 6+ 新增
```

**Queue\<T\>:**
```csharp
Peek()            // 查看队首，异常版
TryPeek(out T)    // .NET 6+ 新增
Dequeue()         // 出队
TryDequeue(out T) // .NET 6+ 新增
```

| 特性 | 评价 |
|------|------|
| 命名惯例 | `Peek` 用于栈/队列语义；`First`/`Last` 用于序列语义 (LINQ) |
| 返回类型 | LINQ 返回值拷贝；索引器可配合 `ref` 返回 |
| 异常处理 | 异常版 + `TryXxx` 模式；`XxxOrDefault` 返回 `default(T)` |
| 优点 | 命名语义清晰，Try 模式成熟 |
| 缺点 | LINQ 的 `First`/`Last` 是扩展方法，不返回 ref |

---

#### 1.2.2 Rust (Vec, slice, VecDeque)

**Vec / slice:**
```rust
vec.first()       // Option<&T>
vec.first_mut()   // Option<&mut T>
vec.last()        // Option<&T>
vec.last_mut()    // Option<&mut T>

// 以下在 nightly 或通过扩展获得：
vec.pop()         // Option<T> - 移除最后一个
```

**VecDeque (双端队列):**
```rust
deque.front()     // Option<&T>
deque.front_mut() // Option<&mut T>
deque.back()      // Option<&T>
deque.back_mut()  // Option<&mut T>
deque.pop_front() // Option<T>
deque.pop_back()  // Option<T>
```

| 特性 | 评价 |
|------|------|
| 命名惯例 | 序列用 `first`/`last`；双端队列用 `front`/`back` |
| 返回类型 | 统一使用 `Option<&T>` / `Option<&mut T>`，显式区分可变性 |
| 异常处理 | 无异常，全部通过 `Option` 返回 |
| 优点 | 命名极其清晰，类型系统强制处理空情况 |
| 缺点 | `Option` 模式在 C# 中不够原生 |

---

#### 1.2.3 Java (ArrayList, Deque)

**ArrayList:**
```java
list.get(0)                   // 第一个，抛 IndexOutOfBoundsException
list.get(list.size() - 1)     // 最后一个
list.getFirst()               // Java 21+ SequencedCollection
list.getLast()                // Java 21+ SequencedCollection
```

**Deque 接口:**
```java
deque.peekFirst()   // null if empty
deque.peekLast()    // null if empty
deque.getFirst()    // 抛 NoSuchElementException if empty
deque.getLast()     // 抛 NoSuchElementException if empty
deque.pollFirst()   // 移除并返回，null if empty
deque.pollLast()    // 移除并返回，null if empty
```

| 特性 | 评价 |
|------|------|
| 命名惯例 | `getFirst`/`getLast` vs `peekFirst`/`peekLast` |
| 返回类型 | 返回值拷贝（无 ref 语义） |
| 异常处理 | `get` 系抛异常，`peek`/`poll` 系返回 null |
| 优点 | `peekFirst`/`peekLast` 命名显式 |
| 缺点 | null 语义对值类型不友好；Java 无 ref 返回 |

---

#### 1.2.4 Python (list)

```python
lst[0]      # 第一个，抛 IndexError
lst[-1]     # 最后一个，抛 IndexError
lst.pop()   # 移除并返回最后一个，抛 IndexError
lst.pop(0)  # 移除并返回第一个
```

| 特性 | 评价 |
|------|------|
| 命名惯例 | 无专门方法，依赖索引 |
| 返回类型 | 值拷贝 |
| 异常处理 | 全部抛异常，无 Try 模式 |
| 优点 | 极简 |
| 缺点 | 无显式 API，可读性依赖约定 |

---

#### 1.2.5 C++ (std::vector, std::deque)

**std::vector:**
```cpp
vec.front()     // T& - 首元素引用，UB if empty
vec.back()      // T& - 尾元素引用，UB if empty
vec.at(0)       // T& - 有边界检查，抛 std::out_of_range
```

**std::deque:**
```cpp
deque.front()   // T&
deque.back()    // T&
```

| 特性 | 评价 |
|------|------|
| 命名惯例 | `front`/`back` 用于双端；`at` 用于安全索引 |
| 返回类型 | 返回引用 |
| 异常处理 | `front`/`back` 是 UB (不检查)；`at` 抛异常 |
| 优点 | `front`/`back` 命名简洁 |
| 缺点 | 无安全的 Try 模式；UB 对安全敏感场景不友好 |

---

### 1.3 设计要素对比总结

| 语言/库 | 首元素 | 尾元素 | 安全版本 | 返回类型 | 栈语义 |
|---------|--------|--------|----------|----------|--------|
| C# List | `[0]` | `[^1]` | N/A | ref (索引器) | N/A |
| C# Stack | N/A | `Peek()` | `TryPeek` | copy | `Peek`/`Pop` |
| C# LINQ | `First()` | `Last()` | `FirstOrDefault` | copy | N/A |
| Rust Vec | `first()` | `last()` | 返回 Option | Option\<&T\> | N/A |
| Rust VecDeque | `front()` | `back()` | 返回 Option | Option\<&T\> | N/A |
| Java Deque | `getFirst` | `getLast` | `peekFirst` | copy | N/A |
| C++ vector | `front()` | `back()` | N/A (UB) | T& | N/A |

**关键洞察:**

1. **栈语义** (`Peek`/`Pop`) 与 **序列语义** (`First`/`Last` 或 `Front`/`Back`) 是两套独立的概念命名
2. **`Peek` 的含义因上下文而异**：栈中指顶部 (尾部)，队列中指头部
3. 对于**通用列表**，`First`/`Last` 或 `Front`/`Back` 更自明
4. **Try 模式** 是 C# 生态的标准做法 (`TryPeek`, `TryPop`, `TryGetValue`)
5. **`ref` 返回**是 C# 高性能值类型操作的关键特性

---

## 2. 决策阶段

### 2.1 可行设计方向

#### 方向 A: 纯序列语义 (First/Last)

```csharp
ref T First()
bool TryFirst(out T item)

ref T Last()
bool TryLast(out T item)

// 栈操作保留，但不与查看操作混用
T Pop()
bool TryPop(out T item)
```

**评估:**

| 维度 | 评分 | 说明 |
|------|------|------|
| 一致性 | ⭐⭐⭐⭐⭐ | `First`/`Last` 完全对称 |
| 自明性 | ⭐⭐⭐⭐⭐ | 名称直接表达位置 |
| 完备性 | ⭐⭐⭐⭐ | 异常版 + Try 版齐全 |
| C# 契合度 | ⭐⭐⭐⭐ | 与 LINQ 命名一致 |
| 破坏性 | 中等 | 删除 `Peek`，可能影响现有使用 |

---

#### 方向 B: 双端队列语义 (Front/Back)

```csharp
ref T Front()
bool TryFront(out T item)

ref T Back()
bool TryBack(out T item)

// 栈操作
T Pop()          // 别名 PopBack
bool TryPop(out T item)
```

**评估:**

| 维度 | 评分 | 说明 |
|------|------|------|
| 一致性 | ⭐⭐⭐⭐⭐ | `Front`/`Back` 完全对称 |
| 自明性 | ⭐⭐⭐⭐ | 对熟悉 C++/Rust 的用户友好 |
| 完备性 | ⭐⭐⭐⭐ | 异常版 + Try 版齐全 |
| C# 契合度 | ⭐⭐⭐ | 非 C# 惯用命名 |
| 破坏性 | 高 | 需重命名多个方法 |

---

#### 方向 C: 混合语义 (保留 Peek + 补充 TryFirst)

```csharp
// 序列语义
ref T First()
bool TryFirst(out T item)

ref T Last()
bool TryLast(out T item)

// 栈语义（别名，标记 [EditorBrowsable(Never)]）
[EditorBrowsable(EditorBrowsableState.Never)]
ref T Peek()    // => Last()
[EditorBrowsable(EditorBrowsableState.Never)]
bool TryPeek(out T item)  // => TryLast()

// 栈操作
T Pop()
bool TryPop(out T item)
```

**评估:**

| 维度 | 评分 | 说明 |
|------|------|------|
| 一致性 | ⭐⭐⭐ | 两套命名并存，有冗余 |
| 自明性 | ⭐⭐⭐⭐ | 主推 `First`/`Last`，老用户仍可用 `Peek` |
| 完备性 | ⭐⭐⭐⭐⭐ | 最齐全 |
| C# 契合度 | ⭐⭐⭐⭐ | 渐进迁移，兼容性好 |
| 破坏性 | 低 | 无 breaking change，仅标记废弃 |

---

#### 方向 D: Ref 返回 + Nullable 值返回 双轨制

```csharp
// Ref 返回版（高性能，可能抛异常）
ref T First()
ref T Last()

// Nullable 值返回版（安全，无异常）
T? FirstOrDefault()
T? LastOrDefault()

// Try 模式（兼容不支持 Nullable 的场景）
bool TryFirst(out T item)
bool TryLast(out T item)
```

**评估:**

| 维度 | 评分 | 说明 |
|------|------|------|
| 一致性 | ⭐⭐⭐⭐ | 三种访问风格并存但各有场景 |
| 自明性 | ⭐⭐⭐⭐ | LINQ 用户熟悉 `OrDefault` |
| 完备性 | ⭐⭐⭐⭐⭐ | 覆盖所有使用场景 |
| C# 契合度 | ⭐⭐⭐⭐⭐ | 与 LINQ 高度一致 |
| 破坏性 | 中等 | 删除 `Peek`/`TryPeek` |

---

### 2.2 决策：选择方向 A (纯序列语义) + 可选兼容别名

**理由:**

1. **语义清晰性优先**: `StructList<T>` 是通用列表，不是专用栈。`First`/`Last` 直接表达"访问第几个元素"的语义，无歧义。

2. **与 LINQ 生态一致**: C# 开发者对 `First()`/`Last()` 有天然认知，迁移成本低。

3. **完备对称**: `First`/`TryFirst` 与 `Last`/`TryLast` 完全对称，API 表面积最小化。

4. **避免冗余**: 删除 `Peek` 作为 `Last` 的别名，减少认知负担。`Pop`/`TryPop` 保留，因为它们有**移除**语义，与 `Last` (只读查看) 本质不同。

5. **渐进迁移**: 可选择在过渡期保留 `Peek`/`TryPeek` 作为 `[Obsolete]` 别名。

**补充说明 - 为何不选其他方向:**

- **方向 B (Front/Back)**: 虽然 C++ 用户熟悉，但与 C# 生态不匹配，且 `Front` 容易与 UI 概念混淆。
- **方向 C (混合)**: 保留冗余增加维护成本，新用户仍会困惑。
- **方向 D (双轨制)**: `XxxOrDefault` 返回 `T?`，但 `StructList` 的核心优势是 `ref` 返回避免拷贝；引入 `T?` 反而增加复杂度。

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
public readonly ref T First();

/// <summary>
/// 尝试获取第一个元素的值。
/// </summary>
/// <param name="item">如果列表非空，则为第一个元素的值；否则为 <c>default(T)</c>。</param>
/// <returns>如果列表非空则为 <c>true</c>；否则为 <c>false</c>。</returns>
public readonly bool TryFirst(out T item);

/// <summary>
/// 返回对最后一个元素的引用。
/// </summary>
/// <returns>最后一个元素的引用。</returns>
/// <exception cref="InvalidOperationException">列表为空时抛出。</exception>
public readonly ref T Last();

/// <summary>
/// 尝试获取最后一个元素的值。
/// </summary>
/// <param name="item">如果列表非空，则为最后一个元素的值；否则为 <c>default(T)</c>。</param>
/// <returns>如果列表非空则为 <c>true</c>；否则为 <c>false</c>。</returns>
public readonly bool TryLast(out T item);

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
| `TryFirst(out T)` | **新增** | 头部访问的安全版本 |
| `Last()` | **修改** | 原为 `=> ref Peek()`，改为独立实现 |
| `TryLast(out T)` | **新增** | 尾部访问的安全版本 |
| `Peek()` | **删除** | 被 `Last()` 取代 |
| `TryPeek(out T)` | **删除** | 被 `TryLast(out T)` 取代 |
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
/// 尝试获取第一个元素的值。
/// </summary>
/// <param name="item">如果列表非空，则为第一个元素的值；否则为 <c>default(T)</c>。</param>
/// <returns>如果列表非空则为 <c>true</c>；否则为 <c>false</c>。</returns>
public readonly bool TryFirst(out T item) {
    if (_count == 0) {
        item = default!;
        return false;
    }
    item = _items[0];
    return true;
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

/// <summary>
/// 尝试获取最后一个元素的值。
/// </summary>
/// <param name="item">如果列表非空，则为最后一个元素的值；否则为 <c>default(T)</c>。</param>
/// <returns>如果列表非空则为 <c>true</c>；否则为 <c>false</c>。</returns>
public readonly bool TryLast(out T item) {
    if (_count == 0) {
        item = default!;
        return false;
    }
    item = _items[_count - 1];
    return true;
}

#endregion
```

### 3.4 迁移指南

#### Breaking Changes

| 旧 API | 新 API | 迁移方式 |
|--------|--------|----------|
| `Peek()` | `Last()` | 直接替换，语义相同 |
| `TryPeek(out T item)` | `TryLast(out T item)` | 直接替换，语义相同 |

#### 迁移步骤

1. **全局搜索替换**:
   - `\.Peek\(\)` → `.Last()`
   - `\.TryPeek\(` → `.TryLast(`

2. **编译验证**: 替换后重新编译，确保无错误。

3. **代码审查**: 检查替换后的代码是否语义正确（`Peek` 和 `Last` 语义相同，通常无问题）。

#### 可选：渐进式迁移（保留废弃别名）

如果需要平滑迁移，可临时保留废弃方法：

```csharp
/// <summary>查看最后一个元素但不移除。</summary>
/// <remarks>此方法已废弃，请使用 <see cref="Last()"/>。</remarks>
[Obsolete("Use Last() instead.", error: false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public readonly ref T Peek() => ref Last();

/// <summary>尝试查看最后一个元素但不移除。</summary>
/// <remarks>此方法已废弃，请使用 <see cref="TryLast(out T)"/>。</remarks>
[Obsolete("Use TryLast() instead.", error: false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public readonly bool TryPeek(out T item) => TryLast(out item);
```

---

## 4. 附录

### 4.1 设计原则总结

1. **命名应表达位置，而非操作模式**: `First`/`Last` 直接表达"第一个/最后一个"，而 `Peek` 需要上下文才能理解指向哪一端。

2. **对称性优先**: API 表面应对称设计。有 `First` 就应有 `TryFirst`；有 `Last` 就应有 `TryLast`。

3. **区分查看与移除**: `First`/`Last` 是只读查看；`Pop` 是移除操作。两者命名风格不同是正确的，因为语义本就不同。

4. **Try 模式是 C# 惯例**: 遵循 `TryGetValue`, `TryParse`, `TryPeek` 等标准模式。

5. **Ref 返回是性能关键**: `StructList<T>` 的核心价值在于避免值类型拷贝，`ref` 返回必须保留。

### 4.2 未来扩展考虑

如果将来需要支持双端操作（类似 `Deque`），可考虑添加：

```csharp
// 头部移除
T PopFirst();
bool TryPopFirst(out T item);

// 头部添加
void Prepend(in T item);
```

但这超出当前设计范围，仅作记录。

---

**文档结束**
