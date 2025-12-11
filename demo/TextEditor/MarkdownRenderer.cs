using System.Text;
using PieceTree.TextBuffer;

namespace DocUI.TextEditor;

/// <summary>
/// 将 TextModel 渲染为 Markdown 格式
/// </summary>
public sealed class MarkdownRenderer
{
    private readonly TextModel _model;
    private readonly string _fileName;
    private readonly string _sessionId;
    
    private int _cursorLine;
    private int _cursorColumn;

    public MarkdownRenderer(TextModel model, string fileName, string sessionId)
    {
        _model = model;
        _fileName = fileName;
        _sessionId = sessionId;
    }

    public void SetCursor(int line, int column)
    {
        _cursorLine = line;
        _cursorColumn = column;
    }

    public string Render()
    {
        var sb = new StringBuilder();
        
        // 标题
        sb.AppendLine($"# TextEditor: {_fileName} (Session: {_sessionId})");
        sb.AppendLine();
        
        // 代码围栏开始
        sb.AppendLine("```csharp");
        
        var lineCount = _model.GetLineCount();
        var maxLineNumWidth = lineCount.ToString().Length;

        for (int lineNum = 1; lineNum <= lineCount; lineNum++)
        {
            var lineContent = _model.GetLineContent(lineNum);
            var lineNumStr = lineNum.ToString().PadLeft(maxLineNumWidth);
            
            // 渲染行号 + 内容
            sb.Append($"{lineNumStr} | ");
            
            // 如果当前行包含光标，插入光标标记
            if (lineNum == _cursorLine)
            {
                var beforeCursor = lineContent.Substring(0, Math.Min(_cursorColumn - 1, lineContent.Length));
                var afterCursor = lineContent.Substring(Math.Min(_cursorColumn - 1, lineContent.Length));
                sb.Append(beforeCursor);
                sb.Append('█'); // 光标字符
                sb.AppendLine(afterCursor);
            }
            else
            {
                sb.AppendLine(lineContent);
            }
        }
        
        // 代码围栏结束
        sb.AppendLine("```");
        sb.AppendLine();
        
        // 图例
        sb.AppendLine("**Legend**: `█` Cursor");
        sb.AppendLine();
        
        // 统计信息
        sb.AppendLine($"**Stats**: {lineCount} lines | Ln {_cursorLine}, Col {_cursorColumn}");
        
        return sb.ToString();
    }
}
