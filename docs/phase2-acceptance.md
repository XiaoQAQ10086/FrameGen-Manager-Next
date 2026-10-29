# Phase 2 本地验收记录

日期：2026-10-29  
机器：Windows 10/11 x64，NVIDIA GeForce RTX 3070，驱动 621.01

## 目标

只读扫描游戏库、识别 GPU 和图形 API，提供 WPF 页面和 CLI；不修改游戏目录。

## 结果

### GPU

```text
GPU: NVIDIA GeForce RTX 3070
Driver: 621.01
Route: Sm86
FrameGeneration: Supported
SmoothMotion: Supported
HAGS: unknown
```

### 游戏库

`fgm-next library scan` 找到 7 个游戏；旧版缓存为 6 个，新增散装游戏 `旅行记录0.17`。

| 游戏 | 旧版 API | 新版 API | 目标目录 |
|---|---|---|---|
| 霍格沃茨之遗 | Dx12 | Direct3D12 | `...\HogwartsLegacy\Phoenix\Binaries\Win64` |
| PUBG: BATTLEGROUNDS | Dx12 | Direct3D12 | `...\TslGame\Binaries\Win64` |
| Wallpaper Engine | Dx11 | Direct3D11 | `...\wallpaper_engine\distribution\bin` |
| VTube Studio | Dx12 | Direct3D12 | 游戏根目录（UnityPlayer 回退） |
| Nomad Drive Demo | Dx12 | Direct3D12 | 游戏根目录（UnityPlayer 回退） |
| Counter-Strike 2 | Unknown | Unknown | `...\game\bin\win64` |
| 旅行记录0.17 | 旧缓存无 | Unknown | `D:\Game\旅行记录0.17` |

### UI

通过 UI Automation 自动切换并截图验证：

- 游戏库：7 行，列宽/省略号正常，打开/移除按钮可见。
- GPU 状态：RTX 3070、621.01、SM86、帧生成支持、Smooth Motion 支持、HAGS 未知。
- 日志与诊断：共享读取日志成功（修复了 Serilog 独占文件导致的读取失败）。
- 关于：版本、.NET 10.0.14、数据目录、便携模式、仓库链接。

截图保存在本地 `artifacts/`（git-ignored）。

## 门禁

- `build/verify.ps1 -Configuration Debug`：通过。
- 测试：185/185。
- `dotnet format --verify-no-changes`：通过。
- 四个 pack schema 校验：通过。
- 质量检查：101 个 C# 文件通过（行数/分层/P-Invoke/硬编码路径/Core 中文串）。

## 已知问题

- 窗口同时显示系统标题栏和 Wpf.Ui 自定义 TitleBar，属于外观问题，留到 UI 打磨阶段。
- 日志页当前显示的是开发过程中修复前的旧错误堆栈；新日志没有该错误。
- WeGame 扫描是基于 `rail_apps`/`WeGameApps` 的保守实现，尚未在真实 WeGame 库上对照。
- HAGS 在本机注册表缺失，显示未知；按旧版语义不当作关闭。

## 复现

```powershell
fgm-next.exe gpu
fgm-next.exe library scan
fgm-next.exe library list --json
```

或启动 `FrameGenManager-Next.exe`，在“游戏库”点击“扫描游戏”。
