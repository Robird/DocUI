# StructList\<T\> 元素访问 API 设计方案 (V2)

> **文档状态**: 草案
> **创建日期**: 2025-11-27
> **作者**: API 设计评审
> **差异化视角**: 本文档从"角色分离"与"零拷贝优先"角度重新审视设计

---

## 1. 分析阶段

### 1.1 现有问题再审视

| 问题 | 现有方案的处理 | 本文档的重新思考 |
|------|----------------|------------------|
| `Last()` 是 `Peek()` 的别名 | 删除 `Peek`，统一用 `Last` | 两者是否真的等价？语义上有细微差别 |
| 缺少 `TryFirst` | 补充 `TryFirst(out T)` | `out T` 会产生值拷贝，是否违背 `StructList` 的设计初衷？ |
| `Peek` 命名不清晰 | 改用 `First`/`Last` | 是否可以保留 `Peek` 但明确其语义？ |
| 命名不一致 | 统一为序列语义 | 是否应该承认"混合角色"的合理性？ |

**核心洞察**: `StructList<T>` 的设计初衷是**零拷贝高性能**，但 `TryXxx(out T)` 模式天然需要值拷贝。这是一个根本性的张力。

### 1.2 主流语言/库的类似设计

#### 1.2.1 C# Span\<T\> 与 Index/Range

```csharp
// Span 不提供 First()/Last() 方法，而是依赖索引
span[0]           // 第一个
span[^1]          // 最后一个 (Index 语法)

// 获取引用
ref T first = ref span[0];
ref T last = ref span[^1];

// 安全边界检查通过 Length
if (span.Length > 0) { /* 安全访问 */ }
```

| 特性 | 评价 |
|------|------|
| 命名惯例 | 无专门方法，完全依赖索引器 + `^` 语法 |
| 返回类型 | ref 返回 |
| 安全检查 | 通过 `Length` 属性预检查 |
| 优点 | **零方法膨胀**，与 C# 8+ Index 语法完美融合 |
| 缺点 | 需要用户自行检查边界，无 Try 模式 |

---

#### 1.2.2 Rust Option\<&T\> 与 unsafe get_unchecked

```rust
// 安全版本 - 返回 Option<&T>
vec.first()       // Option<&T>
vec.last()        // Option<&T>

// 不安全版本 - 直接返回引用，调用者负责边界检查
unsafe { vec.get_unchecked(0) }     // &T
unsafe { vec.get_unchecked(len-1) } // &T

// 模式匹配处理 Option
if let Some(first) = vec.first() {
    // 使用 first
}
```

| 特性 | 评价 |
|------|------|
| 命名惯例 | 安全版与 unsafe 版分离 |
| 返回类型 | `Option<&T>` 包装引用 |
| 安全检查 | 类型系统强制处理 None |
| 优点 | **引用语义与安全性兼得**，无拷贝 |
| 缺点 | 需要解包 Option，C# 无直接对应 |

---

#### 1.2.3 C# 新趋势: Unsafe 类与 MemoryMarshal

```csharp
// .NET 核心库中的模式
public static class Unsafe {
    public static ref T Add<T>(ref T source, int offset);
    public static bool IsNullRef<T>(ref T source);
}

public static class MemoryMarshal {
    public static ref T GetReference<T>(Span<T> span);
    public static ref T GetArrayDataReference<T>(T[] array);
}
```

**洞察**: .NET 高性能代码正在向"ref 返回 + 调用者负责检查"的模式演进，而非依赖 Try 模式。

---

#### 1.2.4 Go Slice

```go
slice := []int{1, 2, 3}
first := slice[0]       // 值拷贝，panic if empty
last := slice[len(slice)-1]

// 安全访问需要显式检查
if len(slice) > 0 {
    first := slice[0]
}
```

| 特性 | 评价 |
|------|------|
| 命名惯例 | 无专门函数，依赖索引 |
| 返回类型 | 值拷贝 |
| 安全检查 | 显式 len 检查 |
| 优点 | 极简 |
| 缺点 | panic 恢复成本高，无 ref 语义 |

---

#### 1.2.5 Swift Array

```swift
array.first      // Optional<Element> - 属性，不是方法
array.last       // Optional<Element> - 属性，不是方法

// 强制解包（不安全）
array.first!     // Element，crash if nil

// 安全解包
if let first = array.first {
    // 使用 first
}
```

| 特性 | 评价 |
|------|------|
| 命名惯例 | `first`/`last` 是**属性**，不是方法 |
| 返回类型 | `Optional<Element>`（值语义） |
| 安全检查 | Optional 强制处理 |
| 优点 | 属性语法更简洁 |
| 缺点 | 无引用语义，每次访问产生拷贝 |

---

### 1.3 关键发现总结

| 维度 | 传统 Try 模式 | Ref 返回 + 预检查模式 | Option/Nullable 模式 |
|------|---------------|----------------------|---------------------|
| 代表 | C# Dictionary.TryGetValue | Span 索引器 / Rust unsafe | Rust Option / Swift Optional |
| 拷贝 | **必须拷贝** | **零拷贝** | 可包装引用 |
| 类型安全 | 编译期无保障 | 调用者责任 | 类型系统强制 |
| C# 惯用度 | 非常惯用 | 高性能场景惯用 | 需要自定义类型 |

**核心矛盾**: C# 的 `TryXxx(out T)` 模式与 `ref` 返回不兼容。`out` 参数必须是确定值，无法返回"可能不存在的引用"。

---

## 2. 决策阶段

### 2.1 可行设计方向

#### 方向 A: 传统 First/Last + Try 模式 (值拷贝)

```csharp
ref T First()           // 异常版，ref 返回
bool TryFirst(out T item)  // 安全版，值拷贝

ref T Last()            // 异常版，ref 返回
bool TryLast(out T item)   // 安全版，值拷贝
```

**评估:**

| 维度 | 评分 | 说明 |
|------|------|------|
| 一致性 | ⭐⭐⭐⭐⭐ | 完全对称 |
| 零拷贝 | ⭐⭐ | Try 版本必须拷贝 |
| C# 契合度 | ⭐⭐⭐⭐⭐ | 最符合 C# 惯例 |
| 创新性 | ⭐ | 无新意 |

---

#### 方向 B: 索引器优先 + 辅助属性

```csharp
// 主要访问方式：索引器（已有）
ref T this[int index]   // 支持 list[0], list[^1]
ref T this[Index index] // C# 8+ Index 语法

// 便捷属性（只读，抛异常）
ref T First => ref this[0];      // 属性，非方法
ref T Last => ref this[^1];      // 属性，非方法

// 安全检查通过 IsEmpty
bool IsEmpty { get; }   // 已有

// 无 Try 方法！调用者应：
// if (!list.IsEmpty) { ref var x = ref list.First; }
```

**评估:**

| 维度 | 评分 | 说明 |
|------|------|------|
| 一致性 | ⭐⭐⭐⭐ | 统一依赖索引器 |
| 零拷贝 | ⭐⭐⭐⭐⭐ | 全程 ref，无拷贝 |
| C# 契合度 | ⭐⭐⭐ | 需要用户改变习惯 |
| 创新性 | ⭐⭐⭐⭐ | 与 Span 风格一致 |

---

#### 方向 C: 引入 Ref<T>? 包装类型

```csharp
// 自定义 ref 包装
public readonly ref struct RefOption<T> {
    private readonly ref T _ref;
    private readonly bool _hasValue;

    public bool HasValue => _hasValue;
    public ref T Value => ref _ref;  // 调用前需检查 HasValue

    public static RefOption<T> None => default;
}

// API 设计
RefOption<T> TryFirst()   // 返回可能为空的 ref
RefOption<T> TryLast()    // 返回可能为空的 ref

ref T First()             // 保留异常版
ref T Last()              // 保留异常版
```

**评估:**

| 维度 | 评分 | 说明 |
|------|------|------|
| 一致性 | ⭐⭐⭐⭐ | Try 版本也返回 ref 语义 |
| 零拷贝 | ⭐⭐⭐⭐⭐ | 全程 ref，无拷贝 |
| C# 契合度 | ⭐⭐ | 需要引入新类型 |
| 创新性 | ⭐⭐⭐⭐⭐ | 解决根本矛盾 |
| 复杂度 | 高 | 需要维护额外类型 |

---

#### 方向 D: 角色分离 - 查看 vs 栈操作

```csharp
// === 序列查看角色 (只读，ref 返回) ===
ref T First => ref this[0];       // 属性
ref T Last => ref this[^1];       // 属性

// === 栈操作角色 (修改，值返回) ===
T Pop()                           // 移除并返回最后一个
bool TryPop(out T item)           // 安全版

// === 废弃 ===
// Peek() - 被 Last 属性取代
// TryPeek() - 无直接替代，使用 IsEmpty + Last

// === 不提供 ===
// TryFirst / TryLast - 使用 IsEmpty 检查 + 属性访问
```

**设计哲学**:
- **查看**是无副作用的纯操作，应返回 ref，调用者通过 `IsEmpty` 预检查
- **移除**有副作用，返回值拷贝是合理的（因为元素已不在列表中）

**评估:**

| 维度 | 评分 | 说明 |
|------|------|------|
| 一致性 | ⭐⭐⭐⭐ | 按角色分离，各自一致 |
| 零拷贝 | ⭐⭐⭐⭐ | 查看操作全程 ref |
| C# 契合度 | ⭐⭐⭐ | 属性访问较新颖 |
| 极简性 | ⭐⭐⭐⭐⭐ | 最小 API 表面积 |

---

### 2.2 决策: 选择方向 D (角色分离) + 可选 Index 支持

**核心理由:**

1. **零拷贝是 `StructList<T>` 的灵魂**
   - 类型注释明确写道："通过 `ref` 返回避免元素复制"
   - 引入 `TryFirst(out T)` 会产生拷贝，**违背设计初衷**

2. **Try 模式的局限性**
   - C# 的 `out` 参数无法返回"可能不存在的引用"
   - 这是语言层面的限制，不应通过增加拷贝来妥协

3. **IsEmpty 检查足够简单**
   ```csharp
   // 推荐模式
   if (!list.IsEmpty) {
       ref var first = ref list.First;
       // 使用 first
   }
   ```
   这与 `Span<T>` 的使用模式一致，C# 高性能开发者已熟悉。

4. **属性 vs 方法**
   - `First` 和 `Last` 是访问器，不是动作，属性语义更准确
   - Swift、Kotlin 都使用属性风格

5. **角色分离降低认知负担**
   - 查看操作: `First`, `Last` (属性，ref 返回，可能抛异常)
   - 移除操作: `Pop`, `TryPop` (方法，值返回)
   - 两类操作的返回语义不同，不应强行对称

**为何不选其他方向:**

- **方向 A**: 引入 `TryFirst`/`TryLast` 产生拷贝，与 `StructList` 的高性能定位冲突
- **方向 B**: 过度依赖索引器，`First`/`Last` 作为方法更常见
- **方向 C**: 引入 `RefOption<T>` 增加复杂度，且 `ref struct` 有使用限制

---

## 3. 目标设计

### 3.1 完整 API 签名

```csharp
#region 元素访问 (查看 - 属性，ref 返回)

/// <summary>
/// 获取对第一个元素的引用。
/// </summary>
/// <value>第一个元素的引用。</value>
/// <exception cref="InvalidOperationException">列表为空时抛出。</exception>
/// <remarks>
/// 调用前请检查 <see cref="IsEmpty"/> 以避免异常。
/// <code>
/// if (!list.IsEmpty) {
///     ref var first = ref list.First;
/// }
/// </code>
/// </remarks>
public readonly ref T First {
    get {
        if (_count == 0) ThrowEmptyList();
        return ref _items[0];
    }
}

/// <summary>
/// 获取对最后一个元素的引用。
/// </summary>
/// <value>最后一个元素的引用。</value>
/// <exception cref="InvalidOperationException">列表为空时抛出。</exception>
/// <remarks>
/// 调用前请检查 <see cref="IsEmpty"/> 以避免异常。
/// <code>
/// if (!list.IsEmpty) {
///     ref var last = ref list.Last;
/// }
/// </code>
/// </remarks>
public readonly ref T Last {
    get {
        if (_count == 0) ThrowEmptyList();
        return ref _items[_count - 1];
    }
}

#endregion

#region 元素访问 (移除 - 方法，值返回)

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

#region 索引访问 (已有，增强)

/// <summary>
/// 通过 ref 返回元素，避免复制。支持 <c>^</c> 索引语法。
/// </summary>
/// <example>
/// <code>
/// ref var first = ref list[0];
/// ref var last = ref list[^1];  // C# 8+ Index 语法
/// </code>
/// </example>
public readonly ref T this[int index] { get; }

/// <summary>
/// 支持 C# 8+ Index 类型（如 <c>^1</c>）。
/// </summary>
public readonly ref T this[Index index] {
    get {
        int actualIndex = index.GetOffset(_count);
        if ((uint)actualIndex >= (uint)_count)
            ThrowIndexOutOfRange(nameof(index));
        return ref _items[actualIndex];
    }
}

#endregion
```

### 3.2 变更清单

| 方法/属性 | 变更类型 | 说明 |
|-----------|----------|------|
| `First` | **修改** | 从方法改为属性 |
| `Last` | **修改** | 从方法改为属性，移除对 `Peek()` 的调用 |
| `this[Index]` | **新增** | 支持 `^1` 语法 |
| `Peek()` | **删除** | 被 `Last` 属性取代 |
| `TryPeek(out T)` | **删除** | 使用 `IsEmpty` + `Last` 替代 |
| `Pop()` | **保留** | 无变化 |
| `TryPop(out T)` | **保留** | 无变化 |

**注意**: 本方案**不提供** `TryFirst`/`TryLast` 方法，这是刻意的设计决策。

### 3.3 实现代码

```csharp
#region 元素访问 (查看)

/// <summary>
/// 获取对第一个元素的引用。
/// </summary>
public readonly ref T First {
    get {
        if (_count == 0)
            ThrowEmptyList();
        return ref _items[0];
    }
}

/// <summary>
/// 获取对最后一个元素的引用。
/// </summary>
public readonly ref T Last {
    get {
        if (_count == 0)
            ThrowEmptyList();
        return ref _items[_count - 1];
    }
}

#endregion

#region 索引访问

/// <summary>
/// 支持 C# 8+ Index 类型。
/// </summary>
public readonly ref T this[Index index] {
    get {
        int actualIndex = index.GetOffset(_count);
        if ((uint)actualIndex >= (uint)_count)
            ThrowIndexOutOfRange(nameof(index));
        return ref _items[actualIndex];
    }
}

#endregion
```

### 3.4 使用示例

```csharp
var list = new StructList<Point>(capacity: 10);
list.Add(new Point(1, 1));
list.Add(new Point(2, 2));
list.Add(new Point(3, 3));

// ✅ 推荐：属性访问
ref var first = ref list.First;
ref var last = ref list.Last;

// ✅ 推荐：Index 语法
ref var alsoLast = ref list[^1];
ref var second = ref list[^2];

// ✅ 安全访问模式
if (!list.IsEmpty) {
    ref var safeFirst = ref list.First;
    safeFirst.X = 100;  // 直接修改，零拷贝
}

// ✅ 栈操作
while (!list.IsEmpty) {
    var item = list.Pop();  // 值拷贝是合理的，元素已移除
}

// ✅ 安全栈操作
if (list.TryPop(out var popped)) {
    Console.WriteLine(popped);
}

// ❌ 不再支持 (编译错误)
// list.Peek()     // 使用 list.Last
// list.TryPeek()  // 使用 if (!list.IsEmpty) list.Last
```

### 3.5 迁移指南

#### Breaking Changes

| 旧 API | 新 API | 迁移方式 |
|--------|--------|----------|
| `First()` | `First` | 移除括号，从方法变属性 |
| `Last()` | `Last` | 移除括号，从方法变属性 |
| `Peek()` | `Last` | 直接替换 |
| `TryPeek(out T item)` | `if (!list.IsEmpty) list.Last` | 重构为显式检查 |

#### 自动化迁移脚本 (正则表达式)

```
# First() → First
\.First\(\)  →  .First

# Last() → Last
\.Last\(\)   →  .Last

# Peek() → Last
\.Peek\(\)   →  .Last

# TryPeek 需要手动重构
# 搜索: \.TryPeek\(
# 改为: if (!xxx.IsEmpty) { ... = xxx.Last; }
```

#### TryPeek 迁移示例

**旧代码:**
```csharp
if (list.TryPeek(out var item)) {
    DoSomething(item);
}
```

**新代码 (方式 1 - 值拷贝):**
```csharp
if (!list.IsEmpty) {
    var item = list.Last;  // 值拷贝
    DoSomething(item);
}
```

**新代码 (方式 2 - 零拷贝):**
```csharp
if (!list.IsEmpty) {
    DoSomething(in list.Last);  // 传递 ref，零拷贝
}
```

---

## 4. 设计对比

### 4.1 与 V1 方案的差异

| 维度 | V1 方案 | V2 方案 (本文档) |
|------|---------|------------------|
| `First`/`Last` | 方法 | **属性** |
| `TryFirst`/`TryLast` | 提供 | **不提供** |
| 安全访问 | Try 模式 | **IsEmpty 预检查** |
| Index 支持 | 无 | **新增 `this[Index]`** |
| 零拷贝 | 部分 (Try 版有拷贝) | **全程** (查看操作) |
| API 表面积 | 6 个方法 | **2 属性 + 1 索引器** |

### 4.2 设计哲学对比

**V1 思路**: 模仿 LINQ，提供全面的 Try 模式，优先兼容性。

**V2 思路**: 坚守零拷贝，承认 Try 模式的语言限制，选择极简。

---

## 5. FAQ

### Q1: 没有 TryFirst/TryLast 会不会不方便？

**A**: 使用 `IsEmpty` 检查只需一行：
```csharp
if (!list.IsEmpty) { /* 使用 list.First */ }
```
这与 `Span<T>` 的使用模式一致，高性能 C# 开发者已熟悉此模式。

### Q2: 为什么 First/Last 是属性而不是方法？

**A**:
1. 属性表示"访问状态"，方法表示"执行动作"
2. `First`/`Last` 是状态查询，不是动作
3. Swift、Kotlin 都采用属性风格
4. 调用语法更简洁：`list.First` vs `list.First()`

### Q3: 如何处理可能为空的情况并需要默认值？

**A**: 使用条件表达式：
```csharp
var value = list.IsEmpty ? defaultValue : list.First;
```

### Q4: Pop 为什么返回值拷贝而不是 ref？

**A**: `Pop` 会移除元素，移除后该内存位置不再属于列表。返回 ref 到已移除的位置是危险的。值拷贝在此场景是正确且必要的。

### Q5: 这个设计会不会太"反 C# 惯例"？

**A**: 本设计与 `Span<T>` 的使用模式一致，而 `Span<T>` 正是 .NET 高性能编程的核心类型。对于 `StructList<T>` 这种明确定位为"高性能替代 List<T>"的类型，采用相同的设计哲学是合理的。

---

## 6. 附录: 为什么不使用 ref struct RefOption\<T\>

理论上可以定义：

```csharp
public readonly ref struct RefOption<T> {
    private readonly bool _hasValue;
    private readonly ref T _value;

    public bool HasValue => _hasValue;
    public ref T Value => ref _value;
}
```

**实际问题**:

1. **C# 不支持 `ref` 字段** (直到 C# 11)，需要使用 `Span<T>` 模拟：
   ```csharp
   public readonly ref struct RefOption<T> {
       private readonly Span<T> _span;  // Length 0 或 1
       public bool HasValue => _span.Length > 0;
       public ref T Value => ref _span[0];
   }
   ```

2. **ref struct 的使用限制**: 不能作为泛型参数、不能装箱、不能用于 async 方法。

3. **增加 API 复杂度**: 需要用户理解一个新类型。

4. **边际收益小**: `IsEmpty` + 属性访问已经足够简洁。

因此，本方案选择不引入额外类型。

---

**文档结束**
