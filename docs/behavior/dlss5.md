# DLSS5 / ReShade 行为规格

来源：旧版 `dlss5.rs`、`ui/dlss5.js` 与真机安装器验证。
新实现：`Core/Features/Dlss5`、`Infrastructure/Dlss5`、`Infrastructure/Processes`、`dlss5.reshade-gitc` pack。

## Rust 迁移状态（Phase 4）

- `fgm-infra/src/dlss5/reshade_zip.rs`：解析 setup EXE 末尾追加的 ZIP（EOCD/central directory/local header），支持 stored 和 DEFLATE（`flate2`），只取文件名匹配的条目；不支持 ZIP64。
- `fgm-infra/src/dlss5/reshade_installer.rs`：
  - 根据渲染 EXE 是否 AMD64/ARM64 选择 `ReShade64.dll` / `ReShade32.dll`。
  - 目标已有模块时读取 PE `ProductName`，只有包含 `ReShade` 才允许覆盖；否则返回 `Conflict`，不误删第三方 `dxgi.dll`。
- `fgm-infra/src/pe/pe_imports.rs::is_64_bit`：读取 PE machine（0x8664 / 0xAA64），读取失败默认 64 位。
- `fgm-infra/src/dlss5/dlss5_service.rs`：
  - `install` 选择代理槽（DX10/11/12 → `dxgi.dll`，DX9 → `d3d9.dll`，OpenGL → `opengl32.dll`）。Vulkan 不写代理：调用包内 ReShade setup（`<game.exe> --headless --api vulkan`，超时 240 秒）注册全局 layer，`ReShadeApps.ini` 只登记该游戏；该路径要求管理员权限，非管理员返回 `AccessDenied`。，用 `PackDeploymentPlanner` 生成 pack 文件计划，再把解包出的 ReShade 模块（role=`proxy`）和缺失时的默认 `ReShade.ini`（role=`config`）作为带内容的内存文件追加到同一份计划，由 `DeploymentEngine` 统一备份/原子写入/校验/回滚。代理槽已有非 ReShade 模块时在写入前返回 `Conflict`。
  - 代理与 INI 因此进入部署清单：卸载时可以精确区分“这个文件是我们写的”和“部署前就存在”。
  - `install` 始终把 `ReShade.ini` 纳入计划：`[GENERAL] EffectSearchPaths/TextureSearchPaths` 缺失时追加 pack 搜索路径，已有键、注释和 `[Uplift]` 等设置原样保留；部署前已有的 INI 会先备份，卸载时恢复原文。
  - `set_hotkey` 写入 `[INPUT] KeyOverlay=<VK>,0,0,0`，并把 `ReShade.ini` 在清单里的哈希/大小同步更新，避免热键修改被误判为冲突；没有清单时返回 `ManifestMissing`。游戏正在运行或 INI 被占用时写入 `state/hotkey-queue.json`（同一游戏只保留最后一次），返回“已排队”；后台每 30 秒重试，游戏退出后补写，成功后移出队列。
  - `uninstall` 走 `DeploymentEngine::restore`：哈希与清单一致的文件被删除（有备份的恢复备份），用户改过的文件报告为冲突并保留。**不运行 ReShade 官方卸载器**——官方卸载器按文件名删除，正是旧版误删第三方 `dxgi.dll` 的原因。
  - `inspect` 返回部署状态、清单版本、代理槽（安装在清单 options 里记录 `proxySlot`）、ReShade 是否在位，以及根目录旧版 RenoDX 残留（`renodx-dlss*`、覆盖旧布局的 `nvngx_dlssnr.dll`）。ReShade 判定优先认清单里的代理记录，其次用 PE `ProductName` 探测。
- `fgm-ffi/src/commands_dlss5.rs`：`fgm_dlss5_install_json`、`fgm_dlss5_inspect_json`、`fgm_dlss5_set_hotkey_json`、`fgm_dlss5_uninstall_json`；卸载返回 `restored`/`removed`/`conflicts`/`warnings`；inspect 在没有显式 pack 时静默尝试本地 `dlss5.reshade-gitc`，缺 pack 也照样报告清单状态；inspect 同时返回当前 `hotkeyVk`。
- `fgm-cli/src/dlss5.rs`：`dlss5 install|inspect|uninstall|hotkey`；`install` 未给 `--api` 时用 `GraphicsApiDetector` 自动判定。
- Electron：DLSS5 页每局游戏显示状态标签（未安装/已安装/需更新/不完整/冲突/无清单）、ReShade 标签、旧版残留提示，并提供安装、更新、快捷键、卸载按钮；操作后自动刷新状态。快捷键按钮打开录制弹窗（Esc 取消），把 USB HID 映射为 Windows 虚拟键后调用 `fgm_dlss5_set_hotkey_json`，成功后刷新显示当前键；返回“已排队”时提示游戏退出后自动写入。主界面每 30 秒调用 `fgm_dlss5_process_hotkey_queue_json` 重试队列。卸载有冲突时按数量提示，不误报成功。
- 旧版残留：`Dlss5Service::clean_legacy` 删除根目录 `renodx-dlss*` 与旧布局 `nvngx_dlssnr.dll`（只删这两类，其他文件不动）；存在残留时 `install` 返回 `Conflict` 并要求先清理，界面在残留标签旁显示“清理旧版文件”按钮与确认框。
- Vulkan layer 安装/卸载已实测：安装登记 `C:\ProgramData\ReShade` 与 `HKLM\SOFTWARE\Khronos\Vulkan\ImplicitLayers`；卸载用 `<game.exe> --headless --elevated --api vulkan --state uninstall` 并清理 `ReShade.ini`/`.log`/`Preset`，随后删除 114 个 pack 文件，只留下游戏 EXE。清单记录 `api`/`packDir`/`gameExe` 供卸载使用。
- 尚未迁移：GITC 选项/INI 的其余项（`StartEnabled` 默认值切换）。
- 测试：`reshade_zip.rs` 内 stored/deflate 提取测试；`tests/reshade_installer.rs` 安装与冲突测试；`tests/dlss5_service.rs` 覆盖安装、代理冲突回滚、卸载删除代理/INI、用户修改文件保留、部署前已有 `ReShade.ini` 保留、inspect 状态/代理/ReShade/旧版残留。

## 前置条件

- 从游戏渲染 EXE 探测图形 API；映射到 ReShade 参数：
  - DX9 → `d3d9`；DX10 → `d3d10`；DX11 → `d3d11`；DX12 → `d3d12`；Vulkan → `vulkan`；OpenGL → `opengl`。
- API 无法识别时拒绝安装。
- GPU 不是 SM75/SM86 时由 pack 的 `gpuRoutes` 在后续 UI 层提示；当前 CLI 不做 GPU 闸门。

## 安装流程

目标与 API 的约定：

- 部署目标来自界面的 EXE 选择：默认用扫描结果，也可以“手动选择游戏实际启动 EXE”。手动选择会同时更新该游戏的部署目标目录（与帧生成页一致），安装文件与 ReShade 都写到该 EXE 所在目录；即使扫描阶段没有找到渲染 EXE，手动选择后也允许安装。
- 图形 API 由扫描阶段自动判定，详情页把它作为默认选项并在后面标注“（自动）”；不再提供单独的“自动”行。用户没有改过该选项时按自动模式安装：DX10/11/12 统一使用 `dxgi.dll`（与上游分析器和旧项目一致）。用户手动改成其他 API 时按所选 API 安装，例如显式 DX12 使用 `d3d12.dll`。
- 2.0.7 之前自动模式会误写 `d3d12.dll`。升级后旧部署需要先“卸载”再重新部署，才能切到 `dxgi.dll`；只重新部署不会自动删除旧代理。
- Unity 游戏按 Unity 的 Windows 默认值判定为 D3D11；只有目录里带 `D3D12Core.dll`（D3D12 构建）或 `vulkan-1.dll`（Vulkan 构建）时才判为对应 API。ReShade 自己的代理 DLL（版本信息 ProductName=ReShade）不参与判定，否则装过 ReShade 后重新扫描会把 API 判错。

1. 校验目标目录、pack schema、安装器 SHA-256。
2. 通过统一部署引擎部署 pack 中的 114 个文件（当前 pack `6.8.2`，GITC Uplift `2.0.0`）：
   - `gitc-uplift.addon64`、`gitc-uplift.addon32`、`gitc-uplift-helper64.exe`
   - `GITC-Uplift/nvngx_dlssnr.dll`（534 MB 模型，2.0.0 未更换，仍由用户自备/沿用）
   - `reshade-shaders/Shaders/...`、`reshade-shaders/Textures/...`
   - `Uplift.fx`、`UpliftMask.fx`（随 2.0.0 更新）
   - GITC 2.0.0 起 NR 每次启动默认关闭，需在 ReShade 面板或热键开启；要恢复旧行为，在 `ReShade.ini` 的 `[Uplift]` 写 `StartEnabled=1`。pack 版本升到 6.8.2，已部署用户会检测到资产更新。
3. 非 Vulkan 优先离线安装 ReShade（不联网、不运行 setup）：
   - 从 `ReShade_Setup_6.8.0_Addon.exe` 的 512 字节步进内嵌 ZIP 取出 `ReShade64.dll`（32 位游戏取 `ReShade32.dll`）。该 DLL 与官方 setup 写出的文件哈希一致。
   - 按最终代理写成代理文件名：自动模式下 DX10/11/12 使用 `dxgi.dll`，DX9 使用 `d3d9.dll`，OpenGL 使用 `opengl32.dll`；UI 不出现独立“自动”项，默认项后标“（自动）”。用户可以手动覆盖为 `dxgi/d3d9/d3d10/d3d11/d3d12/opengl32.dll`，但必须与游戏 API 兼容，否则返回 `InvalidArgument`。目标位置已有非 ReShade 文件时拒绝覆盖并回滚（`Conflict`）。
   - 详情页用“录制快捷键”而不是预设列表：默认显示 Home（VK 36），点击后按下自定义键，Esc 取消。安装成功后写 `ReShade.ini` 的 `[INPUT] KeyOverlay=<VK>,0,0,0`；已部署游戏修改时通过 `dlss5.setHotkey` 立即写入，不需要重新部署；已有配置只覆盖该键，保留其他设置。游戏正在运行或 `ReShade.ini` 被锁时，写入进入 `state/hotkey-queue.json`，后台每 30 秒重试，游戏退出后自动补写；详情页“应用到已安装游戏”会把当前快捷键同步到所有已部署游戏。
   - REFramework 不再在 DLSS5 详情页逐个询问：关于页“RE 引擎”卡片提供“RE 引擎自动安装 REFramework”开关，默认开启；检测到 RE Engine 时部署帧生成或 DLSS5 都会自动安装。REFramework 使用独立 `reframework` pack（`dinput8.dll`），占用 `dinput8.dll` 入口，帧生成必须避开。详见 `docs/behavior/reframework.md`。
   - 目标没有 `ReShade.ini` 时写入有效默认配置；随后由配置服务把搜索路径合并为 `.\reshade-shaders\Shaders\**` / `Textures\**`。
   - 只有离线解包失败时才自动回退到官方 setup（`--headless --api <api>`，超时 240 秒）；界面不提供手动选择官方安装器的入口。
   - Vulkan 需要全局层，无法离线落地，只能使用官方 setup。
   - 标准 ReShade shader/纹理和 GITC 指定的 fx 都在 pack 的 114 个文件里，随部署一起写入。
   - 离线路径实测完整部署约 1–2 秒；官方 setup 的联网阶段约 60–100 秒，正常安装不经过它。
4. 失败时回滚已部署文件。
5. 界面在已部署（`Installed`）时锁定安装按钮；`Outdated`/`Partial`/`Conflict` 显示“更新”。`InstalledWithoutManifest`（文件存在但清单丢失）显示“重新部署”，不永久锁死。详情页提供“打开文件夹”。
6. 安装/卸载期间详情页显示实时进度条：文件写入按数量推进，ReShade 模块阶段显示不确定进度。操作成功或失败后都关闭全局进度卡片；ReShade 阶段使用不确定进度时不会再残留“正在安装 ReShade”。

安装前会检查帧生成是否已占用同一个代理文件（例如帧生成使用 `dxgi.dll`、DLSS5 选择 DXGI 代理）：占用时返回 `Conflict`，避免覆盖帧生成的代理入口。

部署前还会做 ReShade 入口检查：

- GITC pack 同时包含 `gitc-uplift.addon32` 和 `gitc-uplift.addon64`，ReShade 离线安装会按游戏 EXE 选择 `ReShade32.dll` / `ReShade64.dll`，因此 32 位游戏可以正常部署。
- 目录里已有多个 ReShade 代理入口，或已有入口位数与游戏不匹配时返回 `Conflict`，提示用户先清理旧的 ReShade 入口。

## 状态检查

- pack 文件状态来自部署引擎（未部署/已部署/需更新/不完整/冲突）。
- 清单缺失但目录里仍有与当前 pack 哈希一致的文件时，状态为 `InstalledWithoutManifest`；界面允许“重新部署”或“卸载”，不会永久锁死。
- 通过文件版本信息识别 ReShade 模块（`d3d9/d3d10/d3d11/d3d12/dxgi/opengl32.dll`，ProductName/FileDescription 含 `ReShade`）仅用于展示和状态判断；卸载删除时必须再通过“与 bundled setup 内 ReShade 模块 SHA-256 完全一致”校验，避免误删雪月等同样基于 ReShade 的第三方 DLL。
- 同时报告旧版 DLSS5/RenoDX 残留：根目录 `renodx-dlss*.addon*`（含第三方整合包常见的 `renodx-dlss-SF-*.addon64`）或旧布局根目录 `nvngx_dlssnr.dll`；命中项写入 `legacyFiles`，界面显示数量并可在悬浮提示中逐项查看，避免用户误以为是本软件部署过 ReShade。
- 检测到旧版 DLSS5/RenoDX 时拒绝安装 GITC pack（`Conflict`，上下文带 `files`），必须先执行“清理旧版文件”。这是按目录里的实际文件判断，不依赖本软件的部署记录。

## 还原 / 卸载（Rust 实现）

提供两个可安全重复执行的操作：

- **还原（保留 ReShade）**：`Dlss5Service::restore_keeping_reshade` → `DeploymentEngine::restore_keeping`（CLI `dlss5 restore`，FFI `fgm_dlss5_restore_json`，界面“还原（保留 ReShade）”）。只还原 role 不是 `proxy` / `config` 的文件，代理 DLL 与 `ReShade.ini` 原样保留；清单转为 `restored`，状态检查显示“未部署”且仍报告 ReShade 在位，之后可以直接重新部署或彻底卸载。
- **卸载**：`Dlss5Service::uninstall`（CLI `dlss5 uninstall`，FFI `fgm_dlss5_uninstall_json`，界面“卸载 DLSS5”），按清单移除包括代理与 INI 在内的全部记录。

1. 读取 `dlss5` 家族清单；没有清单返回 `ManifestMissing`，不猜测目录内容。
2. 逐条比对当前文件哈希与清单记录：
   - 一致且部署前不存在 → 删除（`removed`）。
   - 一致且部署前存在 → 从备份恢复原文件（`restored`）。
   - 不一致（用户或第三方程序改过）→ 记入 `conflicts`，**保留文件**。
   - 文件已不存在 → 只记 warning。
3. 代理 DLL 与默认 `ReShade.ini` 同在清单里，因此：
   - 我们写入的 ReShade 代理会被删除；部署前就存在的 ReShade 代理会被恢复。
   - 如果用户把代理换成了雪月等基于 ReShade 的第三方 DLL，哈希不匹配 → 保留并报告冲突，绝不按文件名删除。
4. 没有冲突时删除清单、备份目录和 pack 声明的空目录；有冲突时清单转为 `partial`，保留剩余记录供下次重试。
5. 不调用 ReShade 官方卸载器，也不删除未跟踪的 `ReShade.log` / `ReShadePreset.ini`。这两点是对旧版行为的有意修正：官方卸载器按文件名删除 DLL，曾导致误删第三方 `dxgi.dll`。
6. 用户自己后续重新安装的 ReShade 配置不在清单内，不会被删除；需要清理时由用户手动处理。

与旧版的差异：

- 旧版“彻底卸载”会运行官方卸载器并清理 `ReShade.log`、`ReShadePreset.ini` 与运行时残渣；新版依赖清单 + 哈希，不删除未跟踪文件（只提供旧版 RenoDX 的一键清理）。
- 重新部署时，当前代理 DLL 的哈希与新模块相同（或版本信息含 ReShade）即视为自己的文件；换成雪月等第三方 DLL 仍返回 `Conflict`。

## 旧版“彻底卸载”参考

保留旧版规则作为后续补齐的对照。Rust 目前只实现旧版 RenoDX 残留的一键清理；以下官方卸载器与未跟踪残留步骤仍未实现：卸载前记录 `ExistedBefore=true` 的路径；从 bundled setup 提取 ReShade 模块并做 SHA-256 校验；只有“版本信息含 ReShade 且哈希一致”的代理才运行官方卸载器；官方卸载器失败或被跳过后删除仍通过哈希校验的模块，哈希不一致的一律保留；还原清单文件；删除 `ReShade.ini`/`ReShade.log`/`ReShadePreset.ini`；按 pack 文件表清理 GITC 组件、shader/纹理残留（哈希匹配才删）；清理旧版 RenoDX（`renodx-dlss*.addon*`、根目录 `nvngx_dlssnr.dll`）与运行时残渣；仅在 `D3D12\` 只有 Microsoft 版 `D3D12Core.dll` 时删除该文件；最后返回 `removed`/`remaining`/`clean`，`clean=false` 时界面显示失败而不是成功。

## 命令

```powershell
fgm-next dlss5 install <target-dir> <game-exe> [--api <api>] [--pack-dir <dir>] [--data-dir <dir>]
fgm-next dlss5 inspect <target-dir> [--pack-dir <dir>] [--data-dir <dir>]
fgm-next dlss5 restore <target-dir> [--data-dir <dir>]
fgm-next dlss5 uninstall <target-dir> [--data-dir <dir>]
```

## 真机验证（2026-10-29）

用 VTube Studio 的临时副本（UnityPlayer 提供 DX12 证据）：

- `inspect`：`NotDeployed`，`Direct3D12`，`reShadePresent=false`。
- `install`：114 个文件写入，ReShade 安装器输出 `Successfully installed ReShade.`，`reShadePresent=true`。
- `restore`：移除 114 个 pack 文件，`conflicts=0`；ReShade 模块/INI/日志按预期保留。
- `uninstall` 夹具：识别旧版 RenoDX 并阻止安装；卸载删除 ReShade 模块/INI/日志、GITC 组件、shader、旧版 addon、运行时残渣，剩余 0。

## 已知限制

- 官方 ReShade setup 每次都会联网下载兼容性数据，耗时约 60–100 秒；常规安装不经过它，只有 Vulkan 或离线解包失败时使用。
- Vulkan 依赖全局 ReShade 层，只能联网运行官方 setup。
- 官方 setup 的界面与退出码受 ReShade 上游控制；超时按失败处理并回滚。
