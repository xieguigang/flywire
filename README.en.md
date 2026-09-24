# Flywire AI

> 🪰 **Take the complete wiring diagram of a real fly brain, put it inside a computer, wake it up with electrical pulses — and let it play a game of Snake for you.**
>
> *One connectome, one simulation, one cyber-life.*

[中文说明](README.md) | **English**

[![.NET](https://img.shields.io/badge/.NET-10.0-blueviolet)](https://dotnet.microsoft.com/)
[![Language](https://img.shields.io/badge/language-VB.NET-blue)](https://learn.microsoft.com/dotnet/visual-basic/)
[![Dataset](https://img.shields.io/badge/dataset-FAFB%20v783-orange)](https://flywire.ai/)
[![Framework](https://img.shields.io/badge/framework-sciBASIC.NET-success)](https://github.com/xieguigang/GCModeller)

---

## 1. What Is This

**Flywire AI** is a connectome-driven, whole-brain spiking neural network simulation platform. It assembles the female adult fly brain connectome dataset **FAFB-v783** released by the [FlyWire](https://flywire.ai/) project (**139,255** neurons, roughly **50.67 million** synapses) directly into a runnable **LIF spiking neural network (SNN)**, and provides three major capabilities on top of it:

1. **3D brain model visualization** — a point cloud of 139k neurons, coloring by neuropil, and step-by-step replay of spike activity;
2. **Whole-brain electrical stimulation simulation** — inject external currents into an arbitrary set of neurons and watch spike activity propagate through real neural circuitry (with CUDA GPU acceleration and a CPU SIMD backend for cross-checking);
3. **A closed-loop cyber-organism** — wire the game of Snake into the fly brain: the game world is encoded into sensory currents fed into 5,536 afferent sensory neurons, and motor commands (up/down/left/right) are decoded from the spiking of 441 efferent motor neurons. The entire game is driven by a real biological connectome.

The core idea in one sentence: **the network weights are never trained — they are copied verbatim from measured biological data.** All synaptic topology, connection strengths, and excitatory/inhibitory polarity come from an electron-microscopy-reconstructed real brain; the SNN merely runs this "wiring diagram" under the rules of spiking neural computation.

## 2. Solution Layout

```text
flywire.slnx
├── src/FlywireAI/        Core library: dataset loading + connectome modeling + SNN simulation kernel
│   ├── FAFBv783/         Typed models & loaders for every FAFB v783 table (streamed CSV / MsgPack pack)
│   └── Connectome/       SynapseTriplets / ConnectomeMatrix / BrainNetwork /
│                         Stimulation / BrainSimulation / GpuRuntime / SnnConfig
├── src/Neuropils/        WinForms workbench: 3D visualization, stimulation bench, result charts, Snake spectating
├── src/FlywireSnake/     Game interface layer: sensory encoder / motor decoder / imitation learning + policy gradient / session loop
├── src/test/             End-to-end demo tests (data-loading regression, CPU vs GPU cross-check, msgpack round trip)
└── docs/                 Dataset field documentation + a three-part blog series (SNN / connectome / brain in a vat)
```

All low-level dependencies come from the in-house **sciBASIC.NET** runtime (the scientific computing foundation of the GCModeller project):

| Dependency | Purpose |
| --- | --- |
| `Microsoft.VisualBasic.DeepLearning.SpikingNeuralNetwork` | Sparse recurrent LIF network, CSR sparse-matrix SpMM |
| `Microsoft.VisualBasic.MachineLearning.TensorFlow` | Pluggable tensor-compute backend contract (SIMD CPU kernels) |
| `ILCudaTensor` | CUDA GPU backend (NVRTC fused operators + device-resident buffers) |
| `msgpack` | MsgPack binary dumps of the full dataset for fast loading |
| `DataFrame` | CSV reflection storage provider: `LoadCsv(Of T)` full loads and `AsLinq(Of T)` streaming loads |

## 3. Dataset: FAFB v783

All data comes from [FlyWire Codex](https://codex.flywire.ai/api/download?dataset=fafb); field definitions are documented in [`docs/fafb-v783-data.md`](docs/fafb-v783-data.md). The core tables:

| File | Scale | Purpose |
| --- | --- | --- |
| `connections_princeton.csv` | 5.34 M rows (connections with < 5 synapses filtered out) | Synapse triplets `(pre, post, syn_count)` — the sole topology source of the network |
| `classification.csv` | 139,255 rows | The `flow` column: afferent (sensory input, 5,536) / efferent (motor output, 441) / intrinsic |
| `consolidated_cell_types.csv` | 138,327 rows | 8,772 cell types, enabling targeted stimulation by type |
| `coordinates.csv` | 238,909 rows | Neuron spatial coordinates — the source of the 3D point cloud |
| `neuropil_synapse_table.csv` | 134,181 rows × 321 cols | Input/output statistics over 80 neuropils, the basis for neuropil coloring |
| `sk_lod1_783_healed.zip` | SWC skeleton archive | Neuron morphology (skeletons), parsed by `SwcParser` |

`root_id` values are 64-bit integers and the loader includes dedicated `Int64` precision regression tests; multi-million-row tables are read row-by-row through a lazy streaming interface (O(1) memory), while small tables use reflection-based full loads. The entire dataset can be dumped once into a MsgPack pack (`FafbMsgPackStorage`); every consumer afterwards (visualization, simulation, Snake) deserializes the pack directly — turning "read five million CSV rows" into "load a snapshot."

## 4. Algorithm Details

### 4.1 LIF Neurons: A Leaky Bucket

Every neuron evolves under a discrete **Leaky Integrate-and-Fire** model (β = 0.9, θ = 1.0, reset to zero on spike):

$$
u[t] = \beta \, u[t-1] + \sum_{j} W_{ij} \, s_j[t-1] + I_i^{\text{ext}}[t],
\qquad
s_i[t] = \mathbb{1}\!\left[u[t] \geq \theta\right],\;\; u[t] \leftarrow 0 \text{ on spike}
$$

The whole-brain network is a **single sparse recurrent LIF layer** — 139,255 neurons sharing one CSR sparse weight matrix, with one sparse SpMM per time step. The assembly entry point lives in `BrainNetworkBuilder`:

```vb
' Connectome/BrainNetworkBuilder.vb (excerpt)
Dim net As New SpikingNetwork(stimulation.Count, config.TimeSteps, config.Encoding)

net.Rng = New Random(config.Seed)

' CSR weight matrix + LIF parameters + input injection map, assembled into a sparse recurrent layer
Call net.AddSparseLayer(matrix.Synapses, config.Beta, config.Threshold,
                        config.ResetMode, stimulation.InputMap)

Return New BrainNetwork(net, matrix, matrix.Synapses, matrix.Triplets, ...)
```

### 4.2 From Connection Table to Weight Matrix

Weight generation rules (`SynapseTriplets`): **polarized synapse counts + postsynaptic structural normalization**.

```vb
' Weight convention: weight = polarity(nt_type) × syn_count
'   GABA (inhibitory)        -> -1
'   GLUT / ACH etc. (excitatory) -> +1 (empty values treated as excitatory)
```

Then three steps:

1. **CSR merge**: `SparseMatrix.FromTriplets(Pre, Post, Weight)` accumulates repeated synapses of the same `(pre, post)` cell pair across neuropils into a single edge;
2. **Structural normalization**: scale by the absolute input-strength sum of each **postsynaptic** neuron so that every neuron's total input magnitude equals 1 (deliberately not using signed sums — for mixed E/I lists the algebraic sum may be near zero, making signed normalization unreliable):

```vb
' SynapseTriplets.NormalizeStructural (excerpt)
For k As Integer = 0 To values.Length - 1
    absSum(columns(k)) += std.Abs(values(k))    ' Σ|w| per postsynaptic neuron
Next

For k As Integer = 0 To values.Length - 1
    Dim total As Double = absSum(columns(k))
    If total > 0 Then
        values(k) /= total                       ' Σ|w| = 1 per post neuron
    End If
Next
```

3. **Global gain**: the unit weights after structural normalization are kept as `BaseValues`; `SetGain(g)` rewrites `values = base × g` **in place without rebuilding the CSR** — and when the gain has not changed, the weight array is left untouched, so the GPU device-side cache is never invalidated (otherwise a 60 MB host-to-device upload would sneak into every time step's timing).

### 4.3 Global Gain Calibration

After normalization the weights need a global gain $G$ to land in a sensible firing regime. Rather than guessing, the project **auto-calibrates with short probe runs** (`BrainSimulation.CalibrateGain`): for each candidate gain $G \in \{1, 2, 4, 8\}$, run a 10-step probe simulation and pick the gain whose active-neuron fraction lands closest to the target (5% by default):

```text
For each candidate gain G:
    Run a CalibrationProbeSteps = 10-step probe with G
    Compute active fraction f(G) = neurons that fired at least once / N
Pick the G minimizing |f(G) − TargetActiveFraction|
```

Too small a $G$ leaves the whole brain asleep; too large a $G$ triggers global epilepsy. Calibration tunes the simulation into a regime of "sparse, structured activity" (acceptance range: 1%–30%). You may also set `SnnConfig.GlobalGain` explicitly to skip calibration.

### 4.4 Electrical Stimulation Simulation

Stimulation plans (`Stimulation`) support two modes:

- **RandomNeurons**: pick 5,000 neurons with a fixed seed and inject current directly;
- **CellType**: target neurons by `group` / `class` / `primary_type` annotations (e.g. "stimulate all visual neurons only") — effectively playing a slideshow inside the fly's dream.

The input tensor has shape `[1, Count]`; every step injects the same constant current into the neurons named by the injection map (`SpikeEncoding.DirectCurrent`, deterministic and reproducible; switchable to RateCoding Bernoulli sampling). `BrainSimulation.Run` drives `SparseLIFLayer.ForwardStep` one time step at a time and collects per-step statistics:

```text
BrainSimulationResult
  ├── Counts(i)          spike count per neuron          -> top-firing neuron leaderboard
  ├── PerStepSpikes[t]   total spikes / active neurons per step -> per-step activity curves
  ├── MeanFiringRate     whole-brain mean firing rate = TotalSpikes / Units / T
  ├── ActiveFraction     fraction of active neurons
  └── Backend / StepPath / FallbackSteps actual compute path (CUDA/SIMD, fused/op-by-op)
```

The simulator explicitly calls `ForwardStep` step by step (semantically identical to the library's internal `ForwardSparse`), and consistency with the one-shot `ForwardSpikes` path is asserted by the tests.

### 4.5 GPU Acceleration: A Layered CUDA Backend

Tensor compute follows a layered contract — **CUDA → TensorFlow → SNN**: the SNN library programs only against the backend interface and has no idea the GPU exists; "registering the GPU" can only be done by the topmost application project (`GpuRuntime`):

```vb
' Register before assembling the network; on failure (no NVIDIA GPU / NVRTC unavailable / driver mismatch) it falls back to CPU SIMD automatically
If config.UseGpu AndAlso GpuRuntime.TryRegister(config.GpuDeviceOrdinal, config.GpuNvrtcPath) Then
    ' ForwardStep automatically uses the NVRTC fused single-step operator + device-resident state buffers
End If

' ... simulation ...

Call GpuRuntime.ReleaseDeviceBuffers()   ' Return VRAM when done
```

Engineering notes:

- **Fused single-step operator** (`LifStep`): leak, SpMM, threshold check, and reset are fused into a single kernel launch, avoiding the five intermediate tensors per step of the op-by-op path;
- **Device-resident buffers**: membrane potentials and spike states stay in VRAM; with `KeepHistory = False` the whole simulation performs zero per-step read-backs (fastest mode); at the Double64 setting the GPU and CPU results are **bit-for-bit identical**, enabling cross-check acceptance tests;
- Whole-brain scale is nnz ≈ 3.73 M, well above the GPU SpMM minimum non-zeros threshold (65,536 by default).

### 4.6 Letting the Fly Play Snake: A Bidirectional Brain–Computer Interface Loop

`FlywireSnake` wires the game and the brain into a per-tick closed loop (`SnakeSession`; one tick per game frame):

```text
1. Read the game state -> encode it into 16 sensory-channel intensities
2. Inject sensory currents into afferent neurons -> the fly brain advances one step (ForwardStep, state accumulated across ticks)
3. Read out efferent motor-neuron firing rates -> the decoder produces an action
4. Set the snake's heading -> move -> collision check -> the game world advances
```

**Sensory side** (`SnakeSensors`, 16 channels):

```vb
Public Const FoodSectors As Integer = 8         ' Food bearing: 8 sectors in the snake's heading frame
Public Const DangerChannels As Integer = 4      ' Collision danger: absolute up/down/left/right, 0/1
Public Const SpecialFoodSectors As Integer = 4  ' Special food (5 / 30 points) bearing, 4 sectors
Public Const ChannelCount As Integer = 16

' Food-channel intensity decays monotonically with distance: intensity(d) = 1 - d / R (R = sensing radius)
' Intensity × SensorCurrent(2.4) = current injected into the corresponding afferent neurons
```

Food bearings use **heading-relative** sectors rather than absolute directions — a snake cannot reverse, so "my front-left" is the learnable stimulus; the sensing radius spans the map diagonal (full visibility), otherwise the teacher's labels and the brain's perception disagree and the trained readout just wanders in circles.

**Motor side** (`SnakeDecoder`, a linear brain–computer interface): the 441 efferent motor neurons yield a feature vector of 4-tick sliding-window firing rates, linearly mapped to softmax logits over 4 actions; the forbidden reverse action is set to $-\infty$, and a decision filter suppresses per-tick flip-flopping.

**Readout training** — the 139,255-neuron connectome in the middle stays untouched; only this linear decoder is trained, in three stages (`SnakePolicyGradient`):

| Stage | Method | Objective |
| --- | --- | --- |
| 1 | Imitation learning: a greedy teacher demonstrates; (firing features → teacher action) pairs are recorded and fitted with softmax cross-entropy | Per-tick action agreement of 74%–78% |
| 2 | DAgger: states the decoder itself wanders into are added to the training set | Mitigate distribution shift |
| 3 | REINFORCE with a moving-baseline policy gradient | Directly optimize the game score |

Imitation pushes agreement up, yet actual play scores only ~8 points (the demonstration teacher scores 95) — **error accumulation**: once the snake drifts off it enters states never covered by the training set, and drifts ever further. So the third stage changes the objective — no longer "act like the teacher" but "bring the score home," with rewards designed as:

- **Eating food**: the real game-score increment (regular 1 / moving 5 / super 30);
- **Approaching prey**: a dense reward proportional to the drop in "converted cost (distance − value discount)" (`ShapingScale`; otherwise a whole episode contains only a few sparse rewards and gradient noise drowns the signal);
- **Every step**: a small penalty of 0.01; **dying**: a one-time penalty of 1.0.

Safety net: after every training block the policy is evaluated in closed loop and only the best-scoring weight snapshot is kept — in the worst case the result matches "no reinforcement learning at all."

## 5. Getting Started

### 5.1 Requirements

- .NET 10 SDK (VB.NET, `OptionStrict On`, warnings treated as errors);
- x64 platform (`flywire.slnx`, AnyCPU / x64 configurations);
- The solution references sibling projects such as sciBASIC.NET and Snake2; please lay out the repositories per the relative paths inside the slnx (`../GCModeller`, `../pixelArtist`, etc.);
- Optional: NVIDIA GPU + CUDA NVRTC (`nvrtc64_*.dll` auto-discovered via `CUDA_PATH`).

### 5.2 Data Preparation

Download the FAFB v783 CSV files from [FlyWire Codex](https://codex.flywire.ai/api/download?dataset=fafb) into one directory (see [`docs/fafb-v783-data.md`](docs/fafb-v783-data.md) for the field list) and point `SnnConfig.DataDir` at it; you may also pre-dump a MsgPack pack to speed up subsequent loads.

### 5.3 End-to-End Tests

`src/test` is a demo program covering the whole pipeline (sections can be run individually, e.g. `test 11` for the msgpack section only):

```text
 1. full loading via LoadCsv                  reflection-based full loads of small tables + row-count regression
 2. streaming loading via StreamXxx           streaming loads of large tables
 ...
 9. Drosophila brain SNN simulation           end-to-end whole-brain SNN simulation
10. full brain SNN: CPU vs GPU acceleration   CPU/GPU cross-check (bit-identical at Double64)
11. msgpack round trip                        data-pack round-trip consistency
```

### 5.4 Minimal Simulation Example (Library-API View)

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

' 1. Load connection table -> synapse triplets (polarized weights)
Dim index As ConnectomeIndex = config.LoadIndex()
Dim triplets As SynapseTriplets = SynapseTriplets.Load(config, index)

' 2. CSR matrix (merge duplicate edges + Σ|w|=1 structural normalization) -> assemble sparse recurrent LIF network
Dim tripletsStim As Stimulation = Stimulation.Create(config, index)
Dim brain As BrainNetwork = BrainNetworkBuilder.Build(config, triplets, tripletsStim)

' 3. Probe-calibrate the global gain -> run the simulation
Dim gain As Double = BrainSimulation.CalibrateGain(brain, config, tripletsStim)
brain.SetGain(gain)

Dim result As BrainSimulationResult = BrainSimulation.Run(brain, config, tripletsStim)
Console.WriteLine(result.Describe())
' => CellType: spikes=..., active=.../139255 (5.1%), mean rate=..., T=30, ... ms
```

### 5.5 Running the GUI

Launch `Neuropils` (WinForms):

- **3D brain model**: point cloud + neuropil coloring + live spike-activity highlighting (`PageFlywireCanvas`);
- **Stimulation bench**: configure a stimulation plan, run the simulation, inspect response curves and the top-firing neurons (`StimulationExperiment` / `PageResponseChart`);
- **Snake spectating**: load a MsgPack brain-model pack and enter `PageSnakeBrainGamePlay` to watch the fly brain drive the snake's every move tick by tick, with a live view of the tick's sensory-channel intensities, firing neurons, and per-motor-group spike counts.

## 6. Citations & Acknowledgements

- The connectome data comes from **FlyWire** (FAFB v783, updated 2025-06-23): <https://flywire.ai/>. Academic use must follow its [citation guidelines](https://codex.flywire.ai/about_flywire) and [principles](https://flywire.ai/principles.html);
- The SNN / tensor-compute / data-framework foundation is **sciBASIC.NET** (the runtime of the GCModeller project);
- The Snake game-world model comes from **Snake2** (pixelArtist).

## 7. Further Reading

A three-part blog series for general readers (plain-language versions of all the algorithms above):

1. [`01-当你的大脑只有0和1-脉冲神经网络.md`](docs/01-当你的大脑只有0和1-脉冲神经网络.md) — the LIF model, spike-encoding schemes, and how SNNs differ from deep learning (Chinese);
2. [`02-139255个神经元和她的通话记录-果蝇全脑连接组FAFB-v783.md`](docs/02-139255个神经元和她的通话记录-果蝇全脑连接组FAFB-v783.md) — the FlyWire EM-reconstruction pipeline, dataset fields, and the three-step transformation from connectome to weight matrix (Chinese);
3. [`03-给果蝇通电让它替你玩贪吃蛇-缸中脑与数字永生.md`](docs/03-给果蝇通电让它替你玩贪吃蛇-缸中脑与数字永生.md) — the stimulation experiment, the Snake closed loop, and a philosophical discussion of the "brain in a vat" (Chinese).

## License

See [LICENSE](LICENSE).
