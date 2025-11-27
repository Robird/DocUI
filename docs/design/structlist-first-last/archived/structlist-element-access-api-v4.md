````markdown
# StructList\<T\> 元素访问 API 设计方案 v4

> **文档状态**: 提案
> **创建日期**: 2025-11-27
> **基于版本**: v3 设计评审
> **作者**: API 设计评审

---

## 1. 分析阶段

### 1.1 现有问题回顾

当前 `StructList<T>` 的元素访问 API 存在以下问题：

```csharp
// 当前实现
public readonly ref T Peek()        // 访问尾部，抛异常
public readonly bool TryPeek(out T) // 访问尾部，安全版
public readonly ref T First()       // 访问头部，抛异常
public readonly ref T Last() => ref Peek();  // 尾部别名
```

| 问题类型 | 具体描述 | 严重程度 |
|----------|----------|----------|
| **冗余** | `Last()` 是 `Peek()` 的纯别名，两者功能完全重叠 | 中 |
| **功能缺失** | 有 `TryPeek` (尾部) 但没有 `TryFirst` (头部) | 高 |
| **命名不一致** | `Peek` 暗示栈语义 (LIFO)，而 `First`/`Last` 暗示序列语义 | 高 |
| **自明性差** | 用户无法直观判断 `Peek` 访问的是头部还是尾部 | 高 |

### 1.2 主流语言/库的类似设计

#### 1.2.1 C# BCL (System.Collections.Generic)

**List\<T\> - 标准动态数组:**
```csharp
// 无内置 First/Last 方法，依赖索引访问
list[0]                    // 第一个元素
list[list.Count - 1]       // 最后一个元素
list[^1]                   // C# 8+ Index 语法（^1 表示倒数第一个）
```

**Span\<T\> / ReadOnlySpan\<T\> - 高性能视图:**
```csharp
span[0]                    // 第一个元素
span[^1]                   // 最后一个元素
// 无专门方法，完全依赖索引语法
```

**Stack\<T\> - 后进先出集合:**
```csharp
T Peek()                   // 查看栈顶（最后压入的），抛 InvalidOperationException
bool TryPeek(out T result) // .NET 6+ 新增，安全版本
T Pop()                    // 弹出栈顶
bool TryPop(out T result)  // .NET 6+ 新增
void Push(T item)          // 压入
```

**Queue\<T\> - 先进先出集合:**
```csharp
T Peek()                   // 查看队首（最先入队的），抛 InvalidOperationException
bool TryPeek(out T result) // .NET 6+ 新增
T Dequeue()                // 出队
bool TryDequeue(out T result)
void Enqueue(T item)       // 入队
```

**PriorityQueue\<TElement, TPriority\> - .NET 6+ 优先队列:**
```csharp
TElement Peek()                    // 查看最高优先级元素
bool TryPeek(out TElement element, out TPriority priority)
TElement Dequeue()
bool TryDequeue(out TElement element, out TPriority priority)
```

**ImmutableStack\<T\> / ImmutableQueue\<T\>:**
```csharp
T Peek()                   // 返回值拷贝（不可变类型无 ref 返回需求）
// 无 TryPeek，因为 Peek 在空时抛异常是设计选择
```

| 特性 | BCL 设计特点 |
|------|-------------|
| 命名惯例 | `Peek` 专用于栈/队列语义；通用集合依赖索引 |
| 返回类型 | 返回值拷贝（`T`），不返回引用 |
| 异常处理 | 异常版 (`Peek`) + Try 模式 (`TryPeek`) 并存 |
| 一致性 | `Stack.Peek` = 尾部；`Queue.Peek` = 头部（语义绑定到数据结构） |

---

#### 1.2.2 C# LINQ (System.Linq)

```csharp
// 扩展方法，定义在 Enumerable 类上
T First()                          // 抛 InvalidOperationException if empty
T First(Func<T, bool> predicate)   // 带谓词筛选
T FirstOrDefault()                 // 空时返回 default(T)
T FirstOrDefault(T defaultValue)   // .NET 6+ 可指定默认值
T? FirstOrDefault()                // 对于引用类型返回 null

T Last()                           // 抛异常版
T Last(Func<T, bool> predicate)
T LastOrDefault()
T LastOrDefault(T defaultValue)

T Single()                         // 必须恰好一个元素
T SingleOrDefault()

T ElementAt(int index)             // 按索引访问
T ElementAtOrDefault(int index)
T ElementAt(Index index)           // .NET 6+ 支持 ^1 语法
```

| 特性 | LINQ 设计特点 |
|------|--------------|
| 命名惯例 | `First`/`Last`/`Single` + `OrDefault` 后缀 |
| 返回类型 | 值拷贝（LINQ 设计时 C# 还不支持 ref 返回） |
| 异常处理 | 异常版 + `OrDefault` 版（不是 Try 模式！） |
| 性能 | `Last()` 对非 `IList<T>` 是 O(n) 遍历 |

**重要区别**: LINQ 用 `OrDefault` 而非 `TryXxx`，因为：
- `OrDefault` 更符合函数式风格（直接返回值）
- 适合链式调用 `.Where(...).FirstOrDefault()?.DoSomething()`
- Try 模式需要 `out` 参数，无法链式调用

---

#### 1.2.3 Rust (std::vec::Vec, std::collections::VecDeque)

**Vec\<T\> (标准动态数组):**
```rust
fn first(&self) -> Option<&T>          // 返回第一个元素的不可变引用
fn first_mut(&mut self) -> Option<&mut T>  // 返回可变引用
fn last(&self) -> Option<&T>
fn last_mut(&mut self) -> Option<&mut T>

fn pop(&mut self) -> Option<T>         // 移除并返回最后一个元素
fn push(&mut self, value: T)           // 添加到末尾

// 无 Peek！因为 last() 已经是只读访问
```

**VecDeque\<T\> (双端队列):**
```rust
fn front(&self) -> Option<&T>          // 队首
fn front_mut(&mut self) -> Option<&mut T>
fn back(&self) -> Option<&T>           // 队尾
fn back_mut(&mut self) -> Option<&mut T>

fn pop_front(&mut self) -> Option<T>   // 移除队首
fn pop_back(&mut self) -> Option<T>    // 移除队尾
fn push_front(&mut self, value: T)
fn push_back(&mut self, value: T)
```

**slice (数组切片 [T]):**
```rust
fn first(&self) -> Option<&T>
fn last(&self) -> Option<&T>
fn get(&self, index: usize) -> Option<&T>  // 安全索引访问
slice[index]                               // 不安全索引，panic if OOB
```

| 特性 | Rust 设计特点 |
|------|--------------|
| 命名惯例 | 线性容器用 `first`/`last`；双端队列用 `front`/`back` |
| 返回类型 | `Option<&T>` / `Option<&mut T>` 显式区分可变性 |
| 异常处理 | 无异常！一律通过 `Option` 处理空情况 |
| 一致性 | 极高，所有安全访问都返回 `Option` |

**Rust 的关键洞察**:
- `first()`/`last()` 用于**查看**，返回 `Option<&T>`
- `pop()` 用于**移除**，返回 `Option<T>`（消耗所有权）
- 没有 `Peek`，因为 `last()` 就是查看语义
- 可变性在类型签名中显式表达 (`&T` vs `&mut T`)

---

#### 1.2.4 Java (java.util)

**ArrayList\<E\> (动态数组):**
```java
E get(int index)                       // 抛 IndexOutOfBoundsException
// Java 21+ SequencedCollection 接口新增:
E getFirst()                           // 抛 NoSuchElementException if empty
E getLast()
E removeFirst()                        // 移除并返回
E removeLast()
void addFirst(E e)
void addLast(E e)
```

**Deque\<E\> 接口 (LinkedList, ArrayDeque 实现):**
```java
// 抛异常版
E getFirst()                           // NoSuchElementException if empty
E getLast()
E removeFirst()
E removeLast()
void addFirst(E e)
void addLast(E e)

// 返回 null 版（仅适用于引用类型）
E peekFirst()                          // null if empty
E peekLast()
E pollFirst()                          // 移除并返回，null if empty
E pollLast()
boolean offerFirst(E e)                // 添加，返回是否成功
boolean offerLast(E e)
```

**Stack\<E\> (已过时，推荐用 Deque):**
```java
E peek()                               // 栈顶，抛 EmptyStackException
E pop()
E push(E item)
```

| 特性 | Java 设计特点 |
|------|--------------|
| 命名惯例 | `getFirst`/`getLast` (抛异常) vs `peekFirst`/`peekLast` (返回 null) |
| 返回类型 | 对象引用（Java 无值类型 ref 返回） |
| 异常处理 | 双轨制：异常版 + null 返回版 |
| 问题 | null 语义对值类型不友好（Java 用包装类绕过） |

**Java 的 Peek 命名**: `peekFirst`/`peekLast` 显式标注访问哪一端！这比 C# 的 `Peek` 更清晰。

---

#### 1.2.5 C++ STL (std::vector, std::deque)

**std::vector\<T\>:**
```cpp
reference front();                     // 返回首元素引用，UB if empty
const_reference front() const;
reference back();                      // 返回尾元素引用，UB if empty
const_reference back() const;

reference at(size_type pos);           // 有边界检查，抛 std::out_of_range
reference operator[](size_type pos);   // 无边界检查，UB if OOB

void pop_back();                       // 移除尾元素，无返回值！UB if empty
void push_back(const T& value);
void push_back(T&& value);             // C++11 移动语义
```

**std::deque\<T\>:**
```cpp
reference front();
reference back();
void pop_front();                      // 无返回值
void pop_back();
void push_front(const T& value);
void push_back(const T& value);
```

**std::list\<T\> (双向链表):**
```cpp
reference front();
reference back();
// 同 deque
```

| 特性 | C++ STL 设计特点 |
|------|-----------------|
| 命名惯例 | `front`/`back` 表示两端（不是 `first`/`last`！） |
| 返回类型 | 返回引用 `T&` / `const T&` |
| 异常处理 | `front`/`back`/`pop_back` 是 UB if empty；`at` 抛异常 |
| 特殊设计 | `pop_back()` **不返回值**！需先 `back()` 取值再 `pop_back()` |

**C++ 的 pop 不返回值的原因**:
- 历史上考虑异常安全（拷贝构造可能抛异常时，已弹出但拷贝失败会丢失元素）
- C++11 移动语义后这个理由减弱，但 ABI 兼容性导致无法改变

---

#### 1.2.6 Python (list)

```python
lst[0]       # 第一个元素，抛 IndexError if empty
lst[-1]      # 最后一个元素（负索引语法）
lst.pop()    # 移除并返回最后一个元素，抛 IndexError if empty
lst.pop(0)   # 移除并返回第一个元素
# 无 peek！直接用 lst[-1] 查看
```

| 特性 | Python 设计特点 |
|------|----------------|
| 命名惯例 | 无专门方法，依赖负索引语法 |
| 返回类型 | 对象引用 |
| 异常处理 | 全部抛 `IndexError`，无安全版本 |
| 特殊语法 | 负索引 `[-1]` 是语言级别的特性 |

---

### 1.3 设计要素对比矩阵

| 语言/库 | 头部访问 | 尾部访问 | 安全访问方式 | 返回类型 | 移除操作 |
|---------|----------|----------|--------------|----------|----------|
| **C# List** | `[0]` | `[^1]` | N/A | ref (索引器) | `RemoveAt(0)` |
| **C# Stack** | N/A | `Peek()` | `TryPeek()` | T (copy) | `Pop()` |
| **C# Queue** | `Peek()` | N/A | `TryPeek()` | T (copy) | `Dequeue()` |
| **C# LINQ** | `First()` | `Last()` | `FirstOrDefault()` | T (copy) | N/A |
| **Rust Vec** | `first()` | `last()` | 返回 `Option` | `Option<&T>` | `pop()` |
| **Rust VecDeque** | `front()` | `back()` | 返回 `Option` | `Option<&T>` | `pop_front/back()` |
| **Java Deque** | `getFirst()` | `getLast()` | `peekFirst()` (null) | E (ref) | `removeFirst()` |
| **C++ vector** | `front()` | `back()` | `at()` (抛异常) | T& | `pop_back()` (无返回) |
| **Python list** | `[0]` | `[-1]` | N/A | ref | `pop()` |

### 1.4 关键洞察总结

1. **命名语义区分**:
   - `Peek` = 栈/队列专用，访问"操作端"（栈顶/队首）
   - `First`/`Last` = 序列语义，访问"位置"
   - `Front`/`Back` = 双端队列语义，访问"两端"

2. **`Peek` 的歧义性**:
   - C# Stack.Peek = 尾部
   - C# Queue.Peek = 头部
   - 对于**通用列表**，`Peek` 的含义模糊

3. **安全访问模式**:
   - C# BCL: `TryXxx(out T)` 模式
   - C# LINQ: `XxxOrDefault()` 模式（返回值，无 out 参数）
   - Rust: `Option<T>` 类型系统强制处理
   - Java: 返回 null 或抛异常

4. **Ref 返回的独特性**:
   - C# 7.0+ 支持 `ref` 返回，高性能值类型操作的关键
   - Rust 天然支持引用返回
   - Java/Python 无此概念
   - `TryXxx` 模式与 `ref` 返回不兼容（`out` 参数无法返回引用）

5. **移除操作的命名一致性**:
   - 返回移除值: `Pop()` (C#/Python)、`pop()` (Rust)、`poll`/`remove` (Java)
   - 不返回值: `pop_back()` (C++)

---

## 2. 决策阶段

### 2.1 设计方向候选

#### 方向 A: 纯序列语义 (First/Last 系列)

**API 设计:**
```csharp
// 异常版 - ref 返回
ref T First()
ref T Last()

// 安全版 - Try 模式
bool TryFirst(out T item)
bool TryLast(out T item)

// 移除操作 - 保留现有
T Pop()
bool TryPop(out T item)
```

**评估:**

| 维度 | 评分 | 详细说明 |
|------|------|----------|
| 一致性 | ★★★★★ | `First`/`Last` 完全对称 |
| 自明性 | ★★★★★ | 名称直接表达位置语义 |
| 完备性 | ★★★★☆ | 缺少 `FirstOrDefault` 风格 |
| C# 生态契合度 | ★★★★☆ | 与 LINQ 命名一致，但 LINQ 用 `OrDefault` 不用 `Try` |
| 破坏性变更 | 中等 | 删除 `Peek`/`TryPeek` |
| 学习曲线 | 低 | C# 开发者熟悉 `First`/`Last` |

---

#### 方向 B: 双端队列语义 (Front/Back 系列)

**API 设计:**
```csharp
// 异常版 - ref 返回
ref T Front()
ref T Back()

// 安全版 - Try 模式
bool TryFront(out T item)
bool TryBack(out T item)

// 移除操作
T Pop()       // = PopBack 的简写
T PopBack()   // 显式命名
T PopFront()  // 头部移除
bool TryPop(out T item)
bool TryPopFront(out T item)
```

**评估:**

| 维度 | 评分 | 详细说明 |
|------|------|----------|
| 一致性 | ★★★★★ | `Front`/`Back` 完全对称 |
| 自明性 | ★★★★☆ | 熟悉 C++/Rust 的用户友好，但 C# 用户可能陌生 |
| 完备性 | ★★★★★ | 支持双端操作 |
| C# 生态契合度 | ★★☆☆☆ | C# BCL 不使用 `Front`/`Back` 命名 |
| 破坏性变更 | 高 | 需重命名多个方法 |
| 学习曲线 | 中等 | C# 开发者需适应新命名 |

---

#### 方向 C: 混合模式 (保留兼容性)

**API 设计:**
```csharp
// 主推 API - 序列语义
ref T First()
bool TryFirst(out T item)
ref T Last()
bool TryLast(out T item)

// 废弃但保留 - 栈语义
[Obsolete("Use Last() instead.")]
ref T Peek() => ref Last();

[Obsolete("Use TryLast() instead.")]
bool TryPeek(out T item) => TryLast(out item);

// 移除操作
T Pop()
bool TryPop(out T item)
```

**评估:**

| 维度 | 评分 | 详细说明 |
|------|------|----------|
| 一致性 | ★★★☆☆ | 两套命名并存，有冗余 |
| 自明性 | ★★★★☆ | 新用户看到 `First`/`Last`，老用户仍可用 `Peek` |
| 完备性 | ★★★★★ | 最全面 |
| C# 生态契合度 | ★★★★☆ | 渐进迁移，兼容性好 |
| 破坏性变更 | 无 | 仅添加废弃标记 |
| 学习曲线 | 低 | 无需立即迁移 |

---

#### 方向 D: LINQ 风格 (First/Last + OrDefault)

**API 设计:**
```csharp
// 异常版 - ref 返回
ref T First()
ref T Last()

// OrDefault 版 - 值返回（无法返回 ref）
T FirstOrDefault()
T FirstOrDefault(T defaultValue)
T LastOrDefault()
T LastOrDefault(T defaultValue)

// 保留 Try 模式作为补充
bool TryFirst(out T item)
bool TryLast(out T item)

// 移除操作
T Pop()
bool TryPop(out T item)
```

**评估:**

| 维度 | 评分 | 详细说明 |
|------|------|----------|
| 一致性 | ★★★★☆ | 三种风格并存但各有明确场景 |
| 自明性 | ★★★★★ | `OrDefault` 语义对 LINQ 用户极其熟悉 |
| 完备性 | ★★★★★ | 覆盖所有使用场景 |
| C# 生态契合度 | ★★★★★ | 与 LINQ 高度一致 |
| 破坏性变更 | 中等 | 删除 `Peek`/`TryPeek` |
| 学习曲线 | 最低 | 直接复用 LINQ 经验 |

**关键问题**: `OrDefault` 返回值拷贝，与 `ref` 返回的性能目标冲突。但这是可接受的，因为：
- 需要 `ref` 返回的高性能场景使用 `First()`/`Last()`
- 需要安全访问的场景使用 `TryFirst()`/`TryLast()` 或 `FirstOrDefault()`

---

### 2.2 最终决策

**选择: 方向 A (纯序列语义) + 方向 D 的 OrDefault 扩展**

**综合方案:**
```csharp
// 核心 API (异常版 + ref 返回)
ref T First()           // 高性能访问首元素
ref T Last()            // 高性能访问尾元素

// 安全 API (Try 模式)
bool TryFirst(out T item)    // 安全访问首元素
bool TryLast(out T item)     // 安全访问尾元素

// 移除操作
T Pop()                      // 移除并返回尾元素
bool TryPop(out T item)      // 安全版移除
```

**可选扩展 (LINQ 兼容层):**
```csharp
// 如果未来需要更好的 LINQ 兼容性，可添加：
T FirstOrDefault()
T FirstOrDefault(T defaultValue)
T LastOrDefault()
T LastOrDefault(T defaultValue)
```

### 2.3 决策理由

1. **语义清晰**: `StructList<T>` 是通用值类型列表，不是栈。`First`/`Last` 直接表达位置语义，无歧义。

2. **与 LINQ 一致**: C# 开发者对 `First()`/`Last()` 有天然认知。

3. **完备对称**: `First`/`TryFirst` 与 `Last`/`TryLast` 完全对称。

4. **性能优先**: 保留 `ref` 返回版本用于高性能场景。

5. **避免冗余**: 删除 `Peek` 作为 `Last` 的别名。`Pop` 保留因为它是**移除**操作，与 `Last`（只读）本质不同。

6. **Try 模式优于 OrDefault**:
   - `TryFirst(out T)` 可以区分"空列表返回 default"和"首元素恰好是 default"
   - 与 C# BCL 的 `Stack.TryPeek`、`Dictionary.TryGetValue` 一致
   - 保留 `OrDefault` 作为可选扩展

### 2.4 为何不选择其他方向

| 方向 | 否决理由 |
|------|----------|
| B (Front/Back) | 与 C# 生态命名习惯不符。C# BCL 从不使用 `Front`/`Back`，只有 C++/Rust 使用。 |
| C (混合模式) | 保留废弃 API 增加维护成本，新用户仍会困惑"该用 `Peek` 还是 `Last`"。 |
| 纯 D (OrDefault) | `OrDefault` 必须返回值拷贝，与 `StructList` 的 `ref` 返回性能目标冲突。 |

---

## 3. 目标设计

### 3.1 完整方法签名

```csharp
#region 首尾元素访问

/// <summary>
/// 返回对第一个元素的引用。
/// </summary>
/// <returns>第一个元素的引用。</returns>
/// <exception cref="InvalidOperationException">列表为空时抛出。</exception>
/// <remarks>
/// 此方法返回 <c>ref T</c>，允许直接修改元素而无需拷贝。
/// 对于只读访问，调用方应使用 <c>ref readonly</c> 接收。
/// </remarks>
/// <example>
/// <code>
/// ref var first = ref list.First();
/// first.Value = 42; // 直接修改
/// </code>
/// </example>
public readonly ref T First();

/// <summary>
/// 尝试获取第一个元素。
/// </summary>
/// <param name="item">
/// 当列表非空时，输出第一个元素的值；否则输出 <c>default(T)</c>。
/// </param>
/// <returns>如果列表非空则为 <see langword="true"/>；否则为 <see langword="false"/>。</returns>
/// <remarks>
/// 此方法返回元素的拷贝而非引用。如需引用访问，请先检查 <see cref="IsEmpty"/>
/// 后使用 <see cref="First()"/>。
/// </remarks>
public readonly bool TryFirst(out T item);

/// <summary>
/// 返回对最后一个元素的引用。
/// </summary>
/// <returns>最后一个元素的引用。</returns>
/// <exception cref="InvalidOperationException">列表为空时抛出。</exception>
/// <remarks>
/// 此方法返回 <c>ref T</c>，允许直接修改元素而无需拷贝。
/// 对于只读访问，调用方应使用 <c>ref readonly</c> 接收。
/// </remarks>
/// <example>
/// <code>
/// ref var last = ref list.Last();
/// last.Value = 42; // 直接修改
/// </code>
/// </example>
public readonly ref T Last();

/// <summary>
/// 尝试获取最后一个元素。
/// </summary>
/// <param name="item">
/// 当列表非空时，输出最后一个元素的值；否则输出 <c>default(T)</c>。
/// </param>
/// <returns>如果列表非空则为 <see langword="true"/>；否则为 <see langword="false"/>。</returns>
/// <remarks>
/// 此方法返回元素的拷贝而非引用。如需引用访问，请先检查 <see cref="IsEmpty"/>
/// 后使用 <see cref="Last()"/>。
/// </remarks>
public readonly bool TryLast(out T item);

#endregion

#region 尾部移除操作

/// <summary>
/// 移除并返回最后一个元素。
/// </summary>
/// <returns>被移除的元素。</returns>
/// <exception cref="InvalidOperationException">列表为空时抛出。</exception>
/// <remarks>
/// 此操作等效于 <c>var item = list.Last(); list.RemoveAt(list.Count - 1); return item;</c>
/// 但效率更高。如果 <typeparamref name="T"/> 是引用类型或包含引用的值类型，
/// 被移除位置会被清理以避免内存泄漏。
/// </remarks>
public T Pop();

/// <summary>
/// 尝试移除并返回最后一个元素。
/// </summary>
/// <param name="item">
/// 当列表非空时，输出被移除的元素；否则输出 <c>default(T)</c>。
/// </param>
/// <returns>如果成功移除则为 <see langword="true"/>；否则为 <see langword="false"/>。</returns>
public bool TryPop(out T item);

#endregion
```

### 3.2 变更清单

| 方法签名 | 变更类型 | 说明 |
|----------|----------|------|
| `ref T First()` | ✅ **保留** | 无变化 |
| `bool TryFirst(out T item)` | ✨ **新增** | 首元素的安全访问版本 |
| `ref T Last()` | ✏️ **修改** | 原实现为 `=> ref Peek()`，改为独立实现 |
| `bool TryLast(out T item)` | ✨ **新增** | 尾元素的安全访问版本 |
| `ref T Peek()` | ❌ **删除** | 被 `Last()` 取代，语义完全相同 |
| `bool TryPeek(out T item)` | ❌ **删除** | 被 `TryLast(out T item)` 取代 |
| `T Pop()` | ✅ **保留** | 无变化 |
| `bool TryPop(out T item)` | ✅ **保留** | 无变化 |

### 3.3 实现代码

```csharp
#region 首尾元素访问

/// <summary>
/// 返回对第一个元素的引用。
/// </summary>
/// <exception cref="InvalidOperationException">列表为空时抛出。</exception>
public readonly ref T First() {
    if (_count == 0)
        ThrowEmptyList();
    return ref _items[0];
}

/// <summary>
/// 尝试获取第一个元素。
/// </summary>
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
/// <exception cref="InvalidOperationException">列表为空时抛出。</exception>
public readonly ref T Last() {
    if (_count == 0)
        ThrowEmptyList();
    return ref _items[_count - 1];
}

/// <summary>
/// 尝试获取最后一个元素。
/// </summary>
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

#### 3.4.1 Breaking Changes

本次变更删除了 `Peek()` 和 `TryPeek()` 方法，这是 **破坏性变更**。

| 旧 API | 新 API | 语义变化 |
|--------|--------|----------|
| `list.Peek()` | `list.Last()` | 无变化（语义完全相同） |
| `list.TryPeek(out var x)` | `list.TryLast(out var x)` | 无变化（语义完全相同） |

#### 3.4.2 自动迁移脚本

**PowerShell 正则替换:**
```powershell
# 在项目根目录执行
Get-ChildItem -Recurse -Include *.cs | ForEach-Object {
    $content = Get-Content $_.FullName -Raw
    $content = $content -replace '\.Peek\(\)', '.Last()'
    $content = $content -replace '\.TryPeek\(', '.TryLast('
    Set-Content $_.FullName $content -NoNewline
}
```

**sed 替换 (Unix/Git Bash):**
```bash
find . -name "*.cs" -exec sed -i 's/\.Peek()/.Last()/g; s/\.TryPeek(/.TryLast(/g' {} \;
```

#### 3.4.3 手动迁移步骤

1. **全局搜索**: 在 IDE 中搜索 `\.Peek\(` 和 `\.TryPeek\(`
2. **逐个替换**:
   - `Peek()` → `Last()`
   - `TryPeek(` → `TryLast(`
3. **编译验证**: 确保无编译错误
4. **运行测试**: 验证行为不变

#### 3.4.4 可选: 渐进式迁移

如需平滑迁移，可临时保留废弃方法（1-2 个版本后删除）：

```csharp
/// <inheritdoc cref="Last()"/>
[Obsolete("Use Last() instead. This method will be removed in a future version.", error: false)]
[EditorBrowsable(EditorBrowsableState.Never)]
[ExcludeFromCodeCoverage]
public readonly ref T Peek() => ref Last();

/// <inheritdoc cref="TryLast(out T)"/>
[Obsolete("Use TryLast() instead. This method will be removed in a future version.", error: false)]
[EditorBrowsable(EditorBrowsableState.Never)]
[ExcludeFromCodeCoverage]
public readonly bool TryPeek(out T item) => TryLast(out item);
```

---

## 4. 附录

### 4.1 设计原则总结

| 原则 | 说明 |
|------|------|
| **位置优于操作模式** | `First`/`Last` 直接表达位置，`Peek` 需要上下文理解 |
| **对称性优先** | `First`/`TryFirst` 与 `Last`/`TryLast` 完全对称 |
| **查看与移除分离** | `Last()` 是只读查看，`Pop()` 是移除操作，命名风格不同是正确的 |
| **Try 模式是 BCL 惯例** | 遵循 `TryGetValue`、`TryParse`、`TryPeek` 模式 |
| **Ref 返回是性能关键** | 避免值类型拷贝是 `StructList<T>` 的核心价值 |

### 4.2 与 .NET BCL 的对齐

| BCL 类型 | 对应 StructList API | 说明 |
|----------|---------------------|------|
| `Stack<T>.Peek()` | `Last()` | 都访问尾部 |
| `Stack<T>.TryPeek()` | `TryLast()` | 都是安全版本 |
| `Stack<T>.Pop()` | `Pop()` | 完全一致 |
| `Stack<T>.TryPop()` | `TryPop()` | 完全一致 |
| `LINQ.First()` | `First()` | 命名一致，但返回 ref |
| `LINQ.Last()` | `Last()` | 命名一致，但返回 ref |

### 4.3 未来扩展预留

如果未来需要支持双端操作，可考虑添加：

```csharp
// 头部移除
T PopFirst();
bool TryPopFirst(out T item);

// 头部添加（需要移动所有元素，O(n) 复杂度）
void Prepend(in T item);

// LINQ 兼容扩展
T FirstOrDefault();
T FirstOrDefault(T defaultValue);
T LastOrDefault();
T LastOrDefault(T defaultValue);
```

**注意**: 以上 API 不在本次设计范围内，仅作为扩展点记录。

### 4.4 API 一览表

**最终 API 表面:**

| 方法 | 类型 | 返回 | 空列表行为 | 用途 |
|------|------|------|------------|------|
| `First()` | 查看 | `ref T` | 抛异常 | 高性能首元素访问 |
| `TryFirst(out T)` | 查看 | `bool` | 返回 false | 安全首元素访问 |
| `Last()` | 查看 | `ref T` | 抛异常 | 高性能尾元素访问 |
| `TryLast(out T)` | 查看 | `bool` | 返回 false | 安全尾元素访问 |
| `Pop()` | 移除 | `T` | 抛异常 | 弹出尾元素 |
| `TryPop(out T)` | 移除 | `bool` | 返回 false | 安全弹出尾元素 |

---

**文档结束**
````
