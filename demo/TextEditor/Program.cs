using System.CommandLine;
using DocUI.TextEditor;
using PipeMux.Sdk;

// 创建 PipeMux 应用
var app = new PipeMuxApp("texteditor");

// 有状态会话（同一进程内的多次调用共享状态）
EditorSession? session = null;

EditorSession GetOrCreateSession()
{
    session ??= new EditorSession("te-" + Guid.NewGuid().ToString("N")[..8]);
    return session;
}

// === 命令定义 ===

// open <path> - 打开文件
var openPathArg = new Argument<string>("path") { Description = "The file path to open" };
var openCommand = new Command("open", "Open a file for editing") { openPathArg };
openCommand.SetAction(async parseResult =>
{
    var path = parseResult.GetValue(openPathArg)!;
    var s = GetOrCreateSession();
    try
    {
        var output = await s.OpenAsync(path);
        parseResult.Configuration.Output.WriteLine(output);
    }
    catch (Exception ex)
    {
        parseResult.Configuration.Error.WriteLine($"Error: {ex.Message}");
    }
});

// goto-line <line> - 跳转到指定行
var gotoLineArg = new Argument<int>("line") { Description = "The line number to go to (1-based)" };
var gotoLineCommand = new Command("goto-line", "Go to a specific line") { gotoLineArg };
gotoLineCommand.SetAction(parseResult =>
{
    var line = parseResult.GetValue(gotoLineArg);
    var s = GetOrCreateSession();
    try
    {
        var output = s.GotoLine(line);
        parseResult.Configuration.Output.WriteLine(output);
    }
    catch (Exception ex)
    {
        parseResult.Configuration.Error.WriteLine($"Error: {ex.Message}");
    }
});

// select - 选区（尚未实现）
var selectStartLineArg = new Argument<int>("startLine") { Description = "Start line" };
var selectStartColArg = new Argument<int>("startCol") { Description = "Start column" };
var selectEndLineArg = new Argument<int>("endLine") { Description = "End line" };
var selectEndColArg = new Argument<int>("endCol") { Description = "End column" };
var selectCommand = new Command("select", "Select a region (not implemented)")
{
    selectStartLineArg,
    selectStartColArg,
    selectEndLineArg,
    selectEndColArg
};
selectCommand.SetAction(parseResult =>
{
    var startLine = parseResult.GetValue(selectStartLineArg);
    var startCol = parseResult.GetValue(selectStartColArg);
    var endLine = parseResult.GetValue(selectEndLineArg);
    var endCol = parseResult.GetValue(selectEndColArg);
    var s = GetOrCreateSession();
    try
    {
        var output = s.Select(startLine, startCol, endLine, endCol);
        parseResult.Configuration.Output.WriteLine(output);
    }
    catch (Exception ex)
    {
        parseResult.Configuration.Error.WriteLine($"Error: {ex.Message}");
    }
});

// render - 重新渲染当前视图
var renderCommand = new Command("render", "Re-render the current view");
renderCommand.SetAction(parseResult =>
{
    var s = GetOrCreateSession();
    try
    {
        var output = s.Render();
        parseResult.Configuration.Output.WriteLine(output);
    }
    catch (Exception ex)
    {
        parseResult.Configuration.Error.WriteLine($"Error: {ex.Message}");
    }
});

// 根命令
var rootCommand = new RootCommand("TextEditor - A simple text editor service")
{
    openCommand,
    gotoLineCommand,
    selectCommand,
    renderCommand
};

// 启动 PipeMux 应用
await app.RunAsync(rootCommand);
