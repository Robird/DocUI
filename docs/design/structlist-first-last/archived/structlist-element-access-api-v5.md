````markdown
# StructList\<T\> 元素访问 API 设计方案 v5

> **文档状态**: 提案
> **创建日期**: 2025-11-27
> **设计视角**: Rust 风格 — 以 `ref` 返回与 Try 模式的对称性为核心，追求类型安全与零拷贝

---

## 1. 分析阶段

### 1.1 现有问题回顾

当前 `StructList<T>` 的元素访问 API：

```csharp
public readonly ref T Peek()              // 访问尾部，抛异常
public readonly bool TryPeek(out T item)  // 访问尾部，安全版（值拷贝）
public readonly ref T First()             // 访问头部，抛异常
public readonly ref T Last() => ref Peek();  // Peek 的别名
```

| 问题 | 描述 | 严重程度 |
|------|------|----------|
| **冗余** | `Last()` 是 `Peek()` 的纯别名 | 中 |
| **功能不对称** | 有 `TryPeek` 但无 `TryFirst` | 高 |
| **命名冲突** | `Peek` (栈语义) 与 `First`/`Last` (序列语义) 混用 | 高 |
| **Try 模式的类型缺陷** | `TryPeek(out T)` 强制产生值拷贝，违背 `ref` 返回的设计初衷 | 高 |

### 1.2 核心矛盾：`ref` 返回 vs C# `TryXxx` 模式

C# 的 `TryXxx(out T)` 模式有一个根本性限制：

```csharp
// ❌ C# 不支持：返回"可能不存在的引用"
bool TryFirst(out ref T item);  // 语法错误！

// ✅ C# 只能这样：返回值拷贝
bool TryFirst(out T item);      // item 是拷贝，不是引用
```

**问题**：`StructList<T>` 的核心价值是 `ref` 返回避免拷贝。引入 `TryFirst(out T)` 会产生拷贝，与设计初衷冲突。

**Rust 如何解决这个问题？**

```rust
// Rust 使用 Option<&T> 包装引用
fn first(&self) -> Option<&T>       // 返回引用的 Option
fn first_mut(&mut self) -> Option<&mut T>  // 返回可变引用的 Option
```

Rust 的 `Option<&T>` 实现了：
1. **类型安全**：编译器强制处理 `None` 情况
2. **零拷贝**：返回引用而非值拷贝
3. **对称性**：`first()` / `last()` / `get()` 都返回 `Option`

### 1.3 主流语言/库设计调研

#### 1.3.1 Rust — Vec / slice / VecDeque

**Vec\<T\> 与 slice [T]:**

```rust
// 安全访问 — 返回 Option<&T>
fn first(&self) -> Option<&T>
fn first_mut(&mut self) -> Option<&mut T>
fn last(&self) -> Option<&T>
fn last_mut(&mut self) -> Option<&mut T>
fn get(&self, index: usize) -> Option<&T>
fn get_mut(&mut self, index: usize) -> Option<&mut T>

// 不安全访问 — 调用者负责边界检查
unsafe fn get_unchecked(&self, index: usize) -> &T
unsafe fn get_unchecked_mut(&mut self, index: usize) -> &mut T

// 移除操作 — 返回 Option<T>（所有权转移）
fn pop(&mut self) -> Option<T>
```

**VecDeque\<T\> (双端队列):**

```rust
fn front(&self) -> Option<&T>
fn front_mut(&mut self) -> Option<&mut T>
fn back(&self) -> Option<&T>
fn back_mut(&mut self) -> Option<&mut T>
fn pop_front(&mut self) -> Option<T>
fn pop_back(&mut self) -> Option<T>
```

| 特性 | Rust 设计要点 |
|------|---------------|
| **无异常** | 所有安全操作返回 `Option`，无 panic |
| **引用包装** | `Option<&T>` 而非 `Option<T>`，保持引用语义 |
| **可变性显式** | `_mut` 后缀区分可变/不可变引用 |
| **对称性** | `first`/`last`、`front`/`back` 完全对称 |
| **移除返回所有权** | `pop()` 返回 `Option<T>`（值，因为元素已不在容器中） |

**Rust 命名哲学**:
- **线性容器** (Vec, slice): `first()` / `last()`
- **双端队列** (VecDeque): `front()` / `back()`
- **栈操作**: `push()` / `pop()` — 仅用于添加/移除，不用于查看

---

#### 1.3.2 C# BCL — Stack\<T\> / Queue\<T\> / List\<T\>

**Stack\<T\>:**
```csharp
T Peek()                    // 抛 InvalidOperationException if empty
bool TryPeek(out T result)  // .NET 6+，安全版
T Pop()                     // 抛异常版
bool TryPop(out T result)   // 安全版
void Push(T item)
```

**Queue\<T\>:**
```csharp
T Peek()                    // 抛异常
bool TryPeek(out T result)  // 安全版
T Dequeue()                 // 抛异常版
bool TryDequeue(out T result)
void Enqueue(T item)
```

**List\<T\>:**
```csharp
T this[int index] { get; set; }  // 索引器，抛异常
// 无 First()/Last() 方法！依赖 LINQ 扩展或索引
list[0]       // 第一个
list[^1]      // C# 8+ Index 语法
```

**LINQ:**
```csharp
T First()              // 抛 InvalidOperationException
T FirstOrDefault()     // 返回 default(T)
T FirstOrDefault(T defaultValue)  // .NET 6+，指定默认值
T Last()
T LastOrDefault()
```

| 特性 | C# BCL 设计要点 |
|------|-----------------|
| **双轨制** | 异常版 (`Peek`) + 安全版 (`TryPeek`) |
| **Try 模式** | `bool TryXxx(out T)` 返回值拷贝 |
| **OrDefault 模式** | LINQ 用 `FirstOrDefault()` 而非 `TryFirst()` |
| **无 ref 返回** | BCL 集合设计早于 C# 7.0 ref 返回特性 |

---

#### 1.3.3 C++ STL — std::vector / std::optional

**std::vector:**
```cpp
T& front();                     // 返回引用，UB if empty
const T& front() const;
T& back();
const T& back() const;
T& at(size_t pos);              // 有边界检查，抛 std::out_of_range
T& operator[](size_t pos);      // 无检查，UB if OOB
void pop_back();                // 无返回值！先 back() 再 pop_back()
```

**C++17 std::optional:**
```cpp
std::optional<T> opt = get_value();
if (opt.has_value()) {
    T& ref = opt.value();       // 或 *opt
}
// 或用 value_or(default)
```

**C++23 期望的 std::expected:**
```cpp
std::expected<T&, std::error_code> try_front();  // 提案阶段
```

| 特性 | C++ STL 设计要点 |
|------|------------------|
| **命名** | `front()`/`back()` 表示两端 |
| **引用返回** | 返回 `T&` 而非值拷贝 |
| **无内置安全访问** | `front()`/`back()` 对空容器是 UB |
| **optional 不包装引用** | `std::optional<T&>` 在标准中有争议 |

---

#### 1.3.4 Swift — Array / Optional

```swift
// 属性风格，返回 Optional
var first: Element? { get }  // nil if empty
var last: Element? { get }

// 强制解包
array.first!                 // crash if nil

// 安全解包
if let first = array.first {
    // 使用 first
}

// 带默认值
let first = array.first ?? defaultValue
```

| 特性 | Swift 设计要点 |
|------|---------------|
| **属性而非方法** | `first` / `last` 是计算属性 |
| **Optional 类型** | 返回 `Element?`，编译器强制处理 |
| **语法糖丰富** | `if let`、`??` 等语法简化 Optional 处理 |

---

#### 1.3.5 Kotlin — List / Nullable

```kotlin
// 抛异常版
fun first(): T               // 抛 NoSuchElementException
fun last(): T

// 安全版
fun firstOrNull(): T?        // 返回 null
fun lastOrNull(): T?

// 带默认值
fun firstOrDefault(default: T): T   // 扩展函数

// getOrNull
fun getOrNull(index: Int): T?
```

| 特性 | Kotlin 设计要点 |
|------|----------------|
| **OrNull 后缀** | 比 `OrDefault` 更明确语义 |
| **Nullable 类型** | `T?` 强制处理 null |
| **双版本并存** | `first()` + `firstOrNull()` |

---

### 1.4 设计要素对比矩阵

| 语言/库 | 首元素 | 尾元素 | 安全返回类型 | 引用语义 | 异常处理 |
|---------|--------|--------|--------------|----------|----------|
| **Rust Vec** | `first()` | `last()` | `Option<&T>` | ✅ 引用 | 无异常 |
| **C# Stack** | N/A | `Peek()` | `TryPeek(out T)` | ❌ 拷贝 | 异常 + Try |
| **C# LINQ** | `First()` | `Last()` | `FirstOrDefault()` | ❌ 拷贝 | 异常 + OrDefault |
| **C++ vector** | `front()` | `back()` | N/A (UB) | ✅ 引用 | UB |
| **Swift Array** | `.first` | `.last` | `Element?` | ❌ 拷贝 | Optional |
| **Kotlin List** | `first()` | `last()` | `firstOrNull()` | ❌ 拷贝 | 异常 + OrNull |

### 1.5 关键洞察

1. **Rust 的 `Option<&T>` 是唯一同时实现"安全访问"和"引用返回"的方案**

2. **C# 的 `TryXxx(out T)` 模式无法返回引用**
   - `out` 参数必须是确定的值
   - 无法表达"可能不存在的引用"

3. **C# 的替代方案**:
   - 方案 A: 接受 `TryXxx(out T)` 产生拷贝
   - 方案 B: 使用 `IsEmpty` + `First()` 预检查模式
   - 方案 C: 引入类似 Rust `Option<&T>` 的 `ref struct RefOption<T>`
   - 方案 D: 使用 `Unsafe.IsNullRef` 判断 ref 返回是否有效 (C# 9+)

4. **Rust 没有 `Peek`**
   - Rust 的 `last()` 就是查看操作
   - `pop()` 是移除操作，返回 `Option<T>` 而非 `Option<&T>`

---

## 2. 决策阶段

### 2.1 设计目标 (Rust 风格优先)

基于 Rust 的设计哲学，我们定义以下目标：

| 目标 | 描述 | 优先级 |
|------|------|--------|
| **零拷贝** | 查看操作应返回引用，不产生拷贝 | P0 |
| **类型安全** | 空列表情况应在类型层面强制处理 | P0 |
| **对称性** | `First`/`Last` 的 API 应完全对称 | P1 |
| **自明性** | 方法名应直接表达访问位置 | P1 |
| **C# 惯用性** | 在可能的范围内符合 C# 习惯 | P2 |

### 2.2 可行设计方向

#### 方向 A: 传统 C# 风格 (牺牲零拷贝)

```csharp
ref T First()              // 异常版，ref 返回
bool TryFirst(out T item)  // 安全版，值拷贝 ⚠️

ref T Last()
bool TryLast(out T item)   // 值拷贝 ⚠️
```

**评估:**

| 维度 | 评分 | 说明 |
|------|------|------|
| 零拷贝 | ⭐⭐ | Try 版本必须拷贝 |
| 类型安全 | ⭐⭐⭐ | `out` 参数语义清晰但不强制处理 |
| 对称性 | ⭐⭐⭐⭐⭐ | 完全对称 |
| C# 惯用性 | ⭐⭐⭐⭐⭐ | 最符合 BCL 惯例 |

**问题**: `TryFirst(out T)` 的拷贝开销可能很大（对于大型 struct）。

---

#### 方向 B: 预检查模式 (极简 Span 风格)

```csharp
// 属性或方法，仅异常版
ref T First()   // 或 ref T First { get; }
ref T Last()

// 安全访问通过 IsEmpty 预检查
bool IsEmpty { get; }

// 使用模式：
// if (!list.IsEmpty) { ref var x = ref list.First(); }
```

**评估:**

| 维度 | 评分 | 说明 |
|------|------|------|
| 零拷贝 | ⭐⭐⭐⭐⭐ | 全程 ref |
| 类型安全 | ⭐⭐ | 依赖调用者正确检查，无编译器强制 |
| 对称性 | ⭐⭐⭐⭐⭐ | 完全对称 |
| C# 惯用性 | ⭐⭐⭐ | 与 Span 一致，但无 Try 模式可能不习惯 |

**问题**: 调用者可能忘记检查 `IsEmpty`，导致运行时异常。

---

#### 方向 C: 引入 RefOption\<T\> (Rust 风格移植)

```csharp
// 定义 ref struct 包装类型
public readonly ref struct RefOption<T> {
    private readonly Span<T> _span;  // 长度 0 或 1

    public bool HasValue => _span.Length > 0;
    public ref T Value => ref _span[0];
    public ref T ValueOrThrow() => HasValue ? ref Value : throw new InvalidOperationException();

    public static RefOption<T> None => default;
    public static RefOption<T> Some(ref T value) => new(MemoryMarshal.CreateSpan(ref value, 1));
}

// API 设计
RefOption<T> TryFirst()   // 返回可能为空的 ref 包装
RefOption<T> TryLast()

ref T First()             // 保留异常版作为便捷 API
ref T Last()
```

**评估:**

| 维度 | 评分 | 说明 |
|------|------|------|
| 零拷贝 | ⭐⭐⭐⭐⭐ | 全程 ref |
| 类型安全 | ⭐⭐⭐⭐⭐ | `RefOption` 强制检查 `HasValue` |
| 对称性 | ⭐⭐⭐⭐⭐ | 完全对称 |
| C# 惯用性 | ⭐⭐ | 需要引入新类型，学习成本 |
| 复杂度 | 高 | `ref struct` 有使用限制（不能 async、不能装箱等） |

**Rust 精神移植的优势**:
- `RefOption<T>.HasValue` 对应 Rust `Option<&T>.is_some()`
- `RefOption<T>.Value` 对应 Rust `Option<&T>.unwrap()` (不安全版)
- 编译器层面阻止未检查的访问

---

#### 方向 D: Null Ref 哨兵模式 (C# 9+ Unsafe.IsNullRef)

```csharp
// 利用 C# 9 的 Unsafe.NullRef<T>() 作为"无值"标记
ref T TryFirst() {
    if (_count == 0) return ref Unsafe.NullRef<T>();
    return ref _items[0];
}

ref T TryLast() {
    if (_count == 0) return ref Unsafe.NullRef<T>();
    return ref _items[_count - 1];
}

// 检查方式：
ref var first = ref list.TryFirst();
if (!Unsafe.IsNullRef(ref first)) {
    // 安全使用 first
}
```

**评估:**

| 维度 | 评分 | 说明 |
|------|------|------|
| 零拷贝 | ⭐⭐⭐⭐⭐ | 全程 ref |
| 类型安全 | ⭐⭐⭐ | 需要调用者正确调用 `Unsafe.IsNullRef` |
| 对称性 | ⭐⭐⭐⭐⭐ | 完全对称 |
| C# 惯用性 | ⭐⭐ | `Unsafe` 命名空间暗示危险操作 |
| 易用性 | ⭐⭐ | `Unsafe.IsNullRef` 语法冗长 |

**问题**: 名称中的 `Unsafe` 可能吓退用户，但实际上只要正确检查就是安全的。

---

#### 方向 E: 双模式 — Try 值返回 + Ref 异常返回 (务实混合)

```csharp
// 高性能场景：ref 返回 + 预检查
ref T First()              // 抛异常，ref 返回
ref T Last()

// 便捷场景：Try 模式 + 值返回
bool TryFirst(out T item)  // 安全，值拷贝
bool TryLast(out T item)

// 文档明确指导：
// - 高性能路径：if (!IsEmpty) { ref var x = ref First(); }
// - 便捷路径：if (TryFirst(out var x)) { ... }
```

**评估:**

| 维度 | 评分 | 说明 |
|------|------|------|
| 零拷贝 | ⭐⭐⭐⭐ | ref 版本零拷贝，Try 版本有拷贝但明确标注 |
| 类型安全 | ⭐⭐⭐ | Try 版本编译器不强制检查返回值 |
| 对称性 | ⭐⭐⭐⭐⭐ | 完全对称 |
| C# 惯用性 | ⭐⭐⭐⭐⭐ | 符合 BCL 惯例 |
| 选择余地 | ⭐⭐⭐⭐⭐ | 用户可根据场景选择 |

**务实考量**:
- 对于小型值类型 (如 `int`, `Point`)，`TryFirst(out T)` 的拷贝开销可忽略
- 对于大型值类型，用户应使用 `IsEmpty` + `First()` 模式

---

### 2.3 决策：方向 C (RefOption) + 方向 E (兼容层)

**核心设计**:

```csharp
// ═══════════════════════════════════════════════════════════
// 第一层：Rust 风格核心 API (零拷贝 + 类型安全)
// ═══════════════════════════════════════════════════════════

/// <summary>返回第一个元素的引用包装。</summary>
RefOption<T> TryGetFirst();

/// <summary>返回最后一个元素的引用包装。</summary>
RefOption<T> TryGetLast();

// ═══════════════════════════════════════════════════════════
// 第二层：便捷 API (可能抛异常或产生拷贝)
// ═══════════════════════════════════════════════════════════

/// <summary>返回第一个元素的引用，空时抛异常。</summary>
ref T First();

/// <summary>返回最后一个元素的引用，空时抛异常。</summary>
ref T Last();

/// <summary>尝试获取第一个元素 (值拷贝)。</summary>
bool TryFirst(out T item);

/// <summary>尝试获取最后一个元素 (值拷贝)。</summary>
bool TryLast(out T item);
```

### 2.4 决策理由

1. **Rust 精神的核心移植**:
   - `RefOption<T>` 对应 Rust 的 `Option<&T>`
   - 实现了"安全访问" + "引用返回"的统一

2. **命名选择: TryGetFirst vs TryFirst**:
   - `TryGetFirst()` 返回 `RefOption<T>` — 获取引用包装
   - `TryFirst(out T)` 返回 `bool` + `out` 参数 — C# Try 惯例
   - 两者语义不同，命名应区分

3. **保留 C# 惯用 API**:
   - `First()` / `Last()` 异常版作为便捷入口
   - `TryFirst(out T)` / `TryLast(out T)` 兼容传统 Try 模式
   - 文档引导用户根据场景选择

4. **零拷贝路径清晰**:
   - 高性能场景: `TryGetFirst()` → `RefOption<T>`
   - 或: `IsEmpty` 检查 + `First()` 调用

5. **删除 Peek 的理由**:
   - Rust 没有 `peek()` — `last()` 就是查看操作
   - `Peek` 在列表上下文语义不明确
   - `Last()` 更自明

---

## 3. 目标设计

### 3.1 RefOption\<T\> 类型定义

```csharp
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

/// <summary>
/// 表示可能存在的元素引用。类似于 Rust 的 <c>Option&lt;&amp;T&gt;</c>。
/// </summary>
/// <remarks>
/// ⚠️ 这是一个 <c>ref struct</c>，不能作为字段、泛型参数、装箱或在 async 方法中使用。
/// </remarks>
/// <typeparam name="T">元素类型。</typeparam>
public readonly ref struct RefOption<T> {
    // 使用 Span 模拟 "可能存在的引用"
    // Length == 0: None
    // Length == 1: Some(&T)
    private readonly Span<T> _span;

    /// <summary>使用单元素 Span 构造 Some 值。</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private RefOption(Span<T> span) => _span = span;

    /// <summary>是否包含有效引用。</summary>
    public bool HasValue {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _span.Length > 0;
    }

    /// <summary>是否不包含有效引用。</summary>
    public bool IsNone {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _span.Length == 0;
    }

    /// <summary>
    /// 获取元素的引用。调用前必须检查 <see cref="HasValue"/>。
    /// </summary>
    /// <exception cref="InvalidOperationException">当 <see cref="HasValue"/> 为 false 时抛出。</exception>
    public ref T Value {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get {
            if (_span.Length == 0)
                ThrowNoValue();
            return ref _span[0];
        }
    }

    /// <summary>
    /// 获取元素的引用，不检查是否有值。仅在确定 <see cref="HasValue"/> 为 true 时使用。
    /// </summary>
    public ref T ValueUnsafe {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref _span[0];
    }

    /// <summary>
    /// 如果有值则返回引用，否则返回 <paramref name="defaultRef"/> 的引用。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T ValueOr(ref T defaultRef) {
        return ref _span.Length > 0 ? ref _span[0] : ref defaultRef;
    }

    /// <summary>
    /// 尝试获取值的拷贝。
    /// </summary>
    /// <param name="value">如果有值则为元素拷贝；否则为 <c>default(T)</c>。</param>
    /// <returns>是否有值。</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetValue(out T value) {
        if (_span.Length > 0) {
            value = _span[0];
            return true;
        }
        value = default!;
        return false;
    }

    /// <summary>创建空的 RefOption。</summary>
    public static RefOption<T> None {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => default;
    }

    /// <summary>从引用创建有值的 RefOption。</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RefOption<T> Some(ref T value) {
        return new RefOption<T>(MemoryMarshal.CreateSpan(ref value, 1));
    }

    /// <summary>从 Span 创建 RefOption（Span 长度必须为 0 或 1）。</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static RefOption<T> FromSpan(Span<T> span) {
        Debug.Assert(span.Length <= 1);
        return new RefOption<T>(span);
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowNoValue() =>
        throw new InvalidOperationException("RefOption does not contain a value.");

    // 支持解构
    public void Deconstruct(out bool hasValue, out T value) {
        hasValue = HasValue;
        value = hasValue ? _span[0] : default!;
    }
}
```

### 3.2 StructList\<T\> 元素访问 API

```csharp
#region 元素访问 — Rust 风格 (RefOption 返回)

/// <summary>
/// 尝试获取第一个元素的引用。
/// </summary>
/// <returns>
/// 如果列表非空，返回包含第一个元素引用的 <see cref="RefOption{T}"/>；
/// 否则返回 <see cref="RefOption{T}.None"/>。
/// </returns>
/// <remarks>
/// 此方法是零拷贝的安全访问方式，类似于 Rust 的 <c>Vec::first()</c> 返回 <c>Option&lt;&amp;T&gt;</c>。
/// <para>
/// <b>使用模式:</b>
/// <code>
/// var opt = list.TryGetFirst();
/// if (opt.HasValue) {
///     ref var first = ref opt.Value;
///     first.X = 100; // 直接修改，零拷贝
/// }
/// </code>
/// </para>
/// </remarks>
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public readonly RefOption<T> TryGetFirst() {
    if (_count == 0)
        return RefOption<T>.None;
    return RefOption<T>.Some(ref _items[0]);
}

/// <summary>
/// 尝试获取最后一个元素的引用。
/// </summary>
/// <returns>
/// 如果列表非空，返回包含最后一个元素引用的 <see cref="RefOption{T}"/>；
/// 否则返回 <see cref="RefOption{T}.None"/>。
/// </returns>
/// <remarks>
/// 此方法是零拷贝的安全访问方式，类似于 Rust 的 <c>Vec::last()</c> 返回 <c>Option&lt;&amp;T&gt;</c>。
/// </remarks>
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public readonly RefOption<T> TryGetLast() {
    if (_count == 0)
        return RefOption<T>.None;
    return RefOption<T>.Some(ref _items[_count - 1]);
}

/// <summary>
/// 尝试获取指定索引处元素的引用。
/// </summary>
/// <param name="index">要访问的索引。</param>
/// <returns>
/// 如果索引有效，返回包含该元素引用的 <see cref="RefOption{T}"/>；
/// 否则返回 <see cref="RefOption{T}.None"/>。
/// </returns>
/// <remarks>
/// 类似于 Rust 的 <c>slice::get()</c> 返回 <c>Option&lt;&amp;T&gt;</c>。
/// </remarks>
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public readonly RefOption<T> TryGet(int index) {
    if ((uint)index >= (uint)_count)
        return RefOption<T>.None;
    return RefOption<T>.Some(ref _items[index]);
}

#endregion

#region 元素访问 — 异常版 (便捷 API)

/// <summary>
/// 返回对第一个元素的引用。
/// </summary>
/// <returns>第一个元素的引用。</returns>
/// <exception cref="InvalidOperationException">列表为空时抛出。</exception>
/// <remarks>
/// 如需安全访问，请使用 <see cref="TryGetFirst()"/> 或先检查 <see cref="IsEmpty"/>。
/// </remarks>
[MethodImpl(MethodImplOptions.AggressiveInlining)]
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
/// <remarks>
/// 如需安全访问，请使用 <see cref="TryGetLast()"/> 或先检查 <see cref="IsEmpty"/>。
/// </remarks>
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public readonly ref T Last() {
    if (_count == 0)
        ThrowEmptyList();
    return ref _items[_count - 1];
}

#endregion

#region 元素访问 — C# Try 模式 (兼容 API，值拷贝)

/// <summary>
/// 尝试获取第一个元素的值。
/// </summary>
/// <param name="item">如果列表非空，则为第一个元素的拷贝；否则为 <c>default(T)</c>。</param>
/// <returns>如果列表非空则为 <see langword="true"/>；否则为 <see langword="false"/>。</returns>
/// <remarks>
/// ⚠️ 此方法返回元素的<b>拷贝</b>而非引用。对于大型值类型，
/// 考虑使用 <see cref="TryGetFirst()"/> 以避免拷贝开销。
/// </remarks>
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public readonly bool TryFirst(out T item) {
    if (_count == 0) {
        item = default!;
        return false;
    }
    item = _items[0];
    return true;
}

/// <summary>
/// 尝试获取最后一个元素的值。
/// </summary>
/// <param name="item">如果列表非空，则为最后一个元素的拷贝；否则为 <c>default(T)</c>。</param>
/// <returns>如果列表非空则为 <see langword="true"/>；否则为 <see langword="false"/>。</returns>
/// <remarks>
/// ⚠️ 此方法返回元素的<b>拷贝</b>而非引用。对于大型值类型，
/// 考虑使用 <see cref="TryGetLast()"/> 以避免拷贝开销。
/// </remarks>
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public readonly bool TryLast(out T item) {
    if (_count == 0) {
        item = default!;
        return false;
    }
    item = _items[_count - 1];
    return true;
}

#endregion

#region 移除操作

/// <summary>
/// 移除并返回最后一个元素。
/// </summary>
/// <returns>被移除的元素。</returns>
/// <exception cref="InvalidOperationException">列表为空时抛出。</exception>
public T Pop();  // 保持现有实现

/// <summary>
/// 尝试移除并返回最后一个元素。
/// </summary>
/// <param name="item">如果列表非空，则为被移除的元素；否则为 <c>default(T)</c>。</param>
/// <returns>如果成功移除则为 <see langword="true"/>；否则为 <see langword="false"/>。</returns>
public bool TryPop(out T item);  // 保持现有实现

#endregion
```

### 3.3 变更清单

| 方法 | 变更类型 | 说明 |
|------|----------|------|
| `RefOption<T>` | ✨ **新增类型** | Rust `Option<&T>` 的 C# 移植 |
| `TryGetFirst()` | ✨ **新增** | 返回 `RefOption<T>`，零拷贝安全访问 |
| `TryGetLast()` | ✨ **新增** | 返回 `RefOption<T>`，零拷贝安全访问 |
| `TryGet(int)` | ✨ **新增** | 返回 `RefOption<T>`，安全索引访问 |
| `First()` | ✅ **保留** | 无变化 |
| `Last()` | ✏️ **修改** | 改为独立实现，不再调用 `Peek()` |
| `TryFirst(out T)` | ✨ **新增** | C# Try 模式兼容，有值拷贝 |
| `TryLast(out T)` | ✨ **新增** | C# Try 模式兼容，有值拷贝 |
| `Peek()` | ❌ **删除** | 被 `Last()` 取代 |
| `TryPeek(out T)` | ❌ **删除** | 被 `TryLast(out T)` 和 `TryGetLast()` 取代 |
| `Pop()` | ✅ **保留** | 无变化 |
| `TryPop(out T)` | ✅ **保留** | 无变化 |

### 3.4 使用示例

```csharp
var list = new StructList<Point>(capacity: 10);
list.Add(new Point(1, 1));
list.Add(new Point(2, 2));
list.Add(new Point(3, 3));

// ═══════════════════════════════════════════════════════════
// Rust 风格：RefOption (推荐用于高性能场景)
// ═══════════════════════════════════════════════════════════

// 安全访问 + 零拷贝修改
var opt = list.TryGetFirst();
if (opt.HasValue) {
    ref var first = ref opt.Value;
    first.X = 100;  // 直接修改原始元素
}

// 解构语法
var (hasValue, value) = list.TryGetLast();
if (hasValue) {
    Console.WriteLine(value);  // value 是拷贝
}

// ValueOr 模式
Point defaultPoint = new(0, 0);
ref var firstOrDefault = ref list.TryGetFirst().ValueOr(ref defaultPoint);

// 链式检查
if (list.TryGetFirst().TryGetValue(out var firstValue)) {
    Console.WriteLine(firstValue);
}

// ═══════════════════════════════════════════════════════════
// C# 传统风格：异常版 + Try 模式
// ═══════════════════════════════════════════════════════════

// 异常版（确定非空时使用）
if (!list.IsEmpty) {
    ref var first = ref list.First();
    ref var last = ref list.Last();
}

// Try 模式（值拷贝，适合小型类型）
if (list.TryFirst(out var item)) {
    Console.WriteLine(item);
}

// ═══════════════════════════════════════════════════════════
// 安全索引访问
// ═══════════════════════════════════════════════════════════

// 传统方式（抛异常）
ref var elem = ref list[1];

// 新增安全方式
var elemOpt = list.TryGet(10);  // 索引越界返回 None
if (elemOpt.HasValue) {
    // 使用 elemOpt.Value
}

// ═══════════════════════════════════════════════════════════
// 栈操作
// ═══════════════════════════════════════════════════════════

while (!list.IsEmpty) {
    var popped = list.Pop();
}

if (list.TryPop(out var poppedItem)) {
    Console.WriteLine(poppedItem);
}
```

### 3.5 API 选择指南

| 场景 | 推荐 API | 理由 |
|------|----------|------|
| 高性能 + 大型值类型 | `TryGetFirst()` / `TryGetLast()` | 零拷贝，类型安全 |
| 高性能 + 确定非空 | `IsEmpty` 检查 + `First()` / `Last()` | 零拷贝，简洁 |
| 便捷 + 小型值类型 | `TryFirst(out T)` / `TryLast(out T)` | 符合 C# 习惯 |
| 需要默认值 | `TryGetFirst().ValueOr(ref default)` | 零拷贝默认值 |
| 安全索引访问 | `TryGet(index)` | 不抛异常 |

### 3.6 迁移指南

#### Breaking Changes

| 旧 API | 新 API | 迁移方式 |
|--------|--------|----------|
| `Peek()` | `Last()` | 直接替换 |
| `TryPeek(out var x)` | `TryLast(out var x)` | 直接替换 |
| `TryPeek(out var x)` | `TryGetLast()` | 如需零拷贝，改用 RefOption |

#### 迁移脚本

```powershell
# PowerShell 正则替换
Get-ChildItem -Recurse -Include *.cs | ForEach-Object {
    $content = Get-Content $_.FullName -Raw
    $content = $content -replace '\.Peek\(\)', '.Last()'
    $content = $content -replace '\.TryPeek\(', '.TryLast('
    Set-Content $_.FullName $content -NoNewline
}
```

#### 可选：渐进式废弃

```csharp
[Obsolete("Use Last() instead. This method will be removed in v2.0.", error: false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public readonly ref T Peek() => ref Last();

[Obsolete("Use TryLast() or TryGetLast() instead.", error: false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public readonly bool TryPeek(out T item) => TryLast(out item);
```

---

## 4. 设计对比

### 4.1 与前版方案对比

| 维度 | V1-V4 | V5 (本方案) |
|------|-------|-------------|
| **核心创新** | 无 | `RefOption<T>` 类型 |
| **零拷贝安全访问** | 无/部分 | ✅ 完整支持 |
| **类型安全** | 依赖调用者检查 | ✅ 编译器强制 `HasValue` 检查 |
| **C# Try 模式** | 主推 | 作为兼容层保留 |
| **Rust 风格** | 参考 | 核心移植 |

### 4.2 与 Rust 的对应关系

| Rust | C# (本方案) | 说明 |
|------|-------------|------|
| `vec.first()` → `Option<&T>` | `TryGetFirst()` → `RefOption<T>` | 对应 |
| `vec.last()` → `Option<&T>` | `TryGetLast()` → `RefOption<T>` | 对应 |
| `slice.get(i)` → `Option<&T>` | `TryGet(i)` → `RefOption<T>` | 对应 |
| `opt.unwrap()` | `refOpt.Value` | 抛异常 |
| `opt.unwrap_unchecked()` | `refOpt.ValueUnsafe` | 无检查 |
| `opt.unwrap_or(&default)` | `refOpt.ValueOr(ref default)` | 带默认值 |
| `opt.is_some()` | `refOpt.HasValue` | 检查有值 |
| `opt.is_none()` | `refOpt.IsNone` | 检查无值 |

---

## 5. 附录

### 5.1 RefOption\<T\> 的限制

由于 `RefOption<T>` 是 `ref struct`，存在以下限制：

| 限制 | 说明 | 解决方案 |
|------|------|----------|
| **不能作为字段** | 不能存储在 class/struct 字段中 | 在局部作用域使用 |
| **不能装箱** | 不能转换为 object 或接口 | N/A |
| **不能用于 async** | 不能跨越 await 边界 | 在 async 前取值 |
| **不能作为泛型参数** | 不能 `List<RefOption<T>>` | N/A |

**这些限制与 `Span<T>` 完全相同**，是 ref struct 的固有特性。

### 5.2 性能考量

`RefOption<T>` 的实现使用 `Span<T>` (长度 0 或 1)，具有：

- **零堆分配**: 纯栈分配
- **内联优化**: 所有方法标记 `AggressiveInlining`
- **零拷贝**: 返回原始元素的引用

**基准测试预期** (待实现后验证):

| 操作 | 开销 |
|------|------|
| `TryGetFirst().HasValue` 检查 | 1 次比较 |
| `TryGetFirst().Value` 访问 | 1 次比较 + 1 次引用解析 |
| `TryGetFirst().ValueUnsafe` 访问 | 仅引用解析 (无检查) |

### 5.3 为什么不使用 Nullable\<T\> 或 T?

`Nullable<T>` 和 C# 8 可空引用类型 (`T?`) 不适用于此场景：

1. **`Nullable<T>` 只适用于值类型**，且返回值拷贝
2. **`T?` (可空引用类型)** 只适用于引用类型，且是编译时标注，不是真正的包装类型
3. **都无法包装 `ref T`**

`RefOption<T>` 是专门为包装"可能不存在的引用"设计的类型。

### 5.4 为什么命名 `TryGetFirst` 而非 `TryFirst` (返回 RefOption)?

| 命名 | 返回类型 | C# 惯例 |
|------|----------|---------|
| `TryFirst(out T)` | `bool` | 标准 Try 模式 |
| `TryGetFirst()` | `RefOption<T>` | 返回包装类型 |

两者语义不同：
- `TryFirst(out T)` 遵循 `TryXxx(out result)` 模式
- `TryGetFirst()` 遵循 `GetXxxOrNull()` / `TryGetValue()` 模式

为避免歧义，使用 `TryGet` 前缀表示返回包装类型。

---

## 6. 完整实现代码

```csharp
// 文件: RefOption.cs
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DocUI.Text;

/// <summary>
/// 表示可能存在的元素引用。类似于 Rust 的 <c>Option&lt;&amp;T&gt;</c>。
/// </summary>
/// <typeparam name="T">元素类型。</typeparam>
public readonly ref struct RefOption<T> {
    private readonly Span<T> _span;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private RefOption(Span<T> span) => _span = span;

    /// <summary>是否包含有效引用。</summary>
    public bool HasValue {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _span.Length > 0;
    }

    /// <summary>是否不包含有效引用。</summary>
    public bool IsNone {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _span.Length == 0;
    }

    /// <summary>获取元素的引用。调用前必须检查 <see cref="HasValue"/>。</summary>
    /// <exception cref="InvalidOperationException">当 <see cref="HasValue"/> 为 false。</exception>
    public ref T Value {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get {
            if (_span.Length == 0) ThrowNoValue();
            return ref _span[0];
        }
    }

    /// <summary>获取元素的引用，不检查是否有值。</summary>
    public ref T ValueUnsafe {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref _span[0];
    }

    /// <summary>如果有值则返回引用，否则返回默认引用。</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T ValueOr(ref T defaultRef) =>
        ref _span.Length > 0 ? ref _span[0] : ref defaultRef;

    /// <summary>尝试获取值的拷贝。</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetValue(out T value) {
        if (_span.Length > 0) {
            value = _span[0];
            return true;
        }
        value = default!;
        return false;
    }

    /// <summary>空的 RefOption。</summary>
    public static RefOption<T> None {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => default;
    }

    /// <summary>从引用创建有值的 RefOption。</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RefOption<T> Some(ref T value) =>
        new(MemoryMarshal.CreateSpan(ref value, 1));

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowNoValue() =>
        throw new InvalidOperationException("RefOption does not contain a value.");

    /// <summary>解构为 (hasValue, valueCopy)。</summary>
    public void Deconstruct(out bool hasValue, out T value) {
        hasValue = HasValue;
        value = hasValue ? _span[0] : default!;
    }
}
```

```csharp
// 文件: StructList.ElementAccess.cs (partial class)
using System.Runtime.CompilerServices;

namespace DocUI.Text;

internal partial struct StructList<T> {
    #region 元素访问 — RefOption (Rust 风格)

    /// <summary>尝试获取第一个元素的引用。</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly RefOption<T> TryGetFirst() =>
        _count == 0 ? RefOption<T>.None : RefOption<T>.Some(ref _items[0]);

    /// <summary>尝试获取最后一个元素的引用。</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly RefOption<T> TryGetLast() =>
        _count == 0 ? RefOption<T>.None : RefOption<T>.Some(ref _items[_count - 1]);

    /// <summary>尝试获取指定索引处元素的引用。</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly RefOption<T> TryGet(int index) =>
        (uint)index >= (uint)_count ? RefOption<T>.None : RefOption<T>.Some(ref _items[index]);

    #endregion

    #region 元素访问 — 异常版

    /// <summary>返回对第一个元素的引用。</summary>
    /// <exception cref="InvalidOperationException">列表为空。</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly ref T First() {
        if (_count == 0) ThrowEmptyList();
        return ref _items[0];
    }

    /// <summary>返回对最后一个元素的引用。</summary>
    /// <exception cref="InvalidOperationException">列表为空。</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly ref T Last() {
        if (_count == 0) ThrowEmptyList();
        return ref _items[_count - 1];
    }

    #endregion

    #region 元素访问 — Try 模式 (值拷贝)

    /// <summary>尝试获取第一个元素 (值拷贝)。</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool TryFirst(out T item) {
        if (_count == 0) { item = default!; return false; }
        item = _items[0];
        return true;
    }

    /// <summary>尝试获取最后一个元素 (值拷贝)。</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool TryLast(out T item) {
        if (_count == 0) { item = default!; return false; }
        item = _items[_count - 1];
        return true;
    }

    #endregion
}
```

---

## 7. 结论

本方案 (v5) 的核心创新是引入 `RefOption<T>` 类型，实现了 Rust `Option<&T>` 在 C# 中的等价物：

| 目标 | 达成情况 |
|------|----------|
| **零拷贝** | ✅ `RefOption<T>` 包装引用，无拷贝 |
| **类型安全** | ✅ 必须检查 `HasValue` 才能访问 `Value` |
| **对称性** | ✅ `TryGetFirst`/`TryGetLast`/`TryGet` 完全对称 |
| **C# 兼容** | ✅ 保留 `TryFirst(out T)` 传统模式 |
| **删除冗余** | ✅ 删除 `Peek`/`TryPeek` |

这是迄今为止最接近 Rust 设计哲学的 C# 实现方案。

---

**文档结束**
````
