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
| 散装 | 固定/可移动磁盘根目录下的 `Games`、`Game`、`My Games`、`SteamLibrary`、`GOG Games`、`Epic Games`、`Origin Games`、`Repacks`、`游戏`、`单机游戏`、`游戏库` | 每根目录最多 300 个子目录，最多 80 个游戏；目录深度 ≤3，EXE ≥64 KiB |
| Xbox | 固定/可移动磁盘的 `XboxGames` 子目录；识别 `Content` 目录或 `MicrosoftGame.config`/`AppxManifest.xml` | 允许 Probe 进入 Xbox 的 `Content`；受 ACL 限制无法读取时仍列出游戏，但标记为不可部署 |
| 手动 | 用户在 UI 中选择一个 EXE | 目录 = EXE 父目录，渲染 EXE = 所选 EXE |

Steam junk 跳过（精确名称）：`steamworks common redistributables`、`steamvr`、`steamvr beta`、`proton experimental`、`proton hotfix`、`proton - experimental`、`steam linux runtime`；前缀 `steam linux runtime `；前缀 `proton ` 且后面第一个字符是数字。

## 去重与缓存

- 启动器结果按 `InstallDirectory`（完整路径、大小写不敏感）去重；优先级 Steam > Epic > WeGame/Xbox > 散装。
- 手动条目优先于扫描条目；`GameLibrary.EnumerateVisibleGames()` 按安装目录去重。
- 移除游戏只把 id 写入 `ignoredIds`，手动条目仍保留在 `Manual` 中；`EnumerateRemovedGames()` 按 id 去重列出这些条目，恢复时从 `ignoredIds` 删除即可重新显示。
- 已移除的游戏可以“删除记录”：只从 `Manual`、`Scanned`、`ignoredIds` 和 `targetOverrides` 中清掉该条目，不删除游戏文件和部署备份；如果游戏仍然安装，下次扫描会重新发现它。游戏目录已不存在时标记“游戏目录不存在”，恢复按钮禁用。
- 用户可在部署弹窗选择“手动选择游戏实际启动 EXE”；选择结果写入 `targetOverrides[gameId]`，覆盖该游戏的渲染 EXE 和目标目录，重新扫描或重启后仍然生效。覆盖只影响目标选择，不修改游戏文件。
- 页面读取游戏库时并发探测数量等于 CPU 逻辑核心数，结果按游戏库顺序返回；启动不再逐个串行等待。
- 扫描时各启动器并行枚举（Steam/Epic/WeGame/散装互不依赖），再统一去重；游戏探测也使用全部逻辑核心，不再写死 6/12 的上限。
- 扫描进度通过 `progress` 事件上报，阶段为 `launchers`（完成一个启动器）与 `games`（完成一个候选游戏），携带 `current/total`；扫描结束后、重建游戏状态前上报 `finalizing`。前端左下角进度卡显示阶段、当前对象、`当前/总数` 与百分比，结束后立即隐藏。
- 扫描按钮在运行期间进入加载态：文字完全隐藏（包括文字阴影），只显示转圈；同一时刻只允许一个扫描任务。
- 启动不会自动枚举启动器：只读取 `library.json` 缓存并检查部署状态；重新发现游戏只能通过“扫描游戏”。
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

`dxgi.dll` 单独存在不算证据；但目录名（如 Witcher 3 的 `x64_dx12`）可作为导入表之后的补充证据。`Unknown` 不阻断扫描，DLSS5 详情页会使用用户选中的 API。

## Rust 迁移状态（Phase 3）

- `fgm-infra/src/pe/pe_imports.rs`：读取 PE32/PE32+ 的 Import 与 Delay Import 表。
  - 支持最多 96 个 section、4096 个 descriptor、模块名最多 512 字节。
  - RVA 只在 section 覆盖范围内换算；越界读取返回 `ErrorCode.InvalidPe`，不 panic。
  - 模块名按大小写不敏感去重，保留原始大小写。
  - 无效 PE 返回 `InvalidPe`；调用方按“没有导入证据”处理，不阻断游戏扫描。
- `fgm-infra/src/library/render_exe_selector.rs`：渲染 EXE 选择。
  - 深度 ≤5、最多 30,000 条目、不跟随 reparse point。
  - 跳过 `_CommonRedist`、`DirectX`、`vcredist`、`EasyAntiCheat`、`Engine` 等目录。
  - 文件名排除、`-win64-shipping`/`-win32-shipping`、`Binaries/Win64`、安装目录名和文件大小评分。
  - 最高分 ≤0 返回 `None`，不猜。
- `fgm-infra/src/library/graphics_api_detector.rs`：图形 API 检测。
  - 优先级：Vulkan wrapper DLL → EXE 文件名 token → PE 导入 → 同目录 runtime DLL → 引擎 DLL 导入 → `Unknown`。
  - 支持 DX12/Vulkan/DX11/DX10/DX9/OpenGL。
- `fgm-infra/src/library/loose_game_scanner.rs`：散装游戏扫描。
  - 已知目录：`Games`、`Game`、`My Games`、`SteamLibrary`、`GOG Games`、`Epic Games`、`Origin Games`、`Repacks`、`游戏`、`单机游戏`、`游戏库`。
  - 每根目录最多 300 个子目录、80 个游戏；深度 ≤3；EXE ≥64 KiB；不跟随 reparse point。
  - 生成 `loose:<DeploymentKey>` id，选择渲染 EXE 并检测 API。
- `fgm-infra/src/library/library_cache.rs` / `library_store.rs`：`library.json` 缓存。
  - 字段：`schemaVersion`、`games`、`ignoredIds`、`targetOverrides`。
  - 缺失返回默认；损坏重命名为 `library.json.bad` 后从空库开始；写入走原子替换。
- `fgm-infra/src/library/steam_game_scanner.rs`：Steam 库扫描。
  - 解析 `libraryfolders.vdf` 的 `path`，兼容数字键和转义反斜杠。
  - 解析 `appmanifest_*.acf` 的 `appid/name/installdir`。
  - 跳过指定 junk 名称、`steam linux runtime ` 前缀和 `proton <数字>`。
- `fgm-infra/src/library/epic_game_scanner.rs`：Epic `.item` 清单扫描；`bIsApplication=false` 跳过。
- `fgm-infra/src/library/steam_locator.rs`：HKCU/HKLM Steam 路径 + Program Files 回退。
- `fgm-infra/src/library/epic_locator.rs`：`%ProgramData%\Epic\EpicGamesLauncher\Data\Manifests`。
- `fgm-infra/src/library/library_scanner.rs`：聚合 Steam → Epic → 散装，按安装目录大小写不敏感去重。
- `fgm-infra/src/library/library_service.rs`：扫描时保留手动条目（手动优先，按安装目录去重）、丢弃 `ignoredIds`、写回缓存；`add_manual` 用文件选择器结果建条目（名称优先 PE `ProductName`，否则文件名；id 为路径哈希），同一目录重复添加会替换旧条目；`launch` 从缓存按 id 取游戏，Steam/Epic 走协议，散装/手动/WeGame 直接启动渲染 EXE，Xbox 返回不支持。
- `fgm-infra/src/library/cover_service.rs`：先找 Steam 已下载的本地素材（`appcache/librarycache/<appid>/` 的 `library_600x900`/`header`/`library_hero`、旧扁平缓存与 `userdata/*/config/grid`），Windows 上改用 WinINET（跟随系统代理）下载，再依次尝试 `library_600x900`、旧 CDN header、`store_item_assets` header、`capsule_616x353`，成功后缓存到 `cache/covers/<appid>.jpg`；没有 Steam id 或商店素材缺失时，会用游戏名、去掉 “Demo” 的名称以及 EXE 的 ProductName 调用 Steam 商店搜索取封面；仍找不到才提取可执行文件自身图标（含通用 exe 图标）缓存到 `cache/icons/<hash>.png`，卡片以 contain 方式展示；只有连 exe 都没有时才显示首字母占位。Dart 侧不再只对 Steam 请求封面。
- 离线时跳过所有商店/CDN 请求：只使用 Steam 本地素材、缓存和 EXE 图标；渲染层只请求视口内的封面（最多 2 并发、200px 预加载边距），优先本地/下载 600×900 而不是 1200×1800，减少解码内存。
- 封面失败做负缓存：渲染层 10 分钟、原生 `cache/covers/miss-*` 1 小时；成功或用户重新扫描后重新尝试。下载与本地查找仍由全局锁串行，避免 urlmon/GDI+ 并发问题。
- 代理占用徽章只报告可信文件：带 Microsoft 公司信息的系统 DLL（如 CS2 自带的 `dbghelp.dll`）和 Vulkan Runtime 不报告；没有清单但哈希匹配帧生成/RTSS 20/30 SM 包的文件分别归为对应模式。未知的非 Microsoft 代理仍显示“其他程序”。
- 【添加游戏】使用系统文件选择器，标题按界面语言传入：中文为“选择游戏实际启动的 exe 程序，一般在 Win64 文件夹下”。
- 已移除的游戏按钮显示数量（`已移除的游戏（N）`）；列表展示封面、路径、恢复与删除。删除记录会保留 ignored id，后续扫描也不会重新出现在游戏库。
- Steam 启动：若 Steam 未运行，先启动 steam.exe；`steam://rungameid/<appid>` 会持续重试最多约 2 分钟，直到游戏进程出现，解决冷启动/未登录时排队命令丢失的问题。
- `fgm-infra/src/platform/drive_roots.rs`：固定/可移动盘符枚举。
- `fgm-ffi/src/commands_library.rs`：`fgm_library_add_manual_json`（无路径时打开原生选择器，取消返回 `cancelled`）、`fgm_launch_game_json`、`fgm_cover_json`。
- CLI：`fgm-next library scan [--root <dir>]...`、`fgm-next library list`。
- 测试：`pe_imports.rs`、`render_exe.rs`、`graphics_api.rs`、`library_cache.rs`、`steam_scanner.rs`、`epic_scanner.rs`、`library_scanner.rs`、`tests/library_service.rs`（手动添加、去重、缺失文件）。
- 部署弹窗可“选择启动 EXE”：选择结果写入 `targetOverrides`，同时更新该游戏的渲染 EXE 与目标目录，重新扫描后仍生效（`LibraryService::set_target_override` / `apply_overrides`）。
- `fgm-ffi/src/commands_library.rs::game_modes` 同时返回 Smooth Motion 状态（批量 NVAPI），游戏行显示“平滑运动”徽章。
- `fgm-infra/src/library/xbox_game_scanner.rs`：扫描每个盘符的 `XboxGames` 子目录；识别 `Content`、`MicrosoftGame.config`、`AppxManifest.xml`；在 `Content` 或游戏根目录选择渲染 EXE，目标目录取 EXE 所在目录；受 ACL 限制无法读取的目录直接跳过。`LibraryScanner` 的顺序为 Steam → Epic → Xbox → 散装。
- 移除/忽略：`list` 只返回可见游戏，扫描保留被忽略条目；`removed` 列出已移除，`restore_removed` 恢复，`forget` 清掉条目但不删游戏文件与备份。FFI 对应 `fgm_library_removed_json` / `fgm_library_restore_removed_json` / `fgm_library_forget_json`，UI 在列表头“已移除”弹窗中操作。
- 不迁移：WeGame 扫描（用户明确放弃）、Epic 封面（无合法来源）。

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

## 启动游戏

- 卡片“启动游戏”按启动器选择入口，不直接双击渲染 EXE，避免绕过 Steam/Epic 的 DRM 与在线服务：
  - Steam：`steam://rungameid/<AppId>`，AppId 来自 `appmanifest_*.acf`。
  - Epic：`com.epicgames.launcher://apps/<AppName>?action=launch&silent=true`，AppName 来自 `.item`。
  - 手动/散装/WeGame：直接运行 `RenderExePath`，工作目录为 EXE 所在目录。
  - Xbox：受保护游戏无法直接启动，返回 `LaunchUnsupported`，按钮禁用并提示从 Xbox 应用启动。
- 启动是只读操作，不改游戏文件；失败返回 `LaunchFailed` 或 `FileNotFound`，日志记录 game id、启动器和启动方式。
- App 只负责调用 `IGameLauncher`；入口选择规则在 `Core/Features/Library/GameLaunchPlanner`，便于测试。

## 已知限制

- WeGame 没有公开清单格式，当前实现比旧版更保守，可能漏掉部分游戏；可在拿到真实样本后细化。
- 散装扫描遵循旧版启发式，可能把大型工具目录误认为游戏；UI 支持手动移除并加入忽略列表。
- `WindowsApps` / 部分 `XboxGames` 受 ACL 保护。可以只读识别，但不能安全写入；不要修改这些目录的 ACL（会破坏商店更新，也可能触发反作弊）。UI 会显示受保护提示，部署按钮保持禁用。
- 不读取封面、不下载元数据；封面留到后续阶段。
