# Flywire AI

> 🪰 **把一只真实果蝇的全脑接线图装进计算机，用电脉冲唤醒它，再让它替你打一局贪吃蛇。**
>
> *One connectome, one simulation, one cyber-life.*

[![.NET](https://img.shields.io/badge/.NET-10.0-blueviolet)](https://dotnet.microsoft.com/)
[![Language](https://img.shields.io/badge/language-VB.NET-blue)](https://learn.microsoft.com/dotnet/visual-basic/)
[![Dataset](https://img.shields.io/badge/dataset-FAFB%20v783-orange)](https://flywire.ai/)
[![Framework](https://img.shields.io/badge/framework-sciBASIC.NET-success)](https://github.com/xieguigang/GCModeller)

---

## 1. 这是什么

**Flywire AI** 是一个连接组（connectome）驱动的全脑脉冲神经网络仿真平台。它把
[FlyWire](https://flywire.ai/) 项目发布的成年雌果蝇全脑连接组数据集 **FAFB-v783**
（**139,255** 个神经元、约 **5,067 万**个突触）直接装配为一个可运行的
**LIF 脉冲神经网络（Spiking Neural Network）**，并在其上提供三类能力：

1. **三维脑模型可视化** —— 13.9 万个神经元的点云、脑区着色、逐时间步的脉冲活动回放；
2. **全脑电刺激仿真** —— 向任意一批神经元注入外部电流，观察脉冲活动在真实神经
   回路中的传播（支持 CUDA GPU 加速与 CPU SIMD 后端对拍）；
3. **赛博生命闭环** —— 把贪吃蛇游戏接入果蝇大脑：游戏画面编码为感觉电流灌入
   5,536 个 afferent 感觉神经元，从 441 个 efferent 运动神经元的放电中解码出
   上/下/左/右，由真实生物连接组驱动整局游戏。

核心思想一句话概括：**网络权重不训练，直接从生物实测数据里抄**。
所有突触拓扑、连接强度、E/I 极性均来自电镜重建的真实大脑，
SNN 只负责让这张"接线图"按照脉冲神经计算的规则跑起来。

## 2. 解决方案结构

```text
flywire.slnx
├── src/FlywireAI/        核心库：数据集加载 + 连接组建模 + SNN 仿真内核
│   ├── FAFBv783/         FAFB v783 全部数据表的类型化模型与加载器（CSV 流式 / MsgPack 包）
│   └── Connectome/       SynapseTriplets / ConnectomeMatrix / BrainNetwork /
│                         Stimulation / BrainSimulation / GpuRuntime / SnnConfig
├── src/Neuropils/        WinForms 工作台：三维可视化、电刺激实验台、结果图表、贪吃蛇观战
├── src/FlywireSnake/     游戏接口层：感觉编码器 / 运动解码器 / 模仿学习 + 策略梯度 / 会话循环
├── src/test/             端到端演示测试（数据加载回归、CPU vs GPU 对拍、msgpack 往返）
└── docs/                 数据集字段文档 + 三篇科普博客（SNN 原理 / 连接组 / 缸中脑）
```

底层依赖全部来自自研的 **sciBASIC.NET** 运行时（GCModeller 项目的科学计算基座）：

| 依赖 | 用途 |
| --- | --- |
| `Microsoft.VisualBasic.DeepLearning.SpikingNeuralNetwork` | 稀疏递归 LIF 网络、CSR 稀疏矩阵 SpMM |
| `Microsoft.VisualBasic.MachineLearning.TensorFlow` | 可插拔张量计算后端契约（SIMD CPU 内核） |
| `ILCudaTensor` | CUDA GPU 后端（NVRTC 融合算子 + 设备常驻缓冲） |
| `msgpack` | 全量数据集的 MsgPack 二进制转储与快速加载 |
| `DataFrame` | CSV 反射存储提供者：`LoadCsv(Of T)` 全量加载与 `AsLinq(Of T)` 流式加载 |

## 3. 数据集：FAFB v783

数据全部来自 [FlyWire Codex](https://codex.flywire.ai/api/download?dataset=fafb)，
字段定义详见 [`docs/fafb-v783-data.md`](docs/fafb-v783-data.md)。核心表格：

| 文件 | 规模 | 用途 |
| --- | --- | --- |
| `connections_princeton.csv` | 534 万行（已过滤 < 5 突触的连接） | 突触三元组 `(pre, post, syn_count)`，网络的唯一拓扑来源 |
| `classification.csv` | 139,255 行 | `flow` 列：afferent（感觉输入 5,536）/ efferent（运动输出 441）/ intrinsic |
| `consolidated_cell_types.csv` | 138,327 行 | 8,772 种细胞类型，支持按类型点名刺激 |
| `coordinates.csv` | 238,909 行 | 神经元空间坐标，三维点云来源 |
| `neuropil_synapse_table.csv` | 134,181 行 × 321 列 | 80 个脑区的输入/输出统计，脑区着色依据 |
| `sk_lod1_783_healed.zip` | SWC 骨架归档 | 神经元形态（骨架），由 `SwcParser` 解析 |

`root_id` 为 64 位整数，加载器专门做了 `Int64` 精度回归；五百万行级大表使用
惰性流式接口逐行读取（内存 O(1)），小表走反射全量加载。全部数据源可一次性
转储为 MsgPack 包（`FafbMsgPackStorage`），之后所有消费方（可视化、仿真、贪吃蛇）
直接反序列化整包，把"读五百万行 CSV"变成"看快照"。

## 4. 算法原理

### 4.1 LIF 神经元：会漏水的桶

每个神经元按 **Leaky Integrate-and-Fire** 模型离散演化（β = 0.9，θ = 1.0，发放后清零）：

$$
u[t] = \beta \, u[t-1] + \sum_{j} W_{ij} \, s_j[t-1] + I_i^{\text{ext}}[t],
\qquad
s_i[t] = \mathbb{1}\!\left[u[t] \geq \theta\right],\;\; u[t] \leftarrow 0 \text{ on spike}
$$

整个全脑网络就是**单个稀疏递归 LIF 层**——139,255 个神经元共享一张 CSR 稀疏
权重矩阵，每个时间步做一次稀疏 SpMM。网络装配入口在 `BrainNetworkBuilder`：

```vb
' Connectome/BrainNetworkBuilder.vb（节选）
Dim net As New SpikingNetwork(stimulation.Count, config.TimeSteps, config.Encoding)

net.Rng = New Random(config.Seed)

' CSR 权重矩阵 + LIF 参数 + 输入注入映射，装配成稀疏递归层
Call net.AddSparseLayer(matrix.Synapses, config.Beta, config.Threshold,
                        config.ResetMode, stimulation.InputMap)

Return New BrainNetwork(net, matrix, matrix.Synapses, matrix.Triplets, ...)
```

### 4.2 从连接表到权重矩阵

权重生成规则（`SynapseTriplets`）：**带极性的突触计数 + 突触后结构归一化**。

```vb
' 权重约定：weight = polarity(nt_type) × syn_count
'   GABA（抑制性）        -> -1
'   GLUT / ACH 等（兴奋性）-> +1（含空值，按兴奋性处理）
```

随后三步：

1. **CSR 合并**：`SparseMatrix.FromTriplets(Pre, Post, Weight)` 把同一
   `(pre, post)` 细胞对在不同脑区上的重复突触按权重累加合并为单条边；
2. **结构归一化**：按**突触后神经元**的输入强度绝对值之和缩放，使每个神经元
   的总输入幅度为 1（刻意不用带符号求和——E/I 混杂列表的代数和可能接近零，
   带符号归一化不可靠）：

```vb
' SynapseTriplets.NormalizeStructural（节选）
For k As Integer = 0 To values.Length - 1
    absSum(columns(k)) += std.Abs(values(k))    ' 每个突触后神经元的 Σ|w|
Next

For k As Integer = 0 To values.Length - 1
    Dim total As Double = absSum(columns(k))
    If total > 0 Then
        values(k) /= total                       ' Σ|w| = 1 per post neuron
    End If
Next
```

3. **全局增益**：结构归一化后的单位权重另存一份 `BaseValues`，
   `SetGain(g)` 在**不重建 CSR** 的前提下就地重写 `values = base × g`——
   增益未变时不触碰权重数组，避免 GPU 设备端缓存失效
   （否则一次 60 MB 的显存上传会被塞进每个时间步的计时里）。

### 4.3 全局增益标定

归一化后的权重需要乘一个全局增益 $G$ 才能落在合理的放电区间。项目不做猜测，
而是用**短探针试跑**自动标定（`BrainSimulation.CalibrateGain`）：
对每个候选增益 $G \in \{1, 2, 4, 8\}$ 各跑 10 步探针仿真，挑选使活跃神经元
比例最接近目标值（默认 5%）的那个：

```text
对每个候选增益 G：
    用 G 试跑 CalibrationProbeSteps = 10 步
    计算 active fraction f(G) = 发放过至少一次的神经元数 / N
选出使 |f(G) − TargetActiveFraction| 最小的 G
```

$G$ 太小全脑沉睡、太大全体癫痫，标定就是把仿真调到"稀疏而有序的活跃"区间
（验收下限 1%、上限 30%）。也可以在 `SnnConfig.GlobalGain` 里显式配置跳过标定。

### 4.4 电刺激仿真

刺激方案（`Stimulation`）支持两种模式：

- **RandomNeurons**：固定种子随机挑选 5,000 个神经元直接注入电流；
- **CellType**：按 `group` / `class` / `primary_type` 注释筛选目标
  （例如"只刺激全部视觉神经元"），等效于在果蝇的梦里放幻灯片。

输入张量形状为 `[1, Count]`，每步将同一份恒流注入映射对应的神经元
（`SpikeEncoding.DirectCurrent`，确定可复现；亦可切换 RateCoding 伯努利采样）。
`BrainSimulation.Run` 逐时间步驱动 `SparseLIFLayer.ForwardStep`，收集逐步统计：

```text
BrainSimulationResult
  ├── Counts(i)          每个神经元的脉冲计数        -> Top 放电神经元榜单
  ├── PerStepSpikes[t]   每步脉冲总数 / 活跃神经元数 -> 逐步活跃曲线
  ├── MeanFiringRate     全脑平均发放率 = TotalSpikes / Units / T
  ├── ActiveFraction     活跃比例
  └── Backend / StepPath / FallbackSteps 实际计算路径（CUDA/SIMD、融合/逐算子）
```

仿真器显式逐步调用 `ForwardStep`（与库内部 `ForwardSparse` 语义一致），
与一次性 `ForwardSpikes` 路径的一致性由测试断言校验。

### 4.5 GPU 加速：分层注册的 CUDA 后端

张量计算采用 **CUDA → TensorFlow → SNN** 的分层契约：SNN 库只面向后端接口编程，
完全不知道 GPU 的存在；"注册 GPU"只能由最上层的应用工程完成（`GpuRuntime`）：

```vb
' 装配网络之前注册；失败（无 N 卡 / NVRTC 不可用 / 驱动不匹配）自动回退 CPU SIMD
If config.UseGpu AndAlso GpuRuntime.TryRegister(config.GpuDeviceOrdinal, config.GpuNvrtcPath) Then
    ' ForwardStep 自动走 NVRTC 融合单步算子 + 设备常驻状态缓冲
End If

' ... 仿真 ...

Call GpuRuntime.ReleaseDeviceBuffers()   ' 结束后归还显存
```

工程要点：

- **融合单步算子**（`LifStep`）：泄漏、SpMM、阈值判定、重置融合为一次 kernel
  调用，避免逐算子路径每步 5 个中间张量的显存往返；
- **设备常驻缓冲**：膜电位/脉冲状态驻留显存，`KeepHistory = False` 时整段仿真
  零逐步回读（最快档）；Double64 档位下 GPU 与 CPU 结果**逐位一致**，可做对拍验收；
- 全脑规模 nnz ≈ 373 万，超过 GPU SpMM 的最小非零数阈值（默认 65,536）。

### 4.6 让果蝇玩贪吃蛇：双向脑机接口闭环

`FlywireSnake` 把游戏与大脑接成一个逐 tick 的闭环
（`SnakeSession`，一个 tick 对应游戏的一帧）：

```text
1. 读游戏状态 -> 编码成 16 个感觉通道强度
2. 感觉电流注入 afferent 神经元 -> 果蝇大脑走一步（ForwardStep，状态跨 tick 累积）
3. 读出 efferent 运动神经元放电率 -> 解码器给出动作
4. 设置蛇的朝向 -> 移动 -> 碰撞判定 -> 游戏世界推进
```

**感觉侧**（`SnakeSensors`，16 个通道）：

```vb
Public Const FoodSectors As Integer = 8         ' 食物方位：以蛇的朝向为参考系的 8 扇区
Public Const DangerChannels As Integer = 4      ' 碰撞危险：上/下/左/右 绝对方向 0/1
Public Const SpecialFoodSectors As Integer = 4  ' 特殊食物（5 分 / 30 分）方位 4 扇区
Public Const ChannelCount As Integer = 16

' 食物通道强度随距离单调衰减：intensity(d) = 1 - d / R（R = 可视半径）
' 强度 × SensorCurrent(2.4) = 注入对应 afferent 神经元的电流
```

食物方位用**相对朝向**的 8 扇区而非绝对方向——蛇不能掉头，"我的左前方"才是
可学习的刺激量；可视半径取地图对角线（全图可见），否则教师标签与大脑感知
口径不一致，读出层学出来只会原地兜圈。

**运动侧**（`SnakeDecoder`，线性脑机接口）：441 个 efferent 运动神经元取
4-tick 滑动窗放电率作为特征向量，线性映射到 4 个动作的 softmax logits，
禁止反向动作置 $-\infty$，另有决策平滑器抑制逐 tick 左右横跳。

**读出层训练**——中间 139,255 神经元的连接组保持原样不训练，只训练这个线性
解码器，三级递进（`SnakePolicyGradient`）：

| 阶段 | 方法 | 目标 |
| --- | --- | --- |
| 1 | 模仿学习：贪心教师示范，记录（放电特征 → 教师动作），softmax 交叉熵 | 逐 tick 动作一致率 74%~78% |
| 2 | DAgger：把解码器自己走到的状态补进训练集 | 缓解分布偏移 |
| 3 | REINFORCE + 滑动基线策略梯度 | 直接优化游戏得分 |

模仿学习的一致率上去了，实战却只有 8 分（示范教师 95 分）——**误差累积**：
一旦走偏就进入训练集从未覆盖的状态，越偏越远。所以第三级换个目标，不再
"像教师"而是"把分数拿回来"，奖励设计为：

- **吃到食物**：按游戏真实得分增量（常规 1 / 活动 5 / 超级 30）；
- **靠近猎物**：按"折算代价（距离 − 价值折分）"的下降量给密集奖励
  （`ShapingScale`，否则整局只有几次稀疏奖励，梯度噪声会压过信号）；
- **每走一步**：小额罚分 0.01；**撞死**：一次性罚分 1.0。

安全网：每练完一块就闭环评分一次，只保留历史最高分的那版权重——最坏情况下
与"不做强化学习"持平。

## 5. 快速开始

### 5.1 环境要求

- .NET 10 SDK（VB.NET，`OptionStrict On`，警告视为错误）；
- x64 平台（`flywire.slnx`，AnyCPU / x64 双配置）；
- 解决方案引用同级的 sciBASIC.NET / Snake2 等工程，请按 slnx 内的相对路径摆放
  仓库（`../GCModeller`、`../pixelArtist` 等）；
- 可选：NVIDIA GPU + CUDA NVRTC（`nvrtc64_*.dll` 自动搜索 `CUDA_PATH`）。

### 5.2 数据准备

从 [FlyWire Codex](https://codex.flywire.ai/api/download?dataset=fafb) 下载
FAFB v783 各 CSV 到同一目录（字段清单见 [`docs/fafb-v783-data.md`](docs/fafb-v783-data.md)），
在 `SnnConfig.DataDir` 中指定路径即可；亦可预先转储 MsgPack 包加速后续加载。

### 5.3 端到端测试

`src/test` 是覆盖全流程的演示程序（支持按章节运行，如 `test 11` 只跑 msgpack 章节）：

```text
 1. full loading via LoadCsv                  小表反射全量加载 + 行数回归
 2. streaming loading via StreamXxx           大表流式加载
 ...
 9. Drosophila brain SNN simulation           全脑 SNN 仿真端到端
10. full brain SNN: CPU vs GPU acceleration   CPU/GPU 对拍（Double64 逐位一致）
11. msgpack round trip                        数据包往返一致性
```

### 5.4 最小仿真示例（库调用视角）

```vb
Imports FlywireAI.Connectome
Imports FlywireAI.Connectome.Network
Imports FlywireAI.FAFBv783

Dim config As New SnnConfig With {
    .DataDir = "F:\flywire\FAFB-v783",
    .TimeSteps = 30,
    .Beta = 0.9, .Threshold = 1.0,
    .Mode = StimulationMode.CellType,
    .UseGpu = True
}

' 1. 加载连接表 -> 突触三元组（带极性权重）
Dim index As ConnectomeIndex = config.LoadIndex()
Dim triplets As SynapseTriplets = SynapseTriplets.Load(config, index)

' 2. CSR 矩阵（合并重复边 + Σ|w|=1 结构归一化）-> 装配稀疏递归 LIF 网络
Dim tripletsStim As Stimulation = Stimulation.Create(config, index)
Dim brain As BrainNetwork = BrainNetworkBuilder.Build(config, triplets, tripletsStim)

' 3. 探针标定全局增益 -> 正式仿真
Dim gain As Double = BrainSimulation.CalibrateGain(brain, config, tripletsStim)
brain.SetGain(gain)

Dim result As BrainSimulationResult = BrainSimulation.Run(brain, config, tripletsStim)
Console.WriteLine(result.Describe())
' => CellType: spikes=..., active=.../139255 (5.1%), mean rate=..., T=30, ... ms
```

### 5.5 运行图形界面

启动 `Neuropils`（WinForms）：

- **三维脑模型**：点云 + 脑区着色 + 脉冲活动实时高亮（`PageFlywireCanvas`）；
- **电刺激实验台**：配置刺激方案、运行仿真、查看响应曲线与 Top 放电神经元
  （`StimulationExperiment` / `PageResponseChart`）；
- **贪吃蛇观战**：加载 MsgPack 脑模型包后进入 `PageSnakeBrainGamePlay`，
  观看果蝇大脑逐 tick 驱动蛇的每一步移动，同时同步显示本 tick 的
  感觉通道强度、发放神经元与各运动组脉冲数。

## 6. 引用与致谢

- 连接组数据来自 **FlyWire**（FAFB v783，更新于 2025-06-23）：
  <https://flywire.ai/>，学术使用请遵守其
  [citation guidelines](https://codex.flywire.ai/about_flywire) 与
  [principles](https://flywire.ai/principles.html)；
- SNN / 张量计算 / 数据框架底座来自 **sciBASIC.NET**（GCModeller 项目运行时）；
- 贪吃蛇游戏世界模型来自 **Snake2**（pixelArtist）。

## 7. 延伸阅读

面向非专业读者的三部曲科普博客（含全部算法原理的通俗版）：

1. [`01-当你的大脑只有0和1-脉冲神经网络.md`](docs/01-当你的大脑只有0和1-脉冲神经网络.md)
   —— LIF 模型、脉冲编码方式、SNN 与深度学习的异同；
2. [`02-139255个神经元和她的通话记录-果蝇全脑连接组FAFB-v783.md`](docs/02-139255个神经元和她的通话记录-果蝇全脑连接组FAFB-v783.md)
   —— FlyWire 电镜重建流程、数据集字段、连接组到权重矩阵的三步变身；
3. [`03-给果蝇通电让它替你玩贪吃蛇-缸中脑与数字永生.md`](docs/03-给果蝇通电让它替你玩贪吃蛇-缸中脑与数字永生.md)
   —— 电刺激实验、贪吃蛇闭环架构，以及"缸中脑"哲学讨论。

## License

见 [LICENSE](LICENSE)。
