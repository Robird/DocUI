

使用 Reinforcement-Learning 概念体系，近期也务实的使用Chat范式的常规LLM。

## 术语与概念概述：

**Environment**: 是Agent系统中的外部状态转移函数。
**Agent**: 一个能感知环境、为达成目标而行动、并承担行动后果的计算实体。Agent系统有内部和外部两个状态转移函数，交替发挥作用。
**LLM**: Causal Generative Pre-Trained Large Language Model。典型情况下是Autoregressive模型，但也可以是逐块输出的Diffusion模型，可以认为是用扩散方法一次生成一批Token的自回归模型。是Agent系统中的内部状态转移函数。
**Agent-OS**: 在Agent系统中，LLM与Environment之间进行交互的中间件。Agent-OS向LLM提供对Environment的Ovservation。Agent-OS尝试执行LLM发出的Tool-Call。
**Message**: 使用分块通讯模型而非流式通讯。LLM与Agent-OS之间的一次单向信息传递。LLM与Agent-OS之间采用Half-Duplex方式通讯。
**Observation**: 由Agent-OS发送给LLM的Message。Agent-OS向LLM展示的部分重要系统状态。
**Tool-Call**: 由LLM发出的，由Agent-OS负责尝试执行的，同步功能调用。
**Action**: 由LLM发送给Agent-OS的Message。Thinking + Tool-Call。
**History**: 是Agent系统状态的一部分，由Agent-OS负责记录、管理、维护。增量、仅追加、不可变。
**History-View**: 用于向LLM展示的，由Agent-OS渲染的，History的部分信息。

## 明确弃用用的概念:
**Human-User**: 不是一对一问答服务，没有Chat范式中的唯一Human-User概念。LLM通过Agent-OS与Environment互动，Agent可能和形形色色各种关系的人群交互。Chat范式中的User角色被Agent-OS取代。
**To-User-Response**: Agent-OS只解析Action Message中的Tool-Call部分，忽视所有其他的LLM输出，所以LLM对Agent-OS说的话仅有自言自语的意义。LLM有向Environment中的其他人或Agent说话的需求时，需要采用Tool-Call的方式。这与编程语言中将print从关键字变为库函数的演化过程相似。