## User Story - Snapshot TextBox for DocUI

- 作为渲染代理，我需要从 `PieceTree` 获取最新快照，并在不破坏原文本坐标的情况下注入多个选区、光标和高亮标记。
- 作为 LLM 使用者，我希望在 Markdown 代码围栏里看到稳定的行号、图例说明，以及不会被 ``` 冲突破坏的围栏标记。
- 作为控件开发者，我要能通过 `ITextBuffer` 提供的 C Style 行号 API（`lineIndex` + `ReadOnlyMemory<char>` + 手动长度）描述任意插入/替换，方便流水线每一步都能在零分配下构造新的渲染视图。
- 验收要点：标记顺序与 Legend 一致；行号不会因为插入而错位；支持多种 Selection 类型（主选区、副选区、只读光标）。

> 多选区背景：LLM 执行 `str_replace` 时 `oldText` 可能匹配多次，通过 DocUI 的多个选区提示可结合选区 id 做多选一或批量确认，降低误替换风险。

### Interaction Outline

1. TextBox 渲染前调用 `TextBuffer_Freeze(buffer, out TextReadOnly view, out ReleaseHandle lease)`，渲染阶段仅使用该只读句柄访问行内容，结束后再 `lease.Dispose()`。
2. 将 `Selections` 按起点倒序，借助 `MarkerPalette_Attach(palette, view, range, id, out reservation)` 分配安全 token，并通过 `OverlayBuilder_InsertRange` 向可变 overlay 注入 start/end 标记，同时积累 Legend 文本。
3. 调用 `OverlayBuilder_SetLinePrefix(lineIndex, prefix)` 为每行写入形如 `"  42| "` 的行号前缀，避免复制正文切片。
4. 通过 `WrapWithFence(overlay)` 基于 `OverlayBuilder_MaxTickRun` 计算安全围栏，再利用 `OverlayBuilder_InsertLine` 把围栏包裹在正文前后。
5. 最后将 `LegendWriter` 产生的提示行交给 `LegendWidget_Render`，并在 Markdown 输出中放在围栏之前。

### Pseudocode Draft

```c
// Application skeleton ------------------------------------------------------
typedef struct {
    TextBufferHandle Buffer;
    SelectionRange* Selections;
    int SelectionCount;
    MarkerPalette Palette;
    LegendWidget Legend;
    FenceWidget Fence;
} TextBox;

void TextBox_Render(TextBox* box, MarkdownWriter* writer) {
    TextReadOnly view;
    ReleaseHandle lease;
    if (!TextBuffer_Freeze(box->Buffer, &view, &lease)) {
        return;
    }

    OverlayBuilder overlay;
    OverlayBuilder_Init(&overlay, view);

    LegendWriter legend;
    LegendWriter_Init(&legend);

    ApplySelections(&overlay, &legend, view, box->Selections, box->SelectionCount, &box->Palette);
    InjectLineNumbers(&overlay);
    WrapWithFence(&overlay);

    LegendWidget_Render(&box->Legend, &legend, writer);
    FenceWidget_Render(&box->Fence, &overlay, writer);

    LegendWriter_Dispose(&legend);
    OverlayBuilder_Dispose(&overlay);
    ReleaseHandle_Dispose(&lease);
}

// Text buffer contracts (C Style) ------------------------------------------
typedef struct { void* Ptr; } TextBufferHandle;
typedef struct { void* Ptr; } TextReadOnly;

typedef struct {
    void (*Dispose)(void* State);
    void* State;
} ReleaseHandle;

typedef struct {
    ReadOnlyMemory<char> Content;
    int Length;
} TextLineView;

bool TextBuffer_Freeze(TextBufferHandle buffer, TextReadOnly* view, ReleaseHandle* lease);
int  TextReadOnly_LineCount(TextReadOnly view);
void TextReadOnly_GetLine(TextReadOnly view, int lineIndex, TextLineView* dst);
bool TextReadOnly_Contains(TextReadOnly view, ReadOnlySpan<char> token);

typedef struct {
    TextLineView* Lines;
    int LineCount;
} OverlayBuilder;

void OverlayBuilder_Init(OverlayBuilder* builder, TextReadOnly view);
void OverlayBuilder_InsertRange(OverlayBuilder* builder, int absoluteOffset, ReadOnlySpan<char> token);
void OverlayBuilder_SetLinePrefix(OverlayBuilder* builder, int lineIndex, ReadOnlySpan<char> prefix);
void OverlayBuilder_InsertLine(OverlayBuilder* builder, int lineIndex, ReadOnlySpan<char> content);
int  OverlayBuilder_LineCount(const OverlayBuilder* builder);
int  OverlayBuilder_MaxTickRun(const OverlayBuilder* builder);
void OverlayBuilder_Dispose(OverlayBuilder* builder);

// Selection + marker infrastructure ----------------------------------------
typedef enum { SelectionPrimary, SelectionSecondary, SelectionCursor, SelectionKindCount } SelectionKind;

typedef struct {
    int Id; // -1 表示临时选区
    int Start;
    int End;
    SelectionKind Kind;
    const char* Note;
} SelectionRange;

typedef struct {
    char StartToken[16];
    char EndToken[16];
    char Key[8];
    const char* Description;
} MarkerSymbol;

typedef struct {
    SelectionRange Range;
    MarkerSymbol Symbol;
    char Legend[96];
} MarkerReservation;

typedef struct {
    SelectionKind Kind;
    int SelectionId;
    MarkerSymbol Symbol;
} MarkerBinding;

typedef struct {
    MarkerSymbol Pools[SelectionKindCount][8];
    int PoolSizes[SelectionKindCount];
    MarkerBinding Stable[64];
    int StableCount;
    int Counter;
} MarkerPalette;

bool MarkerPalette_TryGetStable(const MarkerPalette* palette, SelectionKind kind, int selectionId, MarkerSymbol* symbol);
void MarkerPalette_RememberStable(MarkerPalette* palette, SelectionKind kind, int selectionId, MarkerSymbol symbol);
void EnqueueSymbol(MarkerPalette* palette, SelectionKind kind, MarkerSymbol symbol);
MarkerSymbol DequeueSymbol(MarkerPalette* palette, SelectionKind kind);
const char* KindName(SelectionKind kind);
void Format(char* buffer, size_t bufferSize, const char* format, ...);
void BuildFence(char* buffer, size_t bufferSize, int tickCount);

static void MarkerPalette_Attach(MarkerPalette* palette, TextReadOnly view, SelectionRange range, int requestedId, MarkerReservation* result) {
    MarkerSymbol symbol;
    if (MarkerPalette_TryGetStable(palette, range.Kind, requestedId, &symbol)) {
        // stable token already exists
    } else {
        while (palette->PoolSizes[range.Kind] == 0) {
            int ordinal = palette->Counter++;
            MarkerSymbol candidate = {0};
            const char* kindName = KindName(range.Kind);
            Format(candidate.Key, sizeof(candidate.Key), "%c%d", kindName[0], ordinal);
            Format(candidate.StartToken, sizeof(candidate.StartToken), "<%s>", candidate.Key);
            Format(candidate.EndToken, sizeof(candidate.EndToken), "</%s>", candidate.Key);
            candidate.Description = kindName;
            if (TextReadOnly_Contains(view, candidate.StartToken) || TextReadOnly_Contains(view, candidate.EndToken)) {
                continue; // token 已存在则继续退避
            }
            EnqueueSymbol(palette, range.Kind, candidate);
        }

        symbol = DequeueSymbol(palette, range.Kind);
        if (requestedId >= 0) {
            MarkerPalette_RememberStable(palette, range.Kind, requestedId, symbol);
        }
    }

    result->Range = range;
    result->Symbol = symbol;
    Format(result->Legend, sizeof(result->Legend), "[%s] %s (%d, %d)", symbol.Key, range.Note ? range.Note : KindName(range.Kind), range.Start, range.End);
}

bool MarkerPalette_TryGetStable(const MarkerPalette* palette, SelectionKind kind, int selectionId, MarkerSymbol* symbol) {
    if (selectionId < 0) {
        return false;
    }

    for (int i = 0; i < palette->StableCount; ++i) {
        MarkerBinding binding = palette->Stable[i];
        if (binding.Kind == kind && binding.SelectionId == selectionId) {
            *symbol = binding.Symbol;
            return true;
        }
    }
    return false;
}

void MarkerPalette_RememberStable(MarkerPalette* palette, SelectionKind kind, int selectionId, MarkerSymbol symbol) {
    if (selectionId < 0) {
        return;
    }

    for (int i = 0; i < palette->StableCount; ++i) {
        MarkerBinding* binding = &palette->Stable[i];
        if (binding->Kind == kind && binding->SelectionId == selectionId) {
            binding->Symbol = symbol;
            return;
        }
    }

    if (palette->StableCount < (int)(sizeof(palette->Stable) / sizeof(palette->Stable[0]))) {
        MarkerBinding* binding = &palette->Stable[palette->StableCount++];
        binding->Kind = kind;
        binding->SelectionId = selectionId;
        binding->Symbol = symbol;
    }
}

void EnqueueSymbol(MarkerPalette* palette, SelectionKind kind, MarkerSymbol symbol) {
    int index = palette->PoolSizes[kind];
    if (index < (int)(sizeof(palette->Pools[kind]) / sizeof(palette->Pools[kind][0]))) {
        palette->Pools[kind][index] = symbol;
        palette->PoolSizes[kind] = index + 1;
    }
}

MarkerSymbol DequeueSymbol(MarkerPalette* palette, SelectionKind kind) {
    int index = palette->PoolSizes[kind] - 1;
    palette->PoolSizes[kind] = index;
    return palette->Pools[kind][index];
}

const char* KindName(SelectionKind kind) {
    static const char* names[] = {"Primary", "Secondary", "Cursor"};
    return names[kind];
}

void Format(char* buffer, size_t bufferSize, const char* format, ...) {
    va_list args;
    va_start(args, format);
    vsnprintf(buffer, bufferSize, format, args);
    va_end(args);
}

void BuildFence(char* buffer, size_t bufferSize, int tickCount) {
    int count = tickCount < (int)bufferSize - 1 ? tickCount : (int)bufferSize - 1;
    for (int i = 0; i < count; ++i) {
        buffer[i] = '`';
    }
    buffer[count] = '\0';
}

// Overlay helpers -----------------------------------------------------------
typedef struct {
    const SelectionRange* Items;
    int Count;
} SelectionBatch;

SelectionBatch SortByStartDesc(const SelectionRange* selections, int count);

typedef struct {
    MarkerReservation* Items;
    int Count;
} ReservationBatch;

ReservationBatch RentReservationBuffer(int count);
void ReturnReservationBuffer(ReservationBatch* batch);

static void ApplySelections(OverlayBuilder* overlay, LegendWriter* legend, TextReadOnly view, const SelectionRange* selections, int count, MarkerPalette* palette) {
    SelectionBatch ordered = SortByStartDesc(selections, count);
    ReservationBatch reservations = RentReservationBuffer(ordered.Count);

    for (int i = 0; i < ordered.Count; ++i) {
        MarkerReservation reservation;
        const SelectionRange* current = &ordered.Items[i];
        MarkerPalette_Attach(palette, view, *current, current->Id, &reservation);

        OverlayBuilder_InsertRange(overlay, reservation.Range.End, reservation.Symbol.EndToken);
        OverlayBuilder_InsertRange(overlay, reservation.Range.Start, reservation.Symbol.StartToken);
        reservations.Items[reservations.Count++] = reservation;
    }

    for (int i = reservations.Count - 1; i >= 0; --i) {
        LegendWriter_Add(legend, reservations.Items[i].Legend);
    }

    ReturnReservationBuffer(&reservations);
}

ReservationBatch RentReservationBuffer(int count) {
    ReservationBatch batch;
    batch.Items = (MarkerReservation*)malloc(sizeof(MarkerReservation) * count);
    batch.Count = 0;
    return batch;
}

void ReturnReservationBuffer(ReservationBatch* batch) {
    free(batch->Items);
    batch->Items = NULL;
    batch->Count = 0;
}

static void InjectLineNumbers(OverlayBuilder* overlay) {
    for (int line = OverlayBuilder_LineCount(overlay) - 1; line >= 0; --line) {
        char prefix[8];
        Format(prefix, sizeof(prefix), "%4d| ", line + 1);
        OverlayBuilder_SetLinePrefix(overlay, line, prefix);
    }
}

static void WrapWithFence(OverlayBuilder* overlay) {
    int ticks = OverlayBuilder_MaxTickRun(overlay) + 1;
    if (ticks < 3) {
        ticks = 3;
    }
    char fence[64];
    BuildFence(fence, sizeof(fence), ticks);

    OverlayBuilder_InsertLine(overlay, 0, fence);
    OverlayBuilder_InsertLine(overlay, OverlayBuilder_LineCount(overlay), fence);
}

// Legend + fence widgets ----------------------------------------------------
typedef struct { StringBuilder Lines; } LegendWriter;

void LegendWriter_Init(LegendWriter* writer);
void LegendWriter_Add(LegendWriter* writer, const char* line);
void LegendWriter_Dispose(LegendWriter* writer);

typedef struct { /* opaque */ } LegendWidget;
typedef struct { /* opaque */ } FenceWidget;
typedef struct { /* opaque */ } MarkdownWriter;

void LegendWidget_Render(LegendWidget* widget, const LegendWriter* legend, MarkdownWriter* writer);
void FenceWidget_Render(FenceWidget* widget, const OverlayBuilder* overlay, MarkdownWriter* writer);

void ReleaseHandle_Dispose(ReleaseHandle* handle);
```
