using PieceTree.TextBuffer;

namespace DocUI.TextEditor;

/// <summary>
/// 单个编辑器会话 - 持有 TextModel 和光标/选区状态
/// </summary>
public sealed class EditorSession
{
    public string SessionId { get; }
    
    private TextModel? _model;
    private string? _filePath;
    private int _cursorLine = 1;
    private int _cursorColumn = 1;

    public EditorSession(string sessionId)
    {
        SessionId = sessionId;
    }

    /// <summary>
    /// 打开文件
    /// </summary>
    /// <param name="path">文件路径</param>
    /// <returns>成功时返回渲染的 Markdown，失败时抛出异常</returns>
    public async Task<string> OpenAsync(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"File not found: {path}");
        }

        var content = await File.ReadAllTextAsync(path);
        _model = new TextModel(content);
        _filePath = path;
        _cursorLine = 1;
        _cursorColumn = 1;

        return RenderMarkdown();
    }

    /// <summary>
    /// 跳转到指定行
    /// </summary>
    /// <param name="line">行号（1-based）</param>
    /// <returns>渲染的 Markdown</returns>
    public string GotoLine(int line)
    {
        if (_model == null)
        {
            throw new InvalidOperationException("No file opened");
        }

        _cursorLine = Math.Clamp(line, 1, _model.GetLineCount());
        _cursorColumn = 1;

        return RenderMarkdown();
    }

    /// <summary>
    /// 选区（尚未实现）
    /// </summary>
    public string Select(int startLine, int startCol, int endLine, int endCol)
    {
        // TODO: 实现选区功能
        throw new NotImplementedException("Select is not implemented yet");
    }

    /// <summary>
    /// 重新渲染当前视图
    /// </summary>
    public string Render()
    {
        if (_model == null)
        {
            throw new InvalidOperationException("No file opened");
        }

        return RenderMarkdown();
    }

    private string RenderMarkdown()
    {
        if (_model == null) return "(no file opened)";

        var renderer = new MarkdownRenderer(_model, _filePath ?? "untitled", SessionId);
        renderer.SetCursor(_cursorLine, _cursorColumn);
        return renderer.Render();
    }
}
