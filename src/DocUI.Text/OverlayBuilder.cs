using System;

namespace DocUI.Text;

/// <summary>
/// 渲染期叠加层生成器。
/// 基于 <see cref="SegmentListBuilder"/> 的原始坐标，声明式地添加叠加标记。
/// 调用 <see cref="Build"/> 时统一应用所有叠加操作到底层 builder。
/// </summary>
/// <remarks>
/// <para>
/// 本类的所有操作都基于原始文本的坐标系（即 Build 调用前的坐标），
/// 不会因为先前的插入而导致坐标漂移。这使得调用顺序无关紧要。
/// </para>
/// <para>
/// 典型用例：为选区添加首尾标记、为代码块添加围栏、插入行号等渲染期装饰。
/// </para>
/// </remarks>
public class OverlayBuilder {
    /// <summary>
    /// 表示一个待应用的叠加操作。
    /// </summary>
    private readonly struct PendingOverlay : IComparable<PendingOverlay> {
        /// <summary>基于原始文本的全局字符偏移。</summary>
        public readonly int Offset;

        /// <summary>要插入的内容。</summary>
        public readonly ReadOnlyMemory<char> Content;

        /// <summary>
        /// 优先级，用于同一 offset 有多个插入时的排序。
        /// 数值越小越先插入（即在最终文本中越靠前）。
        /// </summary>
        public readonly int Priority;

        /// <summary>声明顺序，用于在 offset 和 priority 完全相同时保持稳定排序。</summary>
        public readonly int Sequence;

        public PendingOverlay(int offset, ReadOnlyMemory<char> content, int priority, int sequence) {
            Offset = offset;
            Content = content;
            Priority = priority;
            Sequence = sequence;
        }

        /// <summary>
        /// 比较器：先按 Offset 升序，再按 Priority 升序。
        /// </summary>
        public int CompareTo(PendingOverlay other) {
            int cmp = Offset.CompareTo(other.Offset);
            if (cmp != 0) return cmp;
            cmp = Priority.CompareTo(other.Priority);
            return cmp != 0 ? cmp : Sequence.CompareTo(other.Sequence);
        }
    }

    private readonly SegmentListBuilder _builder;
    private readonly List<PendingOverlay> _pendingOverlays = new();

    /// <summary>
    /// 创建一个新的叠加层生成器。
    /// </summary>
    /// <param name="builder">已初始化的段列表构建器，包含原始文本。</param>
    public OverlayBuilder(SegmentListBuilder builder) {
        _builder = builder ?? throw new ArgumentNullException(nameof(builder));
    }

    /// <summary>当前文本长度。</summary>
    public int Length => _builder.Length;

    /// <summary>已声明的叠加操作数量。</summary>
    public int PendingCount => _pendingOverlays.Count;

    /// <summary>总行数。</summary>
    public int LineCount => _builder.LineCount;

    #region 基础插入操作

    /// <summary>
    /// 在指定偏移处插入标记。
    /// </summary>
    /// <param name="offset">基于原始文本的字符偏移。</param>
    /// <param name="content">要插入的内容。</param>
    /// <param name="priority">优先级（默认 0，数值越小在同一位置越靠前）。</param>
    /// <exception cref="ArgumentOutOfRangeException">偏移超出原始文本范围。</exception>
    public void InsertAt(int offset, ReadOnlyMemory<char> content, int priority = 0) {
        ValidateOffset(offset);
        AddOverlay(offset, content, priority);
    }

    /// <summary>
    /// 在指定偏移处插入标记。
    /// </summary>
    public void InsertAt(int offset, string content, int priority = 0) {
        ArgumentNullException.ThrowIfNull(content);
        InsertAt(offset, content.AsMemory(), priority);
    }

    private void AddOverlay(int offset, ReadOnlyMemory<char> content, int priority) {
        if (content.IsEmpty)
            return;

        _pendingOverlays.Add(new PendingOverlay(offset, content, priority, _pendingOverlays.Count));
    }

    #endregion

    #region 行列插入操作

    /// <summary>
    /// 在指定行列位置插入内容。
    /// </summary>
    /// <param name="line">行索引（0-based）。</param>
    /// <param name="column">列偏移（0-based）。</param>
    /// <param name="content">要插入的内容。</param>
    /// <param name="priority">优先级（默认 0，数值越小在同一位置越靠前）。</param>
    /// <exception cref="ArgumentOutOfRangeException">行或列超出范围。</exception>
    public void InsertAtLine(int line, int column, ReadOnlyMemory<char> content, int priority = 0) {
        // GetOffset 内部会验证 line/column 范围
        int offset = _builder.GetOffset(line, column);
        AddOverlay(offset, content, priority);
    }

    /// <summary>
    /// 在指定行列位置插入内容。
    /// </summary>
    public void InsertAtLine(int line, int column, string content, int priority = 0) {
        ArgumentNullException.ThrowIfNull(content);
        InsertAtLine(line, column, content.AsMemory(), priority);
    }

    /// <summary>
    /// 使用行列范围包围文本。
    /// </summary>
    public void SurroundRangeLines(
        int startLine,
        int startColumn,
        int endLine,
        int endColumn,
        ReadOnlyMemory<char> prefix,
        ReadOnlyMemory<char> suffix,
        int prefixPriority = 0,
        int suffixPriority = 0) {
        int start = _builder.GetOffset(startLine, startColumn);
        int end = _builder.GetOffset(endLine, endColumn);
        SurroundRange(start, end, prefix, suffix, prefixPriority, suffixPriority);
    }

    /// <summary>
    /// 使用行列范围包围文本。
    /// </summary>
    public void SurroundRangeLines(
        int startLine,
        int startColumn,
        int endLine,
        int endColumn,
        string prefix,
        string suffix,
        int prefixPriority = 0,
        int suffixPriority = 0) {
        ArgumentNullException.ThrowIfNull(prefix);
        ArgumentNullException.ThrowIfNull(suffix);
        SurroundRangeLines(startLine, startColumn, endLine, endColumn,
            prefix.AsMemory(), suffix.AsMemory(), prefixPriority, suffixPriority);
    }

    #endregion

    #region 包围操作

    /// <summary>
    /// 用前缀和后缀包围指定范围。
    /// </summary>
    /// <param name="start">范围起始偏移（含）。</param>
    /// <param name="end">范围结束偏移（不含）。</param>
    /// <param name="prefix">前缀内容。</param>
    /// <param name="suffix">后缀内容。</param>
    /// <param name="prefixPriority">前缀优先级（默认 0）。</param>
    /// <param name="suffixPriority">后缀优先级（默认 0）。</param>
    /// <exception cref="ArgumentOutOfRangeException">范围超出原始文本。</exception>
    /// <exception cref="ArgumentException">start > end。</exception>
    public void SurroundRange(
        int start,
        int end,
        ReadOnlyMemory<char> prefix,
        ReadOnlyMemory<char> suffix,
        int prefixPriority = 0,
        int suffixPriority = 0) {
        ValidateRange(start, end);

        if (!prefix.IsEmpty) {
            AddOverlay(start, prefix, prefixPriority);
        }
        if (!suffix.IsEmpty) {
            AddOverlay(end, suffix, suffixPriority);
        }
    }

    /// <summary>
    /// 用前缀和后缀包围指定范围。
    /// </summary>
    public void SurroundRange(int start, int end, string prefix, string suffix,
        int prefixPriority = 0, int suffixPriority = 0) {
        ArgumentNullException.ThrowIfNull(prefix);
        ArgumentNullException.ThrowIfNull(suffix);
        SurroundRange(start, end, prefix.AsMemory(), suffix.AsMemory(), prefixPriority, suffixPriority);
    }

    #endregion

    #region 构建

    /// <summary>
    /// 应用所有叠加操作到底层 <see cref="SegmentListBuilder"/>。
    /// </summary>
    /// <returns>包含原始文本和所有叠加内容的段列表构建器。</returns>
    /// <remarks>
    /// 叠加操作按原始坐标排序后，从后往前插入以避免坐标漂移。
    /// 调用后 pending 列表被清空，可继续添加新的叠加操作。
    /// </remarks>
    public SegmentListBuilder Build() {
        if (_pendingOverlays.Count == 0) {
            return _builder;
        }

        // 排序：按 offset 升序，同 offset 按 priority 升序
        _pendingOverlays.Sort();

        // 从后往前插入，这样前面的 offset 不会受影响
        for (int i = _pendingOverlays.Count - 1; i >= 0; i--) {
            var overlay = _pendingOverlays[i];
            _builder.Insert(overlay.Offset, overlay.Content);
        }

        _pendingOverlays.Clear();

        return _builder;
    }

    /// <summary>
    /// 清除所有待应用的叠加操作。
    /// </summary>
    public void Clear() {
        _pendingOverlays.Clear();
    }

    #endregion

    #region 验证

    private void ValidateOffset(int offset) {
        int length = _builder.Length;
        if (offset < 0 || offset > length) {
            throw new ArgumentOutOfRangeException(nameof(offset),
                $"Offset {offset} is out of range [0, {length}].");
        }
    }

    private void ValidateRange(int start, int end) {
        int length = _builder.Length;
        if (start < 0 || start > length) {
            throw new ArgumentOutOfRangeException(nameof(start),
                $"Start {start} is out of range [0, {length}].");
        }
        if (end < 0 || end > length) {
            throw new ArgumentOutOfRangeException(nameof(end),
                $"End {end} is out of range [0, {length}].");
        }
        if (start > end) {
            throw new ArgumentException($"Start {start} must not be greater than end {end}.");
        }
    }

    #endregion
}
