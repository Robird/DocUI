using System.Diagnostics;

namespace DocUI.Text;

/// <summary>
/// 底层段列表操作器，支持在任意位置插入文本段。
/// 每次插入都会立即生效并改变后续的偏移量。
/// </summary>
/// <remarks>
/// 这是一个可变的、即时生效的文本构建器。
/// 如果需要基于原始文本坐标的声明式叠加操作，请使用 <see cref="OverlayBuilder"/>。
/// </remarks>
public class SegmentListBuilder {
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

    /// <summary>
    /// 用于按 Offset 字段二分查找 LineMut 的键选择器。
    /// </summary>
    private readonly struct LineOffsetSelector : IKeySelector<LineMut, int> {
        public static int GetKey(in LineMut item) => item.Offset;
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

        Debug.Assert(!ContainsLineEnding(segment.Span), "Segment passed to InsertSegmentCore must not contain line endings.");
        // 快速路径：行首插入
        if (offset == 0) {
            line.Segments.Insert(0, segment);
            line.Length += segment.Length;
            InvalidateOffsetsFrom(lineIndex + 1);
            return;
        }

        // 快速路径：行尾追加
        if (offset == line.Length) {
            line.Segments.Add(segment);
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

    private static bool ContainsLineEnding(ReadOnlySpan<char> span) {
        for (int i = 0; i < span.Length; i++) {
            var ch = span[i];
            if (ch == '\r' || ch == '\n') {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 确保文档已包含至少一行，便于统一的 Insert 流程。
    /// </summary>
    private void EnsureDocumentInitialized() {
        if (!_lines.IsEmpty)
            return;

        _lines.Add(CreateEmptyLine());
        _offsetsDirty = true;
    }

    private void InsertNormalizedSegments(int lineIndex, int column, ReadOnlySpan<ReadOnlyMemory<char>> segments) {
        if (segments.IsEmpty)
            return;
        if ((uint)lineIndex >= (uint)_lines.Count)
            throw new ArgumentOutOfRangeException(nameof(lineIndex));

        var currentLine = lineIndex;
        var currentColumn = column;

        for (int i = 0; i < segments.Length; i++) {
            var segment = segments[i];
            if (segment.IsEmpty) {
                continue;
            }

            InsertNormalizedSegment(ref currentLine, ref currentColumn, segment);
        }
    }

    private void InsertNormalizedSegment(ref int lineIndex, ref int column, ReadOnlyMemory<char> segment) {
        var span = segment.Span;
        int cursor = 0;

        while (cursor < span.Length) {
            int breakLength;
            int breakIndex = FindNextLineBreak(span, cursor, out breakLength);
            int chunkEnd = breakIndex >= 0 ? breakIndex : span.Length;

            if (chunkEnd > cursor) {
                var chunk = segment.Slice(cursor, chunkEnd - cursor);
                InsertSegmentCore(lineIndex, column, chunk);
                column += chunk.Length;
            }

            if (breakIndex < 0) {
                break;
            }

            SplitLineAt(lineIndex, column);
            lineIndex++;
            column = 0;
            cursor = breakIndex + breakLength;
        }
    }

    private static int FindNextLineBreak(ReadOnlySpan<char> span, int start, out int breakLength) {
        for (int i = start; i < span.Length; i++) {
            var current = span[i];
            if (current == '\r') {
                breakLength = i + 1 < span.Length && span[i + 1] == '\n' ? 2 : 1;
                return i;
            }

            if (current == '\n') {
                breakLength = 1;
                return i;
            }
        }

        breakLength = 0;
        return -1;
    }

    /// <summary>
    /// 在指定列将行拆分成左右两部分，右半部分作为新行插入。
    /// </summary>
    private void SplitLineAt(int lineIndex, int column) {
        if ((uint)lineIndex >= (uint)_lines.Count)
            throw new ArgumentOutOfRangeException(nameof(lineIndex));

        ref var line = ref _lines[lineIndex];
        if (column < 0 || column > line.Length)
            throw new ArgumentOutOfRangeException(nameof(column));

        if (column == line.Length) {
            _lines.Insert(lineIndex + 1, CreateEmptyLine());
            InvalidateOffsetsFrom(lineIndex + 1);
            return;
        }

        var sourceSegments = line.Segments;
        int capacity = Math.Max(4, sourceSegments.Count);
        var leftSegments = new StructList<ReadOnlyMemory<char>>(capacity);
        var rightSegments = new StructList<ReadOnlyMemory<char>>(capacity);
        int leftLength = 0;
        int rightLength = 0;
        int remaining = column;

        for (int i = 0; i < sourceSegments.Count; i++) {
            var current = sourceSegments[i];
            if (current.IsEmpty) {
                continue;
            }

            if (remaining > 0) {
                if (remaining >= current.Length) {
                    leftSegments.Add(current);
                    leftLength += current.Length;
                    remaining -= current.Length;
                    continue;
                }

                var leftSlice = current.Slice(0, remaining);
                var rightSlice = current.Slice(remaining);

                if (!leftSlice.IsEmpty) {
                    leftSegments.Add(leftSlice);
                    leftLength += leftSlice.Length;
                }

                if (!rightSlice.IsEmpty) {
                    rightSegments.Add(rightSlice);
                    rightLength += rightSlice.Length;
                }

                remaining = 0;
                continue;
            }

            rightSegments.Add(current);
            rightLength += current.Length;
        }

        Debug.Assert(remaining == 0, "SplitLineAt should consume the requested column.");

        line.Segments = leftSegments;
        line.Length = leftLength;

        var newLine = new LineMut {
            Segments = rightSegments,
            Length = rightLength,
            Offset = -1
        };

        _lines.Insert(lineIndex + 1, newLine);
        InvalidateOffsetsFrom(lineIndex);
    }

    private static LineMut CreateEmptyLine() => new() {
        Segments = new StructList<ReadOnlyMemory<char>>(4),
        Length = 0,
        Offset = -1
    };

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
        int lineIndex = _lines.BinarySearchBy<int, LineOffsetSelector>(offset);

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
    /// 在全局字符偏移处插入一批文本段，段内可包含换行符。
    /// </summary>
    public void Insert(int offset, ReadOnlySpan<ReadOnlyMemory<char>> segments) {
        if (segments.IsEmpty)
            return;
        if (offset < 0 || offset > Length)
            throw new ArgumentOutOfRangeException(nameof(offset));

        EnsureDocumentInitialized();
        var (line, column) = GetPosition(offset);
        InsertNormalizedSegments(line, column, segments);
    }

    /// <summary>
    /// 在全局字符偏移处插入单个文本段。
    /// </summary>
    public void Insert(int offset, ReadOnlyMemory<char> segment) {
        if (segment.IsEmpty)
            return;
        if (offset < 0 || offset > Length)
            throw new ArgumentOutOfRangeException(nameof(offset));

        EnsureDocumentInitialized();
        var (line, column) = GetPosition(offset);
        InsertNormalizedSegment(ref line, ref column, segment);
    }

    /// <summary>
    /// 在指定行列位置插入一批文本段，段内可包含换行符。
    /// </summary>
    public void Insert(int lineIndex, int column, ReadOnlySpan<ReadOnlyMemory<char>> segments) {
        if (segments.IsEmpty)
            return;
        if (lineIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(lineIndex));

        bool appendAtEnd = lineIndex == _lines.Count;

        EnsureDocumentInitialized();

        if (appendAtEnd) {
            if (column != 0)
                throw new ArgumentOutOfRangeException(nameof(column));
            Insert(Length, segments);
            return;
        }

        if ((uint)lineIndex >= (uint)_lines.Count)
            throw new ArgumentOutOfRangeException(nameof(lineIndex));

        ref var line = ref _lines[lineIndex];
        if (column < 0 || column > line.Length)
            throw new ArgumentOutOfRangeException(nameof(column));

        InsertNormalizedSegments(lineIndex, column, segments);
    }

    /// <summary>
    /// 在指定行列位置插入单个文本段。
    /// </summary>
    public void Insert(int lineIndex, int column, ReadOnlyMemory<char> segment) {
        if (segment.IsEmpty)
            return;
        if (lineIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(lineIndex));

        bool appendAtEnd = lineIndex == _lines.Count;

        EnsureDocumentInitialized();

        if (appendAtEnd) {
            if (column != 0)
                throw new ArgumentOutOfRangeException(nameof(column));
            Insert(Length, segment);
            return;
        }

        if ((uint)lineIndex >= (uint)_lines.Count)
            throw new ArgumentOutOfRangeException(nameof(lineIndex));

        ref var line = ref _lines[lineIndex];
        if (column < 0 || column > line.Length)
            throw new ArgumentOutOfRangeException(nameof(column));

        var currentLine = lineIndex;
        var currentColumn = column;
        InsertNormalizedSegment(ref currentLine, ref currentColumn, segment);
    }

    #endregion
}
