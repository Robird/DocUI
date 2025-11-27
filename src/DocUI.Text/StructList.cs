using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DocUI.Text;

/// <summary>
/// 值类型列表，通过 <c>ref</c> 返回避免元素复制。
/// 内部数组可外部注入以配合 <see cref="System.Buffers.ArrayPool{T}"/> 池化。
/// </summary>
/// <remarks>
/// ⚠️ 非线程安全：该类型不支持并发读写，必须由调用方在单一所有者或外部同步下使用。
/// ⚠️ 不可复制/克隆：此 <c>struct</c> 绝不应被按值复制或克隆（赋值会浅拷贝内部数组引用）；应作为其它类型的内嵌字段使用以替代堆分配的 <c>List&lt;T&gt;</c>。
/// 用途：局部替代 <c>List&lt;T&gt;</c> 以减少堆分配，并通过 ref 返回避免元素复制。
/// </remarks>
internal struct StructList<T> {
    private T[] _items;
    private int _count;

    private const int DefaultCapacity = 4;

    #region 构造与初始化

    /// <summary>使用指定初始容量创建。</summary>
    /// <remarks>推荐的标准构造方式。若配合 ArrayPool 使用，请改用 <see cref="StructList(T[])"/>。</remarks>
    public StructList(int capacity) {
        if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        _items = capacity == 0 ? Array.Empty<T>() : new T[capacity];
        _count = 0;
    }

    /// <summary>使用外部提供的数组创建（用于池化场景）。</summary>
    /// <remarks>
    /// ⚠️ 调用方负责数组的生命周期管理（如归还到 ArrayPool）。
    /// 传入 null 会被静默替换为 <see cref="Array.Empty{T}"/>，但这通常表示调用方 bug。
    /// </remarks>
    /// <param name="backingArray">后备数组，不应为 null。</param>
    public StructList(T[] backingArray) {
        Debug.Assert(backingArray is not null, "backingArray should not be null; use StructList(int) for empty initialization.");
        _items = backingArray ?? Array.Empty<T>();
        _count = 0;
    }

    /// <summary>使用外部数组并设置初始有效元素数（用于包装已有数据）。</summary>
    /// <remarks>
    /// 适用场景：将已填充数据的租用数组包装为 StructList 进行后续操作。
    /// ⚠️ <paramref name="initialCount"/> 之前的元素被视为有效数据，不会被清零。
    /// </remarks>
    /// <param name="backingArray">后备数组，不应为 null。</param>
    /// <param name="initialCount">初始有效元素数，必须 ≤ 数组长度。</param>
    public StructList(T[] backingArray, int initialCount) {
        Debug.Assert(backingArray is not null, "backingArray should not be null.");
        if (initialCount < 0) throw new ArgumentOutOfRangeException(nameof(initialCount));
        _items = backingArray ?? Array.Empty<T>();
        if (initialCount > _items.Length) throw new ArgumentOutOfRangeException(nameof(initialCount));
        _count = initialCount;
    }

    #endregion

    #region 属性

    /// <summary>有效元素个数。</summary>
    public readonly int Count => _count;

    /// <summary>当前容量。</summary>
    public readonly int Capacity => _items?.Length ?? 0;

    /// <summary>是否为空。</summary>
    public readonly bool IsEmpty => _count == 0;

    /// <summary>内部数组（用于归还到池）。</summary>
    public readonly T[]? BackingArray => _items;

    #endregion

    #region 索引访问

    /// <summary>通过 ref 返回元素，避免复制。</summary>
    public readonly ref T this[int index] {
        get {
            if ((uint)index >= (uint)_count)
                ThrowIndexOutOfRange(nameof(index));
            return ref _items[index];
        }
    }

    /// <summary>直接写入指定索引的元素。</summary>
    public void Set(int index, in T item) {
        if ((uint)index >= (uint)_count) ThrowIndexOutOfRange(nameof(index));
        _items[index] = item;
    }

    /// <summary>获取有效区域的 Span 视图。</summary>
    public readonly Span<T> AsSpan() =>
        _items is null ? Span<T>.Empty : _items.AsSpan(0, _count);

    /// <summary>获取有效区域的 ReadOnlySpan 视图。</summary>
    public readonly ReadOnlySpan<T> AsReadOnlySpan() =>
        _items is null ? ReadOnlySpan<T>.Empty : _items.AsSpan(0, _count);

    #endregion

    #region 添加与插入

    /// <summary>在末尾添加元素。</summary>
    public void Add(in T item) {
        if (_count >= Capacity)
            Grow(_count + 1);
        _items[_count++] = item;
    }

    /// <summary>在末尾添加多个元素。</summary>
    public void AddRange(ReadOnlySpan<T> items) {
        if (items.IsEmpty) return;
        EnsureCapacity(_count + items.Length);
        items.CopyTo(_items.AsSpan(_count));
        _count += items.Length;
    }

    /// <summary>在指定位置插入元素。</summary>
    public void Insert(int index, in T item) {
        if ((uint)index > (uint)_count)
            ThrowIndexOutOfRange(nameof(index));

        if (_count >= Capacity)
            Grow(_count + 1);

        // 后移元素
        if (index < _count) {
            Array.Copy(_items, index, _items, index + 1, _count - index);
        }
        _items[index] = item;
        _count++;
    }

    /// <summary>在指定位置插入多个元素。</summary>
    public void InsertRange(int index, ReadOnlySpan<T> items) {
        if ((uint)index > (uint)_count)
            ThrowIndexOutOfRange(nameof(index));
        if (items.IsEmpty) return;

        EnsureCapacity(_count + items.Length);

        // 后移元素
        if (index < _count) {
            Array.Copy(_items, index, _items, index + items.Length, _count - index);
        }
        items.CopyTo(_items.AsSpan(index));
        _count += items.Length;
    }

    #endregion

    #region 删除

    /// <summary>删除指定位置的元素。</summary>
    public void RemoveAt(int index) {
        if ((uint)index >= (uint)_count)
            ThrowIndexOutOfRange(nameof(index));

        _count--;
        if (index < _count) {
            Array.Copy(_items, index + 1, _items, index, _count - index);
        }
        // 清理引用防止泄漏
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>()) {
            _items[_count] = default!;
        }
    }

    /// <summary>删除指定范围的元素。</summary>
    public void RemoveRange(int index, int count) {
        if (index < 0 || count < 0 || index + count > _count)
            ThrowArgumentOutOfRange();

        if (count == 0) return;

        _count -= count;
        if (index < _count) {
            Array.Copy(_items, index + count, _items, index, _count - index);
        }
        // 清理引用防止泄漏
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>()) {
            Array.Clear(_items, _count, count);
        }
    }

    /// <summary>清空所有元素，保留容量。</summary>
    public void Clear() {
        if (_count == 0) {
            return;
        }

        if (_items is not null && RuntimeHelpers.IsReferenceOrContainsReferences<T>()) {
            Array.Clear(_items, 0, _count);
        }
        _count = 0;
    }

    /// <summary>移除并返回最后一个元素（Python 风格）。</summary>
    /// <exception cref="InvalidOperationException">列表为空时抛出。</exception>
    public T Pop() {
        if (_count == 0)
            ThrowEmptyList();

        _count--;
        var item = _items[_count];
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>()) {
            _items[_count] = default!;
        }
        return item;
    }

    /// <summary>尝试移除并返回最后一个元素。</summary>
    public bool TryPop(out T item) {
        if (_count == 0) {
            item = default!;
            return false;
        }

        _count--;
        item = _items[_count];
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>()) {
            _items[_count] = default!;
        }
        return true;
    }

    /// <summary>
    /// 获取第一个元素的引用。
    /// </summary>
    /// <remarks>
    /// 设计决策：不提供 TryFirst，调用方应使用 <see cref="IsEmpty"/> 预检查。
    /// 理由：Try 模式的 out 参数会产生值拷贝，违背 StructList 的零拷贝设计目标。
    /// </remarks>
    /// <exception cref="InvalidOperationException">列表为空。</exception>
    public readonly ref T First() {
        if (_count == 0)
            ThrowEmptyList();
        return ref _items[0];
    }

    /// <summary>
    /// 获取最后一个元素的引用。
    /// </summary>
    /// <remarks>
    /// 设计决策：不提供 TryLast，调用方应使用 <see cref="IsEmpty"/> 预检查。
    /// 理由：Try 模式的 out 参数会产生值拷贝，违背 StructList 的零拷贝设计目标。
    /// 另注：不提供 Peek() 别名，避免混入栈/队列语义；StructList 锚定 List 实现。
    /// </remarks>
    /// <exception cref="InvalidOperationException">列表为空。</exception>
    public readonly ref T Last() {
        if (_count == 0)
            ThrowEmptyList();
        return ref _items[_count - 1];
    }

    #endregion

    #region 二分查找

    /// <summary>
    /// 二分查找，返回匹配元素的索引。
    /// 若未找到，返回负数，其按位取反（~result）为应插入位置。
    /// </summary>
    public readonly int BinarySearch(in T item) {
        return BinarySearch(0, _count, in item, Comparer<T>.Default);
    }

    /// <summary>使用自定义比较器进行二分查找。</summary>
    public readonly int BinarySearch(in T item, IComparer<T> comparer) {
        return BinarySearch(0, _count, in item, comparer);
    }

    /// <summary>在指定范围内二分查找。</summary>
    public readonly int BinarySearch(int index, int count, in T item, IComparer<T>? comparer) {
        if (index < 0 || count < 0 || index + count > _count)
            ThrowArgumentOutOfRange();

        return Array.BinarySearch(_items, index, count, item, comparer);
    }

    /// <summary>
    /// 使用选择器进行二分查找（适用于按某字段查找）。
    /// </summary>
    /// <typeparam name="TKey">用于比较的键类型。</typeparam>
    /// <param name="key">要查找的键值。</param>
    /// <param name="keySelector">从元素提取键的函数。</param>
    public readonly int BinarySearchBy<TKey>(TKey key, Func<T, TKey> keySelector)
        where TKey : IComparable<TKey> {
        return BinarySearchBy(key, keySelector, Comparer<TKey>.Default);
    }

    /// <summary>使用选择器和自定义比较器进行二分查找。</summary>
    public readonly int BinarySearchBy<TKey>(TKey key, Func<T, TKey> keySelector, IComparer<TKey> comparer) {
        int lo = 0;
        int hi = _count - 1;

        while (lo <= hi) {
            int mid = lo + ((hi - lo) >> 1);
            int cmp = comparer.Compare(keySelector(_items[mid]), key);

            if (cmp == 0)
                return mid;
            if (cmp < 0)
                lo = mid + 1;
            else
                hi = mid - 1;
        }

        return ~lo;
    }

    #endregion

    #region 容量管理

    /// <summary>确保至少有指定容量。</summary>
    public void EnsureCapacity(int capacity) {
        if (capacity > Capacity) {
            Grow(capacity);
        }
    }

    /// <summary>释放多余容量。</summary>
    public void TrimExcess() {
        if (_count < Capacity * 0.9) {
            SetCapacity(_count);
        }
    }

    /// <summary>
    /// 重置并注入新的后备数组（用于池化复用）。
    /// </summary>
    /// <param name="newBackingArray">新的后备数组，可为 null。</param>
    /// <returns>原后备数组，调用方负责归还到池。</returns>
    public T[]? Reset(T[]? newBackingArray) {
        var old = _items;
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>() && old is not null) {
            Array.Clear(old!, 0, _count);
        }
        _items = newBackingArray ?? Array.Empty<T>();
        _count = 0;
        return old;
    }

    /// <summary>
    /// 分离当前数组（调用方获得所有权）。
    /// </summary>
    public T[]? Detach(out int count) {
        var arr = _items;
        count = _count;
        _items = Array.Empty<T>();
        _count = 0;
        return arr;
    }

    #endregion

    #region 私有方法

    private void Grow(int minCapacity) {
        int newCapacity = Capacity == 0 ? DefaultCapacity : Capacity * 2;
        if (newCapacity < minCapacity)
            newCapacity = minCapacity;

        SetCapacity(newCapacity);
    }

    private void SetCapacity(int capacity) {
        if (capacity == Capacity) return;

        if (capacity <= 0) {
            _items = Array.Empty<T>();
            return;
        }

        var newItems = new T[capacity];
        if (_count > 0 && _items is not null) {
            Array.Copy(_items, newItems, Math.Min(_count, capacity));
        }
        _items = newItems;
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowIndexOutOfRange(string paramName) =>
        throw new ArgumentOutOfRangeException(paramName);

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowArgumentOutOfRange(string? paramName = null) =>
        throw new ArgumentOutOfRangeException(paramName);

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowEmptyList() =>
        throw new InvalidOperationException("List is empty.");

    #endregion

    #region 枚举

    /// <summary>获取枚举器（用于 foreach，零分配）。</summary>
    public readonly Enumerator GetEnumerator() => new(this);

    /// <summary>零分配枚举器。</summary>
    public ref struct Enumerator {
        private readonly T[] _items;
        private readonly int _count;
        private int _index;

        internal Enumerator(StructList<T> list) {
            _items = list._items ?? Array.Empty<T>();
            _count = list._count;
            _index = -1;
        }

        public bool MoveNext() => ++_index < _count;

        public readonly ref T Current => ref _items[_index];
    }

    #endregion
}
