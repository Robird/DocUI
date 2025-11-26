using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DocUI.Text;

public class OverlayBuilder {
    private struct LineMut {
        /// <summary>一行内的连续多段字符，不含换行符。</summary>
        public StructList<ReadOnlyMemory<char>> Segments;
        /// <summary>以 char 为单位的本行总长度（不含换行符）。</summary>
        public int Length;
        /// <summary>以 char 为单位的行起始偏移（Lazy 更新，-1 表示脏）。</summary>
        public int Offset;

        /// <summary>标记 Offset 需要重算。</summary>
        public void InvalidateOffset() => Offset = -1;
    }

    private StructList<LineMut> _lines;
    private bool _offsetsDirty;

    /// <summary>行数。</summary>
    public int LineCount => _lines.Count;

    /// <summary>总字符数（不含换行符）。</summary>
    public int Length {
        get {
            if (_lines.IsEmpty) return 0;
            EnsureOffsets();
            ref var last = ref _lines.Last();
            return last.Offset + last.Length;
        }
    }

    /// <summary>
    /// 在指定行的指定字符偏移处插入一段不含换行符的文本。
    /// </summary>
    /// <param name="lineIndex">目标行索引。</param>
    /// <param name="offset">行内字符偏移（0 = 行首）。</param>
    /// <param name="segment">要插入的文本段（不能含换行符）。</param>
    private void InsertSegmentCore(int lineIndex, int offset, ReadOnlyMemory<char> segment) {
        if ((uint)lineIndex >= (uint)_lines.Count)
            throw new ArgumentOutOfRangeException(nameof(lineIndex));
        if (segment.IsEmpty)
            return;

        ref var line = ref _lines[lineIndex];

        if (offset < 0 || offset > line.Length)
            throw new ArgumentOutOfRangeException(nameof(offset));

        // 快速路径：行首插入
        if (offset == 0) {
            line.Segments.Insert(0, segment);
            line.Length += segment.Length;
            InvalidateOffsetsFrom(lineIndex + 1);
            return;
        }

        // 快速路径：行尾追加
        if (offset == line.Length) {
            line.Segments.Append(segment);
            line.Length += segment.Length;
            InvalidateOffsetsFrom(lineIndex + 1);
            return;
        }

        // 一般情况：在行中间插入，需要定位并可能分割现有段
        var (segIndex, segOffset) = FindSegmentPosition(ref line, offset);

        if (segOffset == 0) {
            // 刚好在某段的起始位置，直接插入
            line.Segments.Insert(segIndex, segment);
        } else {
            // 需要分割当前段
            var currentSeg = line.Segments[segIndex];
            var leftPart = currentSeg.Slice(0, segOffset);
            var rightPart = currentSeg.Slice(segOffset);

            // 替换为：left + new + right
            line.Segments[segIndex] = leftPart;
            line.Segments.Insert(segIndex + 1, segment);
            if (!rightPart.IsEmpty) {
                line.Segments.Insert(segIndex + 2, rightPart);
            }
        }

        line.Length += segment.Length;
        InvalidateOffsetsFrom(lineIndex + 1);
    }

    /// <summary>
    /// 在行内查找给定字符偏移所在的段及段内偏移。
    /// </summary>
    /// <returns>(段索引, 段内偏移)</returns>
    private static (int SegmentIndex, int OffsetInSegment) FindSegmentPosition(ref LineMut line, int charOffset) {
        int accumulated = 0;
        for (int i = 0; i < line.Segments.Count; i++) {
            int segLen = line.Segments[i].Length;
            if (charOffset < accumulated + segLen) {
                return (i, charOffset - accumulated);
            }
            accumulated += segLen;
        }
        // 理论上不应到达这里（offset 已验证在范围内）
        return (line.Segments.Count, 0);
    }

    /// <summary>
    /// 标记从指定行开始的所有行 Offset 需要重算。
    /// </summary>
    private void InvalidateOffsetsFrom(int startLine) {
        // 简单策略：标记全局脏，下次访问时重算
        // 更精细的策略可以只标记 [startLine, end)，但对于突发编辑场景，
        // 通常会在一次渲染结束前多次编辑，最后统一重算更高效。
        _offsetsDirty = true;
    }

    /// <summary>
    /// 确保所有行的 Offset 是最新的。
    /// </summary>
    private void EnsureOffsets() {
        if (!_offsetsDirty) return;

        int offset = 0;
        for (int i = 0; i < _lines.Count; i++) {
            ref var line = ref _lines[i];
            line.Offset = offset;
            offset += line.Length;
        }
        _offsetsDirty = false;
    }

    #region 位置查询

    /// <summary>
    /// 从全局字符偏移获取 (行索引, 列偏移)。
    /// </summary>
    /// <param name="offset">全局字符偏移（不含换行符计数）。</param>
    /// <returns>(行索引, 行内列偏移)</returns>
    /// <exception cref="ArgumentOutOfRangeException">偏移超出范围时抛出。</exception>
    public (int Line, int Column) GetPosition(int offset) {
        if (_lines.IsEmpty) {
            if (offset == 0) return (0, 0);
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        EnsureOffsets();

        if (offset < 0 || offset > Length)
            throw new ArgumentOutOfRangeException(nameof(offset));

        // 二分查找：找到 Offset <= offset 的最后一行
        int lineIndex = _lines.BinarySearchBy(offset, static line => line.Offset);

        if (lineIndex < 0) {
            // 未精确匹配，~lineIndex 是"应插入位置"，减 1 得到包含该 offset 的行
            lineIndex = ~lineIndex - 1;
        }

        // 边界保护
        lineIndex = Math.Clamp(lineIndex, 0, _lines.Count - 1);

        ref var line = ref _lines[lineIndex];
        int column = offset - line.Offset;

        // 处理 offset 正好在行尾的情况（可能属于下一行的起始）
        if (column > line.Length && lineIndex + 1 < _lines.Count) {
            lineIndex++;
            column = 0;
        }

        return (lineIndex, Math.Min(column, line.Length));
    }

    /// <summary>
    /// 从 (行索引, 列偏移) 计算全局字符偏移。
    /// </summary>
    public int GetOffset(int line, int column) {
        if ((uint)line >= (uint)_lines.Count)
            throw new ArgumentOutOfRangeException(nameof(line));

        EnsureOffsets();
        ref var lineData = ref _lines[line];

        if (column < 0 || column > lineData.Length)
            throw new ArgumentOutOfRangeException(nameof(column));

        return lineData.Offset + column;
    }

    #endregion

    #region 行级操作

    /// <summary>
    /// 在末尾追加一行（单段）。
    /// </summary>
    public void AppendLine(ReadOnlyMemory<char> content) {
        // 利用 MemoryMarshal 创建单元素 Span，避免数组分配
        InsertLineCoreSpan(_lines.Count, MemoryMarshal.CreateReadOnlySpan(ref content, 1));
    }

    /// <summary>
    /// 在末尾追加一行（多段，零分配）。
    /// </summary>
    public void AppendLine(ReadOnlySpan<ReadOnlyMemory<char>> segments) {
        InsertLineCoreSpan(_lines.Count, segments);
    }

    /// <summary>
    /// 在末尾追加一行（从快照行复制）。
    /// </summary>
    public void AppendLine(OverlayLineImmutable sourceLine) {
        if (sourceLine is null)
            throw new ArgumentNullException(nameof(sourceLine));
        InsertLineFromImmutable(_lines.Count, sourceLine);
    }

    /// <summary>
    /// 在末尾追加一行（流式输入，用于 PieceTree/Rope）。
    /// </summary>
    public void AppendLineFromChunks(IEnumerable<ReadOnlyMemory<char>> chunks) {
        InsertLineFromEnumerable(_lines.Count, chunks);
    }

    /// <summary>
    /// 在指定位置插入一行（单段）。
    /// </summary>
    public void InsertLine(int lineIndex, ReadOnlyMemory<char> content) {
        if ((uint)lineIndex > (uint)_lines.Count)
            throw new ArgumentOutOfRangeException(nameof(lineIndex));
        InsertLineCoreSpan(lineIndex, MemoryMarshal.CreateReadOnlySpan(ref content, 1));
    }

    /// <summary>
    /// 在指定位置插入一行（多段，零分配）。
    /// </summary>
    public void InsertLine(int lineIndex, ReadOnlySpan<ReadOnlyMemory<char>> segments) {
        if ((uint)lineIndex > (uint)_lines.Count)
            throw new ArgumentOutOfRangeException(nameof(lineIndex));
        InsertLineCoreSpan(lineIndex, segments);
    }

    /// <summary>
    /// 插入一行的核心实现（Span 版本，零分配）。
    /// </summary>
    private void InsertLineCoreSpan(int lineIndex, ReadOnlySpan<ReadOnlyMemory<char>> segments) {
        var newLine = new LineMut {
            Segments = new StructList<ReadOnlyMemory<char>>(Math.Max(4, segments.Length)),
            Offset = -1
        };

        int totalLength = 0;
        foreach (var seg in segments) {
            if (!seg.IsEmpty) {
                newLine.Segments.Append(seg);
                totalLength += seg.Length;
            }
        }
        newLine.Length = totalLength;

        _lines.Insert(lineIndex, newLine);
        InvalidateOffsetsFrom(lineIndex);
    }

    /// <summary>
    /// 从 OverlayLineImmutable 复制。
    /// </summary>
    private void InsertLineFromImmutable(int lineIndex, OverlayLineImmutable source) {
        var exported = source.Segments;
        var newLine = new LineMut {
            Segments = new StructList<ReadOnlyMemory<char>>(Math.Max(4, exported.Count)),
            Length = source.Length,
            Offset = -1
        };

        for (int i = 0; i < exported.Count; i++) {
            var seg = exported[i];
            if (!seg.Content.IsEmpty) {
                newLine.Segments.Append(seg.Content);
            }
        }

        _lines.Insert(lineIndex, newLine);
        InvalidateOffsetsFrom(lineIndex);
    }

    /// <summary>
    /// 从 IEnumerable 流式构建（允许分配）。
    /// </summary>
    private void InsertLineFromEnumerable(int lineIndex, IEnumerable<ReadOnlyMemory<char>> chunks) {
        if (chunks is null)
            throw new ArgumentNullException(nameof(chunks));

        var newLine = new LineMut {
            Segments = new StructList<ReadOnlyMemory<char>>(8),
            Offset = -1
        };

        int totalLength = 0;
        foreach (var chunk in chunks) {
            if (!chunk.IsEmpty) {
                newLine.Segments.Append(chunk);
                totalLength += chunk.Length;
            }
        }
        newLine.Length = totalLength;

        _lines.Insert(lineIndex, newLine);
        InvalidateOffsetsFrom(lineIndex);
    }

    /// <summary>
    /// 插入一行（旧版单段实现，保留向后兼容）。
    /// </summary>
    [Obsolete("Use InsertLineCoreSpan instead")]
    private void InsertLineCore(int lineIndex, ReadOnlyMemory<char> content) {
        InsertLineCoreSpan(lineIndex, MemoryMarshal.CreateReadOnlySpan(ref content, 1));
    }

    /// <summary>
    /// 追加一个空行。
    /// </summary>
    public void AppendEmptyLine() => InsertLineCoreSpan(_lines.Count, ReadOnlySpan<ReadOnlyMemory<char>>.Empty);

    /// <summary>
    /// 删除指定行。
    /// </summary>
    public void RemoveLine(int lineIndex) {
        if ((uint)lineIndex >= (uint)_lines.Count)
            throw new ArgumentOutOfRangeException(nameof(lineIndex));

        _lines.RemoveAt(lineIndex);
        InvalidateOffsetsFrom(lineIndex);
    }

    #endregion

    #region 段级操作（公开 API）

    /// <summary>
    /// 在全局字符偏移处插入一段不含换行符的文本。
    /// </summary>
    /// <param name="offset">全局字符偏移。</param>
    /// <param name="segment">要插入的文本段。</param>
    public void InsertAt(int offset, ReadOnlyMemory<char> segment) {
        if (segment.IsEmpty) return;

        // 处理空文档情况
        if (_lines.IsEmpty) {
            if (offset != 0)
                throw new ArgumentOutOfRangeException(nameof(offset));
            AppendLine(segment);
            return;
        }

        var (line, column) = GetPosition(offset);
        InsertSegmentCore(line, column, segment);
    }

    /// <summary>
    /// 在指定行的行首插入文本。
    /// </summary>
    public void PrependToLine(int lineIndex, ReadOnlyMemory<char> segment) {
        InsertSegmentCore(lineIndex, 0, segment);
    }

    /// <summary>
    /// 在指定行的行尾追加文本。
    /// </summary>
    public void AppendToLine(int lineIndex, ReadOnlyMemory<char> segment) {
        if ((uint)lineIndex >= (uint)_lines.Count)
            throw new ArgumentOutOfRangeException(nameof(lineIndex));

        ref var line = ref _lines[lineIndex];
        InsertSegmentCore(lineIndex, line.Length, segment);
    }

    #endregion
}

/// <summary>
/// 值类型列表，通过 <c>ref</c> 返回避免元素复制。
/// 内部数组可外部注入以配合 <see cref="System.Buffers.ArrayPool{T}"/> 池化。
/// </summary>
/// <remarks>
/// ⚠️ 这是 struct，赋值会浅拷贝内部数组引用。请通过 <c>ref</c> 传递或仅在单一所有者场景使用。
/// </remarks>
internal struct StructList<T> {
    private T[] _items;
    private int _count;

    private const int DefaultCapacity = 4;

    #region 构造与初始化

    /// <summary>使用指定初始容量创建。</summary>
    public StructList(int capacity) {
        _items = capacity > 0 ? new T[capacity] : Array.Empty<T>();
        _count = 0;
    }

    /// <summary>使用外部提供的数组创建（用于池化场景）。</summary>
    public StructList(T[] backingArray) {
        _items = backingArray ?? Array.Empty<T>();
        _count = 0;
    }

    /// <summary>使用外部数组并设置初始有效元素数。</summary>
    public StructList(T[] backingArray, int initialCount) {
        _items = backingArray ?? Array.Empty<T>();
        _count = Math.Min(initialCount, _items.Length);
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
                ThrowIndexOutOfRange();
            return ref _items[index];
        }
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(T item) {
        if (_count >= Capacity)
            Grow(_count + 1);
        _items[_count++] = item;
    }

    /// <summary>在末尾添加元素（Add 的别名，Python 风格）。</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(T item) => Add(item);

    /// <summary>在末尾添加多个元素。</summary>
    public void AddRange(ReadOnlySpan<T> items) {
        if (items.IsEmpty) return;
        EnsureCapacity(_count + items.Length);
        items.CopyTo(_items.AsSpan(_count));
        _count += items.Length;
    }

    /// <summary>在指定位置插入元素。</summary>
    public void Insert(int index, T item) {
        if ((uint)index > (uint)_count)
            ThrowIndexOutOfRange();

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
            ThrowIndexOutOfRange();
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
            ThrowIndexOutOfRange();

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

    /// <summary>查看最后一个元素但不移除。</summary>
    public readonly ref T Peek() {
        if (_count == 0)
            ThrowEmptyList();
        return ref _items[_count - 1];
    }

    /// <summary>查看第一个元素。</summary>
    public readonly ref T First() {
        if (_count == 0)
            ThrowEmptyList();
        return ref _items[0];
    }

    /// <summary>查看最后一个元素。</summary>
    public readonly ref T Last() => ref Peek();

    #endregion

    #region 二分查找

    /// <summary>
    /// 二分查找，返回匹配元素的索引。
    /// 若未找到，返回负数，其按位取反（~result）为应插入位置。
    /// </summary>
    public readonly int BinarySearch(T item) {
        return BinarySearch(0, _count, item, Comparer<T>.Default);
    }

    /// <summary>使用自定义比较器进行二分查找。</summary>
    public readonly int BinarySearch(T item, IComparer<T> comparer) {
        return BinarySearch(0, _count, item, comparer);
    }

    /// <summary>在指定范围内二分查找。</summary>
    public readonly int BinarySearch(int index, int count, T item, IComparer<T>? comparer) {
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
        if (_count > 0) {
            Array.Copy(_items, newItems, Math.Min(_count, capacity));
        }
        _items = newItems;
    }

    private static void ThrowIndexOutOfRange() =>
        throw new ArgumentOutOfRangeException("index");

    private static void ThrowArgumentOutOfRange() =>
        throw new ArgumentOutOfRangeException();

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
