# 游戏库扫描行为规格

来源：旧版 `crates/core/src/scan.rs`、`apps/sidecar/src/main.rs` 与 2026-10-29 的真实机器对照。
新实现：`Infrastructure/Library/*`、`Core/Features/Library/*`、`Domain/Library/*`。

## 目标

只读发现游戏、渲染 EXE 和图形 API，写入 `%LOCALAPPDATA%\FrameGen-Manager-Next\library\library.json` 缓存；**不修改游戏目录**。

## 启动器

| 来源 | 规则 | 备注 |
|---|---|---|
| Steam | 注册表 `HKCU\Software\Valve\Steam\SteamPath`、HKLM `InstallPath`、`SteamExe` 父目录、Uninstall `DisplayName=Steam`、Program Files 的 `Steam` | 读取 `steamapps\libraryfolders.vdf` 的 `path`（兼容旧数字键）；解析 `appmanifest_*.acf` 的 `appid/name/installdir` |
| Epic | `%ProgramData%\Epic\EpicGamesLauncher\Data\Manifests\*.item` | `bIsApplication=false` 跳过；读取 `DisplayName/InstallLocation/AppName` |
| WeGame | 注册表 `Tencent\WeGame` 的安装路径 + `rail_apps` / `WeGameApps` 子目录 | 尽力而为；没有公开清单格式，仅收录包含 EXE 的目录 |
| 散装 | 固定磁盘根目录下的 `Games`、`Game`、`My Games`、`SteamLibrary`、`GOG Games`、`Epic Games`、`XboxGames`、`Origin Games`、`Repacks`、`游戏`、`单机游戏`、`游戏库` | 每根目录最多 300 个子目录，最多 80 个游戏；目录深度 ≤3，EXE ≥64 KiB |
| 手动 | 用户在 UI 中选择一个 EXE | 目录 = EXE 父目录，渲染 EXE = 所选 EXE |

Steam junk 跳过（精确名称）：`steamworks common redistributables`、`steamvr`、`steamvr beta`、`proton experimental`、`proton hotfix`、`proton - experimental`、`steam linux runtime`；前缀 `steam linux runtime `；前缀 `proton ` 且后面第一个字符是数字。

## 去重与缓存

- 启动器结果按 `InstallDirectory`（完整路径、大小写不敏感）去重；优先级 Steam > Epic > WeGame > 散装。
- 手动条目优先于扫描条目；`GameLibrary.EnumerateVisibleGames()` 按安装目录去重。
- 移除游戏只把 id 写入 `ignoredIds`，手动条目仍保留在 `Manual` 中；`EnumerateRemovedGames()` 按 id 去重列出这些条目，恢复时从 `ignoredIds` 删除即可重新显示。
- 缓存文件损坏时重命名为 `library.json.bad`，本次从空库开始，不覆盖原文件。

## 渲染 EXE 选择

目录遍历：深度 ≤5，最多 30,000 个条目；不跟随 reparse point；跳过 `_CommonRedist`、`DirectX`、`vcredist`、`EasyAntiCheat`、`Engine` 等低价值目录。

文件名排除（包含即跳过）：`unins`、`vcredist`、`dxsetup`、`dxwebsetup`、`crashreport`、`unitycrashhandler`、`unrealcefsubprocess`、`epicwebhelper`、`eossdk`、`easyanticheat`、`eaclauncher`、`beservice`、`prereq`、`installer`、`subprocess`、`helper`、`launcher`、`vconsole`。

评分（最高分胜出；同分取更大文件）：

| 条件 | 分数 |
|---|---:|
| 文件名以 `-win64-shipping.exe` 结尾 | +50 |
| 否则以 `-win32-shipping.exe` 结尾 | +20 |
| 父目录名 = `Win64` | +10 |
| 父目录 Win64、祖父目录 Binaries、再上一级目录名 = EXE stem | +30 |
| 安装目录名 = EXE stem | +20 |
| 文件 > 10 MiB | +15 |
| 否则文件 > 2 MiB | +3 |

最高分 ≤0 时返回 `null`，由用户手动选择；不猜。

## 图形 API

优先级（与旧版一致）：

1. DXVK/wrapper：`dxgi.dll`/`d3d12.dll`/`d3d11.dll`/`d3d9.dll` 自身导入 `vulkan-1.dll` → Vulkan。
2. EXE 文件名 token：`d3d12`/`dx12` → DX12；`vulkan`/`vk` → Vulkan；`d3d11`/`dx11` → DX11；`d3d10`/`dx10` → DX10；`d3d9`/`dx9` → DX9；`opengl`/`gl` → OpenGL。
3. PE 导入：`d3d12.dll`/`d3d12core.dll` → DX12；`vulkan-1.dll` → Vulkan；`d3d11.dll` → DX11；`d3d10.dll`/`d3d10_1.dll` → DX10；`d3d9.dll` → DX9；`opengl32.dll` → OpenGL。
4. 同目录 runtime DLL：`vulkan-1.dll` → Vulkan；`d3d12.dll` → DX12；其他不单独作为证据。
5. 主 EXE Unknown 时，尝试引擎 DLL：`unityplayer.dll`、`engine2.dll`、`engine.dll`、`crysystem.dll`，重复步骤 2–4。
6. 仍 Unknown。

`dxgi.dll` 单独存在不算证据；`Unknown` 不阻断扫描，由部署层决定是否提示手动选择。

## 真实机器对照（2026-10-29）

| 游戏 | 旧版 API | 新版 API |
|---|---|---|
| 霍格沃茨之遗 | Dx12 | Direct3D12 |
| PUBG | Dx12 | Direct3D12 |
| Wallpaper Engine | Dx11 | Direct3D11 |
| VTube Studio | Dx12 | Direct3D12（UnityPlayer 回退） |
| Nomad Drive Demo | Dx12 | Direct3D12（UnityPlayer 回退） |
| Counter-Strike 2 | Unknown | Unknown |

新版额外发现散装游戏 `旅行记录0.17`（旧缓存未包含）。

## 测试

- `Domain.Tests/RenderExeLocatorTests.cs`
- `Domain.Tests/GraphicsApiClassifierTests.cs`
- `Infrastructure.Tests/ValveKeyValuesTests.cs`
- `Infrastructure.Tests/SteamGameScannerTests.cs`
- `Infrastructure.Tests/EpicGameScannerTests.cs`
- `Infrastructure.Tests/GameLibraryStoreTests.cs`
- `Infrastructure.Tests/GameProbeTests.cs`
- `Infrastructure.Tests/PeImportReaderTests.cs`

## 已知限制

- WeGame 没有公开清单格式，当前实现比旧版更保守，可能漏掉部分游戏；可在拿到真实样本后细化。
- 散装扫描遵循旧版启发式，可能把大型工具目录误认为游戏；UI 支持手动移除并加入忽略列表。
- 不读取封面、不下载元数据；封面留到后续阶段。
