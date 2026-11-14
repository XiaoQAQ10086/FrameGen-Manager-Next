# GPU / 驱动行为规格

来源：旧版 `crates/core/src/gpu.rs`、`scan.rs::classify_gpu`、`sm.rs` 与真实机器对照。
新实现：`Domain/Gpu/*`、`Infrastructure/Gpu/NvidiaSmiGpuProbe.cs`。

## 读取

- 名称：`nvidia-smi --query-gpu=name --format=csv,noheader` 第一行；`nvidia-smi` 缺失时回退读取注册表 `HKLM\SYSTEM\CurrentControlSet\Control\Video` 的 `DriverDesc`，让 AMD/Intel 机器也能显示实际显卡名称（跳过 `Microsoft Basic` 适配器）；完全取不到时返回 `NVIDIA GPU not detected`。
- 驱动：`nvidia-smi --query-gpu=driver_version --format=csv,noheader` 第一行；回退路径读取同一注册表项的 `DriverVersion`（Windows 驱动版本号格式），仍取不到时为 `null`。
- HAGS：`HKLM\SYSTEM\CurrentControlSet\Control\GraphicsDrivers` 的 `HwSchMode` DWORD：`2` 开启、`1` 关闭、缺失或其他值未知。
- 不写注册表、不修改驱动配置。

## 检测时机

- 每个进程只执行一次 `nvidia-smi` 探测；结果由 `GpuService` 缓存，帧生成页、Smooth Motion 页和诊断包共享。
- `AppWebBridge` 构造时开始预热探测，与 WebView2 初始化并行，页面请求通常直接命中缓存。
- 调用方取消只取消自己的等待，不会取消共享探测；探测失败不缓存，下次请求会重试。
- GPU 卡片独立于游戏列表渲染，不再等待 `games.state` 完成。
- 检测结果在本次启动内视为稳定；HAGS 或驱动变更需要重启程序后刷新。

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

帧生成自资产 0.4.2 起不再使用上表拦截部署：RTX 20/30（`Sm86`/`Sm75`）与 RTX 40/50（`NotNeeded`）都可以部署，实际支持范围为 RTX 20–50 系；40/50 使用上游的 PTX 内核。`Gtx16`/`Unsupported` 不属于上游支持范围，界面不再拦截，但运行时不保证生效。Smooth Motion 仍按上表门控。

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

- 注册表回退只用于展示名称/驱动；路由仍按名称分类，AMD/Intel 得到 `Unsupported`。Smooth Motion 仍会拒绝；帧生成不再门控（上游仅支持 NVIDIA RTX 20–50，其他显卡可以尝试但运行时不保证生效）。
- 多显卡/虚拟显示器环境可能返回第一个非基本显示适配器，不保证与用户期望的主卡一致。
- 多 GPU 机器取第一行；如需选择器，后续在设置页增加。
- HAGS 缺失时显示未知，不当作关闭。
