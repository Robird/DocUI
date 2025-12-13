## Doc as User Interface
LLM是Agent-OS的用户，LLM从Agent-OS获取信息和进行操作的界面就是DocUI。因将展现给LLM的信息渲染为Markdown Document而命名。

## DocUI与GUI/TUI/API的区别与联系
- 在渲染形式上，DocUI与TUI和Web服务器最为近似，只是DocUI渲染出的信息以Markdown文档为基础，并按需进行扩展。选择Markdown的原因在于LLM对此熟悉，语法噪声也较少。xml和html语法噪音较多，json转义序列问题更突出。asciidoc也入围，但没有markdown语料多。

## DocUI注入LLM上下文的形式
DocUI主要通过2种形式注入LLM上下文中：Window和Notification。Window呈现实况状态。Notification呈现事件历史。

### Window
在Render过程中，将各种需要向LLM展现的实况信息渲染为一份Markdown文档，作为最新的一条ObservationMessage的正文的一部分。在呈现给LLM的信息中只有最新的一份，而不在HistoryMessage层中展示所有历史快照，但不排除在Entry层为了调试、存档或时间旅行目的而保存不可变快照。

Window中的信息有{Full, Summary, Gist}三个LOD级别。
  - Gist级别保留最基本的“What”和“关键线索”信息，意在最小化Token占用并保留提供一个提高LOD级别来恢复认知的入口。
  - Summary级别是最常用的主要级别，是信息实用性和Token占用的甜点级别。展示当前节点及所有子节点的概述、重要子节点链接、重要相关节点链接、所有子节点列表链接。
  - Full则展现所有原始信息。

### Notification
Recent History为LLM提供了近期的认知与思路连续性，过程性信息的各个历史动态则保存入HistoryEntry中，有{Basic, Detail}两档LOD，称为Notification。根据上下文窗口预算，较新的HistoryEntry渲染时取Detail级别，较老的Entry取Basic级别。Agent系统收到外部其他人或Agent发来的信息，就建模为一条Notification，带有对方的关系标识和通讯渠道标识，取代Chat范式下唯一的直接交互用户。时间戳也是一条Notification，为节约Token数可能以较低的频率产生，进而使得并非每条Observation都有时间戳。一种候选设计是每条都注入时间戳Notification，但仅Detail级别始终不为空，而Basic级别每分钟有一次不为空。

## 待消化的建议
- 定义一个 Focus（焦点），Agent 的操作（如 read, edit）会自动移动焦点。
- 引入 **"Attention Focus" (注意力焦点)** 概念。Agent 当前操作的对象自动为 Full，相关联对象为 Summary，其余为 Gist。这比静态的全局设置更高效。
- LOD 不应只是“字数减少”，而应是“信息维度切换”。Gist 显示“类型和ID”，Summary 显示“属性和状态”，Full 显示“内容和关系”。
**增加 "Diff" 视角**：
    *   对于 Window，Agent 往往更关注“什么变了”。
    *   在 Summary 级别中，显式标记 **Dirty State**（自上次交互以来发生变化的部分）。这能极大地引导 LLM 的注意力，节省其扫描整个 Summary 来寻找变化的时间。

## TODO
- 关于Window中的Summary级别。考虑平铺渲染各信息节点，树形结构何时inplace展开，何时树形结构内保留Gist或Link而在文档其他地方平铺展开更详细的目标节点内容，一种选择是子节点就地展开，外链则在外链处展开。

