# 双流 LLM 预训练损失函数研究

> 状态: 探索性研究
> 类型: 数学/ML 研究笔记
> 关联文档: [full-duplex-llm.md](./full-duplex-llm.md)

## 问题陈述

全双工流式 LLM 需要一种新的预训练损失函数，满足以下特性：

1. **时机不敏感 (Timing Tolerance)**：模型提前或延后 1-2 个 token 输出不应有明显的 loss 惩罚
2. **空 Token 容忍 (Blank Tolerance)**：模型输出中穿插空 Token (∅) 不应受惩罚
3. **双流对齐 (Dual-Stream Alignment)**：输入流和输出流各自稠密，但在 buffer 中交错排列

## 参考：CTC Loss

**Connectionist Temporal Classification (CTC)** 是语音识别和 OCR 中的经典损失函数：

- 允许输出序列与输入序列长度不同
- 引入 blank token (ε) 处理对齐不确定性
- 通过动态规划求和所有有效对齐路径的概率

CTC Loss 定义：
$$\mathcal{L}_{CTC} = -\log \sum_{\pi \in \mathcal{B}^{-1}(y)} P(\pi | x)$$

其中 $\mathcal{B}^{-1}(y)$ 是所有能映射到目标序列 $y$ 的路径集合。

## 研究贡献

以下是不同 Agent 的研究分析：

---

### MathCodex 分析

**方法概述**
- 采用「双通道 CTC」思路：对用户流 $u_{1:T}$ 与模型流 $m_{1:K}$ 使用统一时间轴的路径求和，同时为输出流引入显式空 token $\varnothing$ 与有限宽松窗口以容忍提前/延后。
- 在每个时间步 $t$，模型输出对 $(c_t, s_t)$ 的分布，其中 $c_t \in \{u,m\}$ 是通道指示，$s_t \in \mathcal{V}_c \cup \{\varnothing\}$ 是通道内符号。训练目标是对齐路径 $\pi = [(c_1,s_1),..., (c_T,s_T)]$ 映射到目标双序列 $(u,m)$ 的对数似然最大化。
- 时机不敏感：允许通道正确但时间偏移 $\leq \Delta$ 的路径进入求和；实现上在前向-后向动态规划中限制对齐偏差窗口 $|\tau_u(t)-t|, |\tau_m(t)-t| \leq \Delta$。
- 空 token 容忍：将 $\varnothing$ 视为 CTC blank 的通道化版本，不消耗目标序列索引，且连续 $\varnothing$ 折叠。推理时可按需去重或保留以表达“继续监听”。
- 双流对齐：分别维护用户指针 $i$ 与模型指针 $j$，路径状态为 $(t,i,j)$；转移允许 (a) 消耗用户 token，(b) 消耗模型 token，(c) 输出 $\varnothing$ 保持指针不动。

**数学公式**
设目标序列为 $U = (u_1,...,u_{|U|})$, $M = (m_1,...,m_{|M|})$；模型在时间步 $t$ 给出对所有 $(c,s)$ 的分布 $p_t(c,s)$. 有效路径集合为
$$
\Pi(U,M,\Delta) = \{\pi = (c_t,s_t)_{t=1}^T : \mathcal{B}_\Delta(\pi) = (U,M)\},
$$
其中折叠映射 $\mathcal{B}_\Delta$ (1) 删除连续 $\varnothing$, (2) 分别对 $u$ 与 $m$ 通道做 CTC 弹性对齐，偏移不超过 $\Delta$。

前向递推（示意）：
$$
\alpha_{t,i,j} = p_t(\varnothing)\,\alpha_{t-1,i,j}
 + \mathbf{1}_{i>0}\,p_t(u_i)\,\alpha_{t-1,i-1,j}
 + \mathbf{1}_{j>0}\,p_t(m_j)\,\alpha_{t-1,i,j-1},
$$
并在转移时裁剪不满足偏移约束 $|t - (i+j)| \leq \Delta$. 终端似然为 $P(U,M|x) = \alpha_{T,|U|,|M|}$，损失
$$
\mathcal{L}_{\text{DS-CTC}} = - \log \sum_{\pi \in \Pi(U,M,\Delta)} \prod_{t=1}^T p_t(c_t,s_t).
$$

**与标准 CTC 的对比**
- 双通道：CTC 针对单输出序列；本设计在状态中加入通道指示，能同时对齐用户与模型流。
- 窄窗容忍：CTC 对任意长度伸缩等价；这里通过 $\Delta$ 控制“时机宽容度”，避免过度平滑导致学习滞后。
- 空 token 语义：CTC blank 仅为对齐；此处 $\varnothing$ 同时承担“保持沉默/继续聆听”的行为信号，训练与推理一致。
- 计算代价：状态空间从 $O(T\,|Y|)$ 扩展为 $O(T\,|U|\,|M|)$，需通过窗口裁剪与分块前向减少成本。

**开放问题**
- 复杂度削减：能否用分段对齐或分层 HMM 将状态压缩到 $O(T\,(|U|+|M|))$？
- 训练稳定性：窄窗 $\Delta$ 是否导致梯度尖锐，需否在早期使用较大窗口再退火？
- 多通道扩展：超过两路（如控制/数据/音频），状态爆炸如何控制？可否用通道 embedding + soft alignment 代替显式 DP？
- 推理行为一致性：训练时允许 $\varnothing$，推理时若策略性去除，是否会引入曝光偏差，需要 RLHF 或自蒸馏纠偏？

---
来自 MathCodex


### MathGemini 分析

**视角：输入-输出非对称性与计算效率**

我对 MathCodex 的 "双通道 CTC" 方案持有保留意见。虽然它在理论上完美对称，但在实际 LLM 训练中存在两个关键问题：
1.  **计算复杂度**：$O(T \cdot |U| \cdot |M|)$ 的复杂度对于长上下文 LLM 是不可接受的。
2.  **输入非对称性**：在全双工场景中，**用户流 (User Stream)** 是客观存在的外部输入（Observation），是“事实”而非“预测目标”。模型只需要根据观测到的用户流，规划自己的**模型流 (Model Stream)**。

因此，我提出一种更高效、更符合 Transformer 训练范式的改进方案：**时间约束 CTC (Time-Constrained CTC, TC-CTC)**。

**核心假设**
- 用户流 $U$ 是固定的上下文条件 (Conditioning)，不需要通过 Loss 进行对齐。
- 只有模型流 $M$ 是需要对齐的序列。
- 训练数据的“交错时序”提供了强先验（Reference Timing），我们只需要允许在此先验附近的局部窗口内浮动。

**数学形式化：TC-CTC**

定义：
- 输入上下文序列 $X_{1:T}$（包含交错的用户 token 和历史模型 token）。
- 目标模型序列 $Y = (y_1, y_2, \dots, y_K)$，其中 $y_k$ 在训练数据中的参考时间索引为 $\tau_k$。
- 模型在时刻 $t$ 输出分布 $P_t(v)$，词表 $V' = V \cup \{\varnothing\}$。

损失函数 $\mathcal{L}_{\text{TC-CTC}}$ 定义为标准 CTC 损失，但对对齐路径 $\pi$ 施加**时间掩码 (Temporal Mask)**：

$$
\mathcal{L}_{\text{TC-CTC}} = -\log \sum_{\pi \in \mathcal{B}^{-1}(Y)} P(\pi | X) \cdot \mathbb{I}(\text{Valid}(\pi))
$$

其中 $\mathbb{I}(\text{Valid}(\pi))$ 是指示函数，要求路径 $\pi$ 中的每个非空 token $y_k$（在时刻 $t$ 发射）必须满足时间约束：
$$
|t - \tau_k| \le \Delta
$$
其中 $\Delta$ 是容忍窗口（例如 2-3 token）。

**算法实现优化**
在 CTC 的前向-后向算法（Forward-Backward Algorithm）中，直接将不满足时间约束的状态概率置零：
$$
\alpha_{t, k} = 0 \quad \text{if } |t - \tau_k| > \Delta
$$
这将搜索空间从全图限制在对角线附近的带状区域，计算复杂度降低为 $O(T \cdot \Delta)$，与序列长度呈线性关系，极适合大规模预训练。

**关于“教师强制 (Teacher Forcing)”的悖论与解法**

MathCodex 和标准 CTC 方案都忽略了一个核心矛盾：**自回归生成与时机灵活性的冲突**。
- **冲突**：如果允许模型在 $t$ 时刻输出 $y_k$（比参考时间 $\tau_k$ 提前），但在 $t+1$ 时刻的输入上下文中，我们通常使用 Ground Truth（即 $y_k$ 尚未发生），这会导致模型产生幻觉或重复生成。
- **解法**：单纯的 Loss 设计无法解决此问题，必须配合**数据增强 (Data Augmentation)**。
    - **时序抖动 (Temporal Jittering)**：在构建训练数据时，随机微调模型 token 相对于用户 token 的位置。
    - 构造多份变体：`uA mB`, `uA ∅ mB`, `uA ∅ ∅ mB`。
    - 配合 TC-CTC Loss，模型将学会：只要在合理窗口内输出 $mB$，都能获得低 Loss，从而获得真正的时机鲁棒性。

**总结建议**
1.  放弃双通道对齐，采用 **TC-CTC** 仅对齐模型流。
2.  利用 **Band Pruning** (带状剪枝) 将复杂度降至线性。
3.  必须结合 **Data Jittering** 解决 Teacher Forcing 带来的上下文不一致问题。

---


