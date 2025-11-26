using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DocUI.Text;

/// <summary>
/// Immutable snapshot implementation backed by per-line segments.
/// Editing helpers live on this type to keep read-only.
/// </summary>
public sealed class OverlayImmutable {
    private const string DefaultLineEnding = "\n";
    private readonly OverlayLineImmutable[] _lines;
    private readonly int[] _lineStarts;
    private readonly int _length; // Logical length that excludes implicit line endings.
    private string? _cachedText;

    private OverlayImmutable(IReadOnlyList<LineSegment[]> lines) {
        if (lines is null) {
            throw new ArgumentNullException(nameof(lines));
        }

        var lineCount = lines.Count;
        _lines = new OverlayLineImmutable[lineCount];
        _lineStarts = lineCount == 0 ? Array.Empty<int>() : new int[lineCount];

        var offset = 0;

        for (var i = 0; i < lineCount; i++) {
            var normalized = lines[i]; // 先简化
            _lineStarts[i] = offset;
            var line = new OverlayLineImmutable(this, normalized, offset);
            _lines[i] = line;
            offset += line.Length;
        }

        _length = offset;
    }

    /// <summary>
    /// Creates a snapshot from raw text, splitting on CR and LF and dropping empty lines.
    /// </summary>
    public static OverlayImmutable FromText(string text) {
        if (text is null) {
            throw new ArgumentNullException(nameof(text));
        }

        var builder = new SegmentLineBuilder();
        ReadOnlyMemory<char> chunk = text.AsMemory();
        builder.AddChunk(chunk);
        return new OverlayImmutable(builder.Build());
    }

    /// <summary>
    /// Creates a snapshot from raw memory, splitting on CR and LF and dropping empty lines.
    /// </summary>
    public static OverlayImmutable FromText(ReadOnlyMemory<char> text) {
        if (text.IsEmpty) {
            return FromText(string.Empty);
        }

        var builder = new SegmentLineBuilder();
        builder.AddChunk(text);
        return new OverlayImmutable(builder.Build());
    }

    /// <summary>
    /// Creates a snapshot from pre-split lines.
    /// </summary>
    public static OverlayImmutable FromLines(IEnumerable<string> lines) {
        if (lines is null) {
            throw new ArgumentNullException(nameof(lines));
        }

        var buffer = new List<LineSegment[]>();
        foreach (var line in lines) {
            buffer.Add(CreateSegments((line ?? string.Empty).AsMemory()));
        }

        if (buffer.Count == 0) {
            buffer.Add(Array.Empty<LineSegment>());
        }

        return new OverlayImmutable(buffer);
    }

    /// <summary>
    /// Creates a snapshot by streaming already-materialized memory chunks without copying them.
    /// </summary>
    public static OverlayImmutable FromMemoryChunks(IEnumerable<ReadOnlyMemory<char>> chunks) {
        if (chunks is null) {
            throw new ArgumentNullException(nameof(chunks));
        }

        var builder = new SegmentLineBuilder();
        foreach (var chunk in chunks) {
            builder.AddChunk(chunk);
        }

        return new OverlayImmutable(builder.Build());
    }

    /// <summary>
    /// Returns the provided snapshot, ensuring null checks are centralized.
    /// </summary>
    public static OverlayImmutable FromSnapshot(OverlayImmutable snapshot) =>
        snapshot ?? throw new ArgumentNullException(nameof(snapshot));

    /// <summary>
    /// Appends another snapshot's lines to the current one.
    /// </summary>
    public OverlayImmutable WithAppendText(OverlayImmutable other) {
        if (other is null || other.LineCount == 0) {
            return this;
        }

        var lines = CloneLines(other.LineCount);
        var start = _lines.Length;
        for (var i = 0; i < other._lines.Length; i++) {
            lines[start + i] = other._lines[i].ExportSegments();
        }

        return new OverlayImmutable(lines);
    }

    /// <summary>
    /// Appends a single <see cref="ReadOnlyMemory{Char}"/> line at the end of the snapshot.
    /// </summary>
    public OverlayImmutable WithAppendLine(ReadOnlyMemory<char> content) =>
        WithInsertLine(LineCount, content);

    /// <summary>
    /// Inserts a new <see cref="ReadOnlyMemory{Char}"/> line at the specified zero-based index.
    /// </summary>
    public OverlayImmutable WithInsertLine(int index, ReadOnlyMemory<char> content) {
        if (index < 0 || index > LineCount) {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        var lines = CloneLines(1);
        Array.Copy(lines, index, lines, index + 1, _lines.Length - index);
        lines[index] = CreateSegments(content);
        return new OverlayImmutable(lines);
    }

    /// <summary>
    /// Replaces the specified line using the provided mutator.
    /// </summary>
    public OverlayImmutable ReplaceLine(int index, Func<OverlayLineImmutable, string> mutate) {
        if (index < 0 || index >= LineCount) {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        var nextValue = mutate?.Invoke(_lines[index]) ?? string.Empty;
        var lines = CloneLines();
        lines[index] = CreateSegments(nextValue.AsMemory());
        return new OverlayImmutable(lines);
    }

    /// <summary>
    /// Replaces the segment sequence for the specified line using the provided mutator.
    /// </summary>
    public OverlayImmutable ReplaceLineSegments(int index, Func<OverlayLineImmutable, IEnumerable<LineSegment>> mutate) {
        if (index < 0 || index >= LineCount) {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        if (mutate is null) {
            throw new ArgumentNullException(nameof(mutate));
        }

        var nextSegments = MaterializeSegments(mutate(_lines[index]));
        var lines = CloneLines();
        lines[index] = nextSegments;
        return new OverlayImmutable(lines);
    }

    /// <summary>
    /// Performs an absolute replacement using character offsets, returning a new snapshot.
    /// </summary>
    public OverlayImmutable WithReplace(int start, int length, OverlayImmutable replacement) {
        if (start < 0 || start > _length) {
            throw new ArgumentOutOfRangeException(nameof(start));
        }
        if (length < 0 || start + length > _length) {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        var current = ToString();
        var physicalStart = ToPhysicalIndex(start);
        var physicalEnd = ToPhysicalIndex(start + length);
        var removalLength = physicalEnd - physicalStart;

        string? replacementText = replacement is null ? null : replacement.ToString();
        var replacementLength = replacementText?.Length ?? 0;

        var builder = new StringBuilder(current.Length - removalLength + replacementLength);
        builder.Append(current, 0, physicalStart);
        if (replacementText is not null) {
            builder.Append(replacementText);
        }
        builder.Append(current, physicalEnd, current.Length - physicalEnd);
        var nextText = builder.ToString();
        return FromText(nextText);
    }

    /// <inheritdoc />
    public int Length => _length;

    /// <inheritdoc />
    public int LineCount => _lines.Length;

    /// <inheritdoc />
    public OverlayLineImmutable GetLine(int index) => _lines[index];

    /// <inheritdoc />
    public (int Line, int Column) GetLinePosition(int offset) {
        if (LineCount == 0) {
            return (0, 0);
        }
        if (offset < 0 || offset > Length) {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        var lineIndex = FindLineIndex(offset);
        if (lineIndex < 0) {
            lineIndex = ~lineIndex - 1;
        }
        lineIndex = Math.Clamp(lineIndex, 0, LineCount - 1);
        var lineStart = _lineStarts[lineIndex];
        var lineLength = _lines[lineIndex].Length;
        var column = Math.Clamp(offset - lineStart, 0, lineLength);
        return (lineIndex, column);
    }

    /// <inheritdoc />
    public bool Contains(ReadOnlySpan<char> value) {
        if (value.Length == 0) {
            return true;
        }

        return ToString().AsSpan().Contains(value, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public IEnumerable<OverlayLineImmutable> EnumerateLines() => _lines;

    /// <inheritdoc />
    public int Count => _lines.Length;

    /// <inheritdoc />
    public OverlayLineImmutable this[int index] => _lines[index];

    /// <inheritdoc />
    public IEnumerator<OverlayLineImmutable> GetEnumerator() {
        foreach (var line in _lines) {
            yield return line;
        }
    }

    /// <summary>
    /// Returns the concatenated textual representation of the snapshot using <c>"\n"</c> as the separator.
    /// </summary>
    public override string ToString() {
        if (_cachedText is not null) {
            return _cachedText;
        }

        if (LineCount == 0) {
            _cachedText = string.Empty;
            return _cachedText;
        }

        var separatorBudget = Math.Max(0, LineCount - 1) * DefaultLineEnding.Length;
        var builder = new StringBuilder(_length + separatorBudget);
        for (var i = 0; i < LineCount; i++) {
            var segments = _lines[i].ExportSegments();
            for (var j = 0; j < segments.Length; j++) {
                var memory = segments[j].Content;
                if (!memory.IsEmpty) {
                    builder.Append(memory.Span);
                }
            }

            if (i < LineCount - 1) {
                builder.Append(DefaultLineEnding);
            }
        }

        _cachedText = builder.ToString();
        return _cachedText;
    }

    private int ToPhysicalIndex(int logicalOffset) {
        if (logicalOffset <= 0 || LineCount <= 1) {
            return logicalOffset;
        }

        var (line, _) = GetLinePosition(logicalOffset);
        var separatorCount = Math.Min(Math.Max(line, 0), Math.Max(LineCount - 1, 0));
        if (separatorCount == 0) {
            return logicalOffset;
        }

        return logicalOffset + separatorCount * DefaultLineEnding.Length;
    }

    private int FindLineIndex(int offset) {
        var index = Array.BinarySearch(_lineStarts, 0, LineCount, offset);
        if (index >= 0) {
            while (index > 0 && _lineStarts[index - 1] == _lineStarts[index]) {
                index--;
            }
        }

        return index;
    }

    private LineSegment[][] CloneLines(int additionalCapacity = 0) {
        if (additionalCapacity < 0) {
            throw new ArgumentOutOfRangeException(nameof(additionalCapacity));
        }

        var baseLength = _lines.Length;
        var totalLength = baseLength + additionalCapacity;
        if (totalLength == 0) {
            return Array.Empty<LineSegment[]>();
        }

        var clone = new LineSegment[totalLength][];
        for (var i = 0; i < baseLength; i++) {
            clone[i] = _lines[i].ExportSegments();
        }

        return clone;
    }

    private sealed class SegmentLineBuilder {
        private readonly List<LineSegment[]> _lines = new();
        private readonly List<LineSegment> _currentLine = new();

        public void AddChunk(ReadOnlyMemory<char> chunk) {
            if (chunk.IsEmpty) {
                return;
            }

            var span = chunk.Span;
            var index = 0;
            var segmentStart = 0;

            while (index < span.Length) {
                var currentChar = span[index];
                if (currentChar == '\r' || currentChar == '\n') {
                    if (index > segmentStart) {
                        _currentLine.Add(new LineSegment(chunk.Slice(segmentStart, index - segmentStart)));
                    }

                    index += 1;
                    CommitLine();
                    segmentStart = index;
                    continue;
                }

                index++;
            }

            if (segmentStart < span.Length) {
                _currentLine.Add(new LineSegment(chunk.Slice(segmentStart)));
            }
        }

        public List<LineSegment[]> Build() {
            CommitLine();
            if (_lines.Count == 0) {
                _lines.Add(Array.Empty<LineSegment>());
            }

            return _lines;
        }

        private void CommitLine() {
            if (_currentLine.Count == 0) {
                return;
            }

            _lines.Add(_currentLine.ToArray());
            _currentLine.Clear();
        }
    }

    private static LineSegment[] CreateSegments(ReadOnlyMemory<char> content) {
        if (content.IsEmpty) {
            return Array.Empty<LineSegment>();
        }

        return new[] { new LineSegment(content) };
    }

    private static LineSegment[] MaterializeSegments(IEnumerable<LineSegment> segments) {
        if (segments is null) {
            return Array.Empty<LineSegment>();
        }

        return segments.ToArray();
    }

}

/// <summary>
/// Snapshot line implementation backed by one or more segments.
/// </summary>
public sealed class OverlayLineImmutable {
    private readonly LineSegment[] _segments;
    private IReadOnlyList<LineSegment>? _segmentView;
    private IReadOnlyList<char>? _characters;
    private string? _cachedText;

    internal OverlayLineImmutable(OverlayImmutable snapshot, LineSegment[]? segments, int start) {
        Snapshot = snapshot;
        Start = start;
        _segments = segments is null || segments.Length == 0 ? Array.Empty<LineSegment>() : segments;
        Length = CalculateLength(_segments);
    }

    /// <inheritdoc />
    public OverlayImmutable Snapshot { get; }

    /// <inheritdoc />
    public int Start { get; }

    /// <inheritdoc />
    public int Length { get; }

    /// <inheritdoc />
    public IReadOnlyList<char> Characters =>
        _characters ??= _segments.Length == 0 ? Array.Empty<char>() : new SegmentCharView(_segments);

    /// <summary>
    /// Full list of segments that compose the line, including metadata.
    /// </summary>
    public IReadOnlyList<LineSegment> Segments =>
        _segmentView ??= _segments.Length == 0 ? SegmentArrayView.Empty : new SegmentArrayView(_segments);

    /// <inheritdoc />
    public int Count => _segments.Length;

    /// <inheritdoc />
    public ReadOnlyMemory<char> this[int index] => _segments[index].Content;

    /// <inheritdoc />
    public IEnumerator<ReadOnlyMemory<char>> GetEnumerator() {
        for (var i = 0; i < _segments.Length; i++) {
            yield return _segments[i].Content;
        }
    }

    /// <summary>
    /// Materializes the textual content for callers that still expect string-based workflows.
    /// </summary>
    public override string ToString() {
        if (_cachedText is not null) {
            return _cachedText;
        }

        if (_segments.Length == 0) {
            _cachedText = string.Empty;
            return _cachedText;
        }

        var builder = new StringBuilder(Length);
        for (var i = 0; i < _segments.Length; i++) {
            var memory = _segments[i].Content;
            if (!memory.IsEmpty) {
                builder.Append(memory.Span);
            }
        }

        _cachedText = builder.ToString();
        return _cachedText;
    }

    internal LineSegment[] ExportSegments() => _segments;

    private static int CalculateLength(LineSegment[] segments) {
        if (segments.Length == 0) {
            return 0;
        }

        var total = 0;
        for (var i = 0; i < segments.Length; i++) {
            total += segments[i].Content.Length;
        }

        return total;
    }
}

/// <summary>
/// Represents a logical chunk of text plus optional marker metadata.
/// </summary>
public readonly struct LineSegment {
    public LineSegment(ReadOnlyMemory<char> content) {
        Content = content;
    }

    /// <summary>
    /// Underlying text for this segment.
    /// </summary>
    public ReadOnlyMemory<char> Content { get; }
}

/// <summary>
/// Zero-copy character projection built on top of per-line segments using prefix sums + binary search.
/// </summary>
file sealed class SegmentCharView : IReadOnlyList<char> {
    private readonly LineSegment[] _segments;
    private readonly int[] _segmentStarts;
    private readonly int _length;

    public SegmentCharView(LineSegment[] segments) {
        _segments = segments;
        _segmentStarts = segments.Length == 0 ? Array.Empty<int>() : new int[segments.Length];

        var offset = 0;
        for (var i = 0; i < segments.Length; i++) {
            _segmentStarts[i] = offset;
            offset += segments[i].Content.Length;
        }

        _length = offset;
    }

    public char this[int index] {
        get {
            if ((uint)index >= (uint)_length) {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            if (_segments.Length == 0) {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            var segmentIndex = LocateSegment(index);
            var within = index - _segmentStarts[segmentIndex];
            return _segments[segmentIndex].Content.Span[within];
        }
    }

    public int Count => _length;

    public IEnumerator<char> GetEnumerator() {
        for (var i = 0; i < _segments.Length; i++) {
            var memory = _segments[i].Content;
            var length = memory.Length;
            for (var j = 0; j < length; j++) {
                yield return memory.Span[j];
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private int LocateSegment(int index) {
        var low = 0;
        var high = _segmentStarts.Length - 1;

        while (low <= high) {
            var mid = low + ((high - low) >> 1);
            var start = _segmentStarts[mid];
            var end = mid + 1 < _segmentStarts.Length ? _segmentStarts[mid + 1] : _length;

            if (index < start) {
                high = mid - 1;
            }
            else if (index >= end) {
                low = mid + 1;
            }
            else {
                return mid;
            }
        }

        return Math.Clamp(low - 1, 0, _segmentStarts.Length - 1);
    }
}

file sealed class SegmentArrayView : IReadOnlyList<LineSegment> {
    public static readonly SegmentArrayView Empty = new(Array.Empty<LineSegment>());

    private readonly LineSegment[] _segments;

    public SegmentArrayView(LineSegment[] segments) => _segments = segments;

    public LineSegment this[int index] => _segments[index];

    public int Count => _segments.Length;

    public IEnumerator<LineSegment> GetEnumerator() {
        for (var i = 0; i < _segments.Length; i++) {
            yield return _segments[i];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

