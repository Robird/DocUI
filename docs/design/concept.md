## User Story - Snapshot TextBox for DocUI

- 作为渲染代理，我需要从 `PieceTree` 获取最新快照，并在不破坏原文本坐标的情况下注入多个选区、光标和高亮标记。
- 作为 LLM 使用者，我希望在 Markdown 代码围栏里看到稳定的行号、图例说明，以及不会被 ``` 冲突破坏的围栏标记。
- 作为控件开发者，我要能通过 `ITextBuffer` 提供的 C Style 行号 API（`lineIndex` + `ReadOnlyMemory<char>` + 手动长度）描述任意插入/替换，方便流水线每一步都能在零分配下构造新的渲染视图。
- 验收要点：标记顺序与 Legend 一致；行号不会因为插入而错位；支持多种 Selection 类型（主选区、副选区、只读光标）。

> 多选区背景：LLM 执行 `str_replace` 时 `oldText` 可能匹配多次，通过 DocUI 的多个选区提示可结合选区 id 做多选一或批量确认，降低误替换风险。

### Interaction Outline

1. TextBox 渲染前调用 `TextBufferNative.Freeze(buffer, out TextReadOnly view, out ReleaseHandle lease)`，渲染阶段仅使用该只读句柄访问行内容，结束后再 `lease.Dispose()`。
2. 将 `Selections` 按起点倒序，借助 `MarkerPalette.Attach(view, range)` 分配安全 token，并通过 `OverlayBuilder.InsertRange` 向可变 overlay 注入 start/end 标记，同时积累 Legend 文本。
3. 调用 `OverlayBuilder.SetLinePrefix(lineIndex, prefix)` 为每行写入形如 `"  42| "` 的行号前缀，避免复制正文切片。
4. 通过 `OverlayHelpers.WrapWithFence(overlay)` 基于 `OverlayBuilder.MaxTickRun` 计算安全围栏，再利用 `OverlayBuilder.InsertLine` 把围栏包裹在正文前后。
5. 最后将 `LegendWriter` 产生的提示行交给 `LegendWidget.Render`，并在 Markdown 输出中放在围栏之前。

### Pseudocode Draft

```csharp
using System;
using System.Buffers;
using System.Collections.Generic;

// Application skeleton ------------------------------------------------------
sealed class TextBox {
    public TextBufferHandle Buffer { get; init; }
    public SelectionRange[] Selections { get; init; } = Array.Empty<SelectionRange>();

    private readonly MarkerPalette _palette = new();
    private readonly LegendWidget _legend = new();
    private readonly FenceWidget _fence = new();

    public void Render(MarkdownWriter writer) {
        if (!TextBufferNative.Freeze(Buffer, out var view, out var lease)) {
            return;
        }

        using (lease)
        using (var overlay = OverlayBuilder.Create(view))
        using (var legend = new LegendWriter()) {
            SelectionHelpers.ApplySelections(overlay, legend, view, Selections, _palette);
            OverlayHelpers.InjectLineNumbers(overlay);
            OverlayHelpers.WrapWithFence(overlay);

            _legend.Render(legend, writer);
            _fence.Render(overlay, writer);
        }
    }
}

// Text buffer contracts (C Style) ------------------------------------------
readonly record struct TextBufferHandle(IntPtr Ptr);
readonly ref struct TextReadOnly {
    internal readonly IntPtr Ptr;
    public TextReadOnly(IntPtr handle) => Ptr = handle;
}

readonly struct ReleaseHandle : IDisposable {
    private readonly Action? _dispose;
    public ReleaseHandle(Action? dispose) => _dispose = dispose;
    public void Dispose() => _dispose?.Invoke();
}

readonly struct TextLineView {
    public TextLineView(ReadOnlyMemory<char> content) {
        Content = content;
    }

    public ReadOnlyMemory<char> Content { get; }
    public int Length => Content.Length;
}

static class TextBufferNative {
    public static bool Freeze(TextBufferHandle buffer, out TextReadOnly view, out ReleaseHandle lease) {
        throw new NotImplementedException();
    }
}

static class TextReadOnlyApi {
    public static int LineCount(TextReadOnly view) => throw new NotImplementedException();
    public static void GetLine(TextReadOnly view, int lineIndex, out TextLineView dst) => throw new NotImplementedException();
    public static bool Contains(TextReadOnly view, ReadOnlySpan<char> token) => throw new NotImplementedException();
}

sealed class OverlayBuilder : IDisposable {
    public static OverlayBuilder Create(TextReadOnly view) => throw new NotImplementedException();
    public void InsertRange(int absoluteOffset, ReadOnlySpan<char> token) => throw new NotImplementedException();
    public void SetLinePrefix(int lineIndex, ReadOnlySpan<char> prefix) => throw new NotImplementedException();
    public void InsertLine(int lineIndex, ReadOnlySpan<char> content) => throw new NotImplementedException();
    public int LineCount => throw new NotImplementedException();
    public int MaxTickRun => throw new NotImplementedException();
    public void Dispose() => throw new NotImplementedException();
}

// Selection + marker infrastructure ----------------------------------------
enum SelectionKind { Primary, Secondary, Cursor }

readonly record struct SelectionRange(int Id, int Start, int End, SelectionKind Kind, string? Note = null) {
    public bool IsTemporary => Id < 0;
}

readonly record struct MarkerSymbol(string Key, string StartToken, string EndToken, string Description);

readonly record struct MarkerReservation(SelectionRange Range, MarkerSymbol Symbol, string Legend);

sealed class MarkerPalette {
    private readonly Dictionary<(SelectionKind Kind, int Id), MarkerSymbol> _stable = new();
    private readonly Queue<MarkerSymbol>[] _pools = new[] {
        new Queue<MarkerSymbol>(),
        new Queue<MarkerSymbol>(),
        new Queue<MarkerSymbol>()
    };

    private int _counter;

    public MarkerReservation Attach(TextReadOnly view, SelectionRange range) {
        if (!range.IsTemporary && _stable.TryGetValue((range.Kind, range.Id), out var cached)) {
            return CreateReservation(range, cached);
        }

        var pool = _pools[(int)range.Kind];
        while (pool.Count == 0) {
            var symbol = CreateSymbol(range.Kind, _counter++);
            if (TextReadOnlyApi.Contains(view, symbol.StartToken) || TextReadOnlyApi.Contains(view, symbol.EndToken)) {
                continue; // token 已存在则继续退避
            }
            pool.Enqueue(symbol);
        }

        var assigned = pool.Dequeue();
        if (!range.IsTemporary) {
            _stable[(range.Kind, range.Id)] = assigned;
        }

        return CreateReservation(range, assigned);
    }

    private static MarkerSymbol CreateSymbol(SelectionKind kind, int ordinal) {
        var key = $"{kind.ToString()[0]}{ordinal}";
        return new MarkerSymbol(key, $"<{key}>", $"</{key}>", kind.ToString());
    }

    private static MarkerReservation CreateReservation(SelectionRange range, MarkerSymbol symbol) {
        var note = range.Note ?? range.Kind.ToString();
        var legend = $"[{symbol.Key}] {note} ({range.Start}, {range.End})";
        return new MarkerReservation(range, symbol, legend);
    }
}

// Overlay helpers -----------------------------------------------------------
sealed class ReservationBatch : IDisposable {
    private MarkerReservation[] _buffer;
    private int _count;

    private ReservationBatch(int capacity) {
        _buffer = capacity == 0
            ? Array.Empty<MarkerReservation>()
            : ArrayPool<MarkerReservation>.Shared.Rent(capacity);
        _count = 0;
    }

    public static ReservationBatch Rent(int capacity) => new(capacity);

    public void Add(in MarkerReservation reservation) => _buffer[_count++] = reservation;

    public ReadOnlySpan<MarkerReservation> Written => _buffer.AsSpan(0, _count);

    public void Dispose() {
        if (_buffer.Length > 0) {
            ArrayPool<MarkerReservation>.Shared.Return(_buffer, clearArray: true);
        }
        _buffer = Array.Empty<MarkerReservation>();
        _count = 0;
    }
}

static class SelectionHelpers {
    public static void ApplySelections(OverlayBuilder overlay, LegendWriter legend, TextReadOnly view, IReadOnlyList<SelectionRange> selections, MarkerPalette palette) {
        var ordered = CopyAndSortDescending(selections);
        using var reservations = ReservationBatch.Rent(ordered.Length);

        foreach (var current in ordered) {
            var reservation = palette.Attach(view, current);
            overlay.InsertRange(reservation.Range.End, reservation.Symbol.EndToken);
            overlay.InsertRange(reservation.Range.Start, reservation.Symbol.StartToken);
            reservations.Add(reservation);
        }

        var written = reservations.Written;
        for (var i = written.Length - 1; i >= 0; i--) {
            legend.Add(written[i].Legend);
        }
    }

    private static SelectionRange[] CopyAndSortDescending(IReadOnlyList<SelectionRange> selections) {
        var buffer = new SelectionRange[selections.Count];
        for (var i = 0; i < buffer.Length; i++) {
            buffer[i] = selections[i];
        }

        Array.Sort(buffer, static (a, b) => b.Start.CompareTo(a.Start));
        return buffer;
    }
}

static class OverlayHelpers {
    public static void InjectLineNumbers(OverlayBuilder overlay) {
        for (var line = overlay.LineCount - 1; line >= 0; line--) {
            var prefix = $"{line + 1,4}| ";
            overlay.SetLinePrefix(line, prefix);
        }
    }

    public static void WrapWithFence(OverlayBuilder overlay) {
        var ticks = Math.Max(3, overlay.MaxTickRun + 1);
        var fence = new string('`', ticks);
        overlay.InsertLine(0, fence);
        overlay.InsertLine(overlay.LineCount, fence);
    }
}

// Legend + fence widgets ----------------------------------------------------
sealed class LegendWriter : IDisposable {
    private readonly List<string> _lines = new();
    public void Add(string line) => _lines.Add(line);
    public IReadOnlyList<string> Lines => _lines;
    public void Dispose() { }
}

sealed class LegendWidget {
    public void Render(LegendWriter legend, MarkdownWriter writer) => throw new NotImplementedException();
}

sealed class FenceWidget {
    public void Render(OverlayBuilder overlay, MarkdownWriter writer) => throw new NotImplementedException();
}

sealed class MarkdownWriter { }
```
