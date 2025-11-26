本repo的目标是为工作在Agent系统中的LLM创建一种基于文本文档的组件化轻量TUI。
以WinForms与Markdown之间的映射举例：

| 功能 | WinForms | Markdown |
| --- | --- | --- |
| 互不嵌套容器 | Form| ATX 1 Heading |
| 可嵌套容器| Panel | ATX 1+ Heading |
| 有边界文本| TextBox | Fenced code blocks |
| 无边界文本| Label | Paragraphs |
| 光标/选区/高亮/虚拟行号 | Overlays | 插入正文中的标记+代码围栏外的图例 |
| 输入 | 各种输入空间 | Tool Calling |
| 向导 | UI动态控制 | 动态渲染且与Tool可见性绑定 |
| 信息聚焦 | 折叠与滚动 | 子LLM实现的语义摘要(LLM需要摘要来留下印象) |

关键机制：
TUI与Tool建立绑定机制，Model-View-Control一体封装为Widget。多个Widget又聚合成App。
以App为单位，以3种形式向LLM Context注入信息：
  1. 随着输入给LLM的消息（role=User/Tool）而永久注入History，有Basic/Detail两个LOD级别。例如：时钟App会给每条User/Tool消息注入一个时间戳，最近N条用精确实际格式，更久的消息则只精确到分钟。
  2. 在每次构建调用LLM的上下文时，唯一的注入到最后一条输入给LLM的消息中，展现信息的最新状态。例如：TextFileViewApp会始终呈现文件的最新状态。ShellTerminalApp会始终呈现终端的最新Buffer。
  3. 通过可见工具集合，在调用LLM前注入当前的可用工具。

