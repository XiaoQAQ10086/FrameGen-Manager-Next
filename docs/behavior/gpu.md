# GPU / 驱动行为规格

来源：旧版 `crates/core/src/gpu.rs`、`scan.rs::classify_gpu`、`sm.rs` 与真实机器对照。
新实现：`Domain/Gpu/*`、`Infrastructure/Gpu/NvidiaSmiGpuProbe.cs`。

## 读取

- 名称：`nvidia-smi --query-gpu=name --format=csv,noheader` 第一行；`nvidia-smi` 缺失时回退读取注册表 `HKLM\SYSTEM\CurrentControlSet\Control\Video` 的 `DriverDesc`，让 AMD/Intel 机器也能显示实际显卡名称（跳过 `Microsoft Basic` 适配器）；完全取不到时返回 `NVIDIA GPU not detected`。
- 驱动：`nvidia-smi --query-gpu=driver_version --format=csv,noheader` 第一行；回退路径读取同一注册表项的 `DriverVersion`（Windows 驱动版本号格式），仍取不到时为 `null`。
- HAGS：先枚举所有显示适配器（`D3DKMTEnumAdapters2`），对每个适配器调用 `D3DKMTQueryAdapterInfo(KMTQAITYPE_WDDM_2_7_CAPS = 70)` 读取 `HwSchSupported`（bit 0）和 `HwSchEnabled`（bit 1）；任一适配器报告已开启即视为开启。只查询主显示器 DC 会在核显/虚拟显示器上误判，因此枚举优先，枚举不可用时才回退到 `D3DKMTOpenAdapterFromHdc`。
- 注册表 `HKLM\SYSTEM\CurrentControlSet\Control\GraphicsDrivers\HwSchMode` 与实时状态合并：实时已开启 → `Enabled`；实时未开启但注册表为 `2` → `PendingReboot`（设置已开、重启后生效）；实时未开启且注册表为 `1` → `Disabled`；实时未开启、注册表缺失时按适配器是否支持区分 `Disabled` / `Unsupported`。注册表缺失不当作关闭。
- 日志记录 `HAGS probe: state/live/supported/registry/detail`，其中 `detail` 是各适配器的 caps 原始值，用于诊断核显/虚拟显示器/驱动差异；不写注册表、不修改驱动配置。
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

## Rust 迁移状态（Phase 4）

- `fgm-infra/src/gpu/gpu_detector.rs`：`GpuDetector::detect` 调用 `nvidia-smi --query-gpu=name,driver_version --format=csv,noheader`，5 秒超时；失败返回 `None`。
- `GpuDetector::classify` 纯函数按名称映射 `GpuRoute`，覆盖 RTX 50/40、AMD/Intel、Quadro RTX、RTX A、RTX 30、GTX 16、RTX 2050、RTX 20、Titan RTX、Pascal 级别；驱动 `major.minor >= 616.64` 才算支持。
- `fgm-infra/src/gpu/hags.rs`：`HagsProbe::read` 走 D3DKMT 实时 caps，失败时用 `HwSchMode` 注册表分类；`GpuDetector` 把结果并入 `GpuInfo.hags`。
- FFI：`fgm_gpu_json` 返回 `hags`（`enabled`/`disabled`/`pendingReboot`/`unknown`）、`hagsDetail`、`driverName`；`fgm_open_hags_json` 打开 `ms-settings:display-advancedgraphics`。
- Flutter：帧生成页左侧显卡面板显示驱动、路由与 HAGS 状态，右侧提供“打开显卡设置”；状态为 `pendingReboot` 时提示重启。
- 该机器 2026-10-29 记录中的 `HAGS: unknown` 已由实时探针补齐（实测“已开启”）。
- Rust 测试：`gpu_detector.rs` 内单元测试覆盖路由和驱动阈值；`hags.rs` 覆盖 caps/注册表分类。

## 测试

- `Domain.Tests/GpuRouteClassifierTests.cs`
- `Domain.Tests/GpuCompatibilityTests.cs`

## 已知限制

- 注册表回退只用于展示名称/驱动；路由仍按名称分类，AMD/Intel 得到 `Unsupported`。Smooth Motion 仍会拒绝；帧生成不再门控（上游仅支持 NVIDIA RTX 20–50，其他显卡可以尝试但运行时不保证生效）。
- 多显卡/虚拟显示器环境可能返回第一个非基本显示适配器，不保证与用户期望的主卡一致。
- 多 GPU 机器取第一行；如需选择器，后续在设置页增加。
- HAGS 缺失时显示未知，不当作关闭。注册表为 `2` 但实时 caps 仍为关闭时显示“待重启生效”，而不是“已关闭”；这通常表示设置已写入但驱动尚未在本次启动中启用。
- 多显卡机器会汇总所有适配器：任一适配器报告 HAGS 已开启即显示已开启，避免主显示器接在核显或虚拟显示器上时误报关闭。
