

使用 Reinforcement-Learning 概念体系，近期也务实的使用Chat范式的常规LLM。

## 借用心理学术语
我们构造的是智能系统，关心的是系统的外在行为与内在行为，追求模拟人类等自然智能系统的有益心理机制，既然是模拟就会借用前人的心智模型研究成果。后续所有涉及心智模型的术语借用皆指等效模拟机制和行为层面，而忽视技术实现是生化反应还是计算机建模。

## 术语与概念概述：

**Environment**: 是Agent系统中的外部状态转移函数。
**Agent**: 一个能感知环境、为达成目标而行动、并承担行动后果的计算实体。Agent系统有内部和外部两个状态转移函数，交替发挥作用。
**LLM**: Causal Generative Pre-Trained Large Language Model。典型情况下是Autoregressive模型，但也可以是逐块输出的Diffusion模型，可以认为是用扩散方法一次生成一批Token的自回归模型。是Agent系统中的内部状态转移函数。
**Agent-OS**: 在Agent系统中，LLM与Environment之间进行交互的中间件。Agent-OS向LLM提供对Environment的Observation。Agent-OS尝试执行LLM发出的Tool-Call。
**Message**: 使用分块通讯模型而非流式通讯。LLM与Agent-OS之间的一次单向信息传递。LLM与Agent-OS之间采用Half-Duplex方式通讯。
**Observation**: 由Agent-OS发送给LLM的Message。Agent-OS向LLM展示的部分重要系统状态。
**Tool-Call**: 由LLM发出的，由Agent-OS负责尝试执行的，同步功能调用。
**Action**: 由LLM发送给Agent-OS的Message。Thinking + Tool-Call。
**History**: 是Agent系统状态的一部分，由Agent-OS负责记录、管理、维护。增量、仅追加、不可变。
**History-View**: 用于向LLM展示的，由Agent-OS渲染的，History的部分信息。

## 明确弃用的概念:

**Human-User**: 不是一对一问答服务，没有Chat范式中的唯一Human-User概念。LLM通过Agent-OS与Environment互动，Agent可能和形形色色各种关系的人群交互。Chat范式中的User角色被Agent-OS取代。

**To-User-Response**: Agent-OS只解析Action Message中的Tool-Call部分，其他LLM输出不触发Agent-OS的状态转移。LLM有向Environment中的其他人或Agent说话的需求时，需要采用Tool-Call的方式。这与编程语言中将print从关键字变为库函数的演化过程相似。

## 重要澄清:

**关于Thinking/CoT**: Agent-OS不解析Thinking部分，并不意味着它是无用的或仅供调试。Thinking是Chain-of-Thought，是Agent系统的**重要内部状态**——它直接影响LLM后续token的生成概率分布，是推理能力的核心机制。Thinking对Agent-OS无语义效力，但对LLM自身至关重要。

**关于分块消息与流式传输**: 这是不同层面的概念，不要混淆。
- **当前LLM的分块特性**: 典型LLM采用批量Pre-Fill再批量Decode的架构，模型的输入输出在语义层面是分块的。这些块状信息是否采用流式传输（如SSE）是传输层的实现细节。
- **真正的流式LLM**: 是"Think while Listening"的全双工架构。最简形式是每输入一个token都输出一个token，外界输入流和自身输出流共同构成模型上下文。技术上可通过位置编码区间划分或Channel编码实现。这种架构支持语音交互中的抢答与插话。
- **本文档采用分块消息模型**，对应当前主流LLM的实际能力。

## LLM调用的3层模型

**ICompletionClient**: 每种厂商规范有独立的实现，包括但不限于OpenAI v1、OpenAI Responses、Anthropic Messages v1、Gemini Content Parts等。目前仅有“atelia/prototypes/Completion/Anthropic”一份实现，后续继续补充其他厂商和规范。由Abstract-LLM-Request-Layer翻译得到。
**IHistoryMessage**: 抽象的跨LLM厂商和规范的LLM调用和结果。目前代码在“atelia/prototypes/Completion.Abstractions”。由History-Layer渲染得到。ToolCallResult仅包含LOD渲染后的一份信息。在最后一条Observation中包含渲染出的各App-For-LLM的信息，也就是DocUI的渲染结果。
**HistoryEntry**: 丰富和相对完整的LLM交互记录。目前代码在“atelia/prototypes/Agent.Core/History/HistoryEntry.cs”。随着每次LLM的输入和输出创建。与IHistoryMessage的一个重要区别是HistoryEntry中存储的ToolCallResult包含Basic+Detail两个LOD级别的信息。

## Render
DocUI语境下的Render是指由活跃HistoryEntry和AppState生成用于LLM调用的一组IHistoryMessage的过程。

## TODO
- 关于ICompletionClient，需要查找和确定更准确的各厂商API规范名称。
- 关于由活跃HistoryEntry和AppState生成用于LLM调用的一组IHistoryMessage的过程，用Render这一名称虽然通俗易懂但是过于宽泛，争取找到更好的术语表述此过程。
- 关于LLM调用的3层模型，需要更好的命名。需要一份Mermaid插图。
- 关于HistoryEntry层和IHistoryMessage层，可能各需要一份Mermaid插图展现信息的结构。
- 关于IHistoryMessage，考虑是否改名回“IContextMessage”，考虑是否从接口改为类型。
