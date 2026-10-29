# GPU / 驱动行为规格

来源：旧版 `crates/core/src/gpu.rs`、`scan.rs::classify_gpu`、`sm.rs` 与真实机器对照。
新实现：`Domain/Gpu/*`、`Infrastructure/Gpu/NvidiaSmiGpuProbe.cs`。

## 读取

- 名称：`nvidia-smi --query-gpu=name --format=csv,noheader` 第一行；失败时返回 `NVIDIA GPU not detected`。
- 驱动：`nvidia-smi --query-gpu=driver_version --format=csv,noheader` 第一行；失败时为 `null`。
- HAGS：`HKLM\SYSTEM\CurrentControlSet\Control\GraphicsDrivers` 的 `HwSchMode` DWORD：`2` 开启、`1` 关闭、缺失或其他值未知。
- 不写注册表、不修改驱动配置。

## GPU 路由

按顺序匹配（名称大写）：

| 顺序 | 条件 | 路由 |
|---:|---|---|
| 1 | 消费级 RTX 40/50（`RTX 40`/`RTX 50` 后两位为 50–90） | `NotNeeded` |
| 2 | 含 `AMD` / `RADEON` / `INTEL` / `ARC A` | `Unsupported` |
| 3 | 独立的 ` ADA` token（不是 `ADAPTER`） | `NotNeeded` |
| 4 | 含 `QUADRO RTX` | `Sm75` |
| 5 | 含 `RTX A` | `Sm86` |
| 6 | 含 `RTX 30` | `Sm86` |
| 7 | 含 `GTX 16` | `Gtx16` |
| 8 | 含 `RTX 2050` | `Sm86` |
| 9 | 含 `RTX 20` | `Sm75` |
| 10 | 含 `GTX 10/9/7`、`GT 10/9/7/6`、`MX1–MX5`、`QUADRO`、`TESLA` | `NoTensorCore` |
| 11 | 含 `TITAN RTX` | `Sm75`（新版改进） |
| 12 | 其他 | `Unknown` |

`Unknown` 是 fail-open：只提示，不阻断。

## 功能闸门

| 功能 | 条件 | 结果 |
|---|---|---|
| 帧生成 | `Sm86`/`Sm75` + 驱动 ≥ 580 | `Supported` |
| 帧生成 | `Sm86`/`Sm75` + 驱动 < 580 | `DriverTooOld` |
| 帧生成 | `Gtx16`/`NoTensorCore`/`Unsupported` | `GpuNotSupported` |
| 帧生成 | `NotNeeded` | `GpuNotNeeded` |
| 帧生成 | 驱动未知 / 路由 Unknown | `Unknown` |
| Smooth Motion | `Sm86`/`Sm75` + 驱动 ≥ 616.64 | `Supported` |
| Smooth Motion | `Sm86`/`Sm75` + 驱动 < 616.64 | `DriverTooOld` |
| Smooth Motion | 其他路由 | `GpuNotSupported` / `GpuNotNeeded` |

驱动版本按 `major.minor` 比较；`616.64`、`620.10` 等格式直接解析。非数字版本为 Unknown。

## 真实机器对照（2026-10-29）

```
GPU: NVIDIA GeForce RTX 3070
Driver: 621.01
Route: Sm86
FrameGeneration: Supported
SmoothMotion: Supported
HAGS: unknown
```

旧版对同一台机器的路由同样是 SM86；新版把 R580/616.64 闸门统一收敛到 Domain，后续部署引擎直接复用。

## 测试

- `Domain.Tests/GpuRouteClassifierTests.cs`
- `Domain.Tests/GpuCompatibilityTests.cs`

## 已知限制

- 只读取 NVIDIA；AMD/Intel 机器在 `nvidia-smi` 缺失时显示 `NVIDIA GPU not detected` / `Unknown`，而不是 `Unsupported`。旧版也如此；后续可通过 WMI 补全。
- 多 GPU 机器取第一行；如需选择器，后续在设置页增加。
- HAGS 缺失时显示未知，不当作关闭。
