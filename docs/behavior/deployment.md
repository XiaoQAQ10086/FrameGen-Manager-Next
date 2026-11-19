# 部署 / 还原行为规格

来源：旧版 `deploy.rs`、`backup_restore.rs`、`update.rs::prepare_deploy_ini`，以及本机夹具验收。
新实现：`Domain/Deployment`、`Core/Features/Deployment`、`Core/Features/Framegen`、`Infrastructure/Deployment`、`Infrastructure/Packs`。

## 目标

把 FrameGen 资产包安全地写入游戏渲染目录，并能还原部署前的原文件。所有写操作必须可审计、可回滚，且不覆盖用户修改而不报告。

## 状态

| 状态 | 含义 |
|---|---|
| `NotDeployed` | 没有有效 manifest，也没有匹配的部署文件 |
| `Installed` | manifest 存在，所有文件存在且哈希匹配当前 pack |
| `Outdated` | pack 版本/文件哈希与 manifest 不一致 |
| `Partial` | 文件缺失，或上次部署未完成 |
| `Conflict` | 文件被用户或第三方修改 |
| `InstalledWithoutManifest` | 目标目录里有匹配当前 pack 的文件，但没有 manifest |

manifest 状态：`Installing → Installed / RolledBack / Partial`，还原后 `Restored`。`Restored`/`RolledBack` 在检查时视为未部署。

检查时优先使用 manifest 里记录的部署选项（代理槽、优化等级、帧数、Preset、引擎、超分、XeSS），只有没有有效 manifest 时才使用默认值。否则用户改过的选项会被误报成 `Outdated`。

## Rust 迁移状态（Phase 2/3）

已实现 pack 规划 + 文件级部署事务（含 ownedDirectories 与游戏运行检查）：

- `fgm-domain/src/deployment/`：`DeploymentManifest`、`DeployedFileRecord`、`DeploymentManifestState`
- `fgm-infra/src/deployment/backup_store.rs`：manifest 读写与备份目录布局
  - `<backups>\<family>\<DeploymentKey>\manifest.json`
  - family 非法时用 `unknown`；key 由目标绝对路径生成
  - 损坏 manifest 移到 `manifest.json.bad`，按“无 manifest”处理
  - 保存走同目录临时文件 + 原子替换
  - `store_file` / `restore_file` 流式复制并原子替换，不整文件读入内存
  - 备份内相对路径只允许通过 `TargetPath::combine`
- `fgm-infra/src/deployment/pack_planner.rs`：`PackDeploymentPlanner::plan`
  - 先跑 `PackValidator`；schema 不通过直接返回 `PackInvalid`
  - 代理槽：`proxySlot` 选项优先，未指定时取第一个 `role=proxy && proxySlot=true`；其余代理槽不进入部署表
  - 非代理文件全部进入部署表；源路径经过 `TargetPath`
  - `ini.mode=merge`：读模板、校验模板大小/哈希，目标 INI 已存在时在其上合并，否则从模板开始；应用 stockValues + 调用方 overrides
  - 生成 INI 使用 CRLF 和末尾换行，作为内存内容交给部署引擎
- `fgm-infra/src/deployment/deployment_engine.rs`：`DeploymentEngine::apply`
  - 校验目标为绝对路径、存在、非受保护系统目录
  - 可选 `game_exe`：检测到进程运行返回 `GameRunning`，不写任何文件、不等待锁
  - 获取 `TargetLock`；预检所有源文件存在、大小、SHA-256
  - 目标已存在且不是我们的文件才备份；旧 manifest 的有效备份复用
  - manifest 记录 `ownedDirectories`
  - 旧 manifest 中不再部署的代理文件：写新文件前先还原原文件或删除
  - 写入成功后清理其余遗留文件；只有当前哈希仍等于旧部署哈希才删除/还原，被修改的保留并标记 `partial`
  - manifest 状态 `backingUp → writing → installed`
  - 逐文件原子复制并读回 SHA-256；任一步失败回滚已写文件
  - 回滚成功标记 `rolledBack`，回滚自身失败标记 `partial`
- `fgm-infra/src/deployment/deployment_restore.rs`：`DeploymentEngine::restore`
  - 没有 manifest 返回 `ManifestMissing`
  - 只有当前哈希等于部署哈希才还原备份或删除；用户修改过的文件报告冲突并保留
  - 冲突为空时按 manifest 删除 `ownedDirectories`（递归），失败写入 warnings
  - 冲突为空时删除 manifest；否则标记 `partial`
- `fgm-infra/src/deployment/deployment_inspector.rs`：`DeploymentInspector::inspect`
  - 有 manifest：逐文件比对哈希；修改 → `Conflict`，缺失 → `Partial`，pack 版本不同 → `Outdated`，否则 `Installed`
  - 无 manifest：提供 pack 时按 pack 文件表匹配，命中 → `InstalledWithoutManifest`
  - 只读，不修改目标或备份
- `fgm-infra/src/fs/`：`AtomicFile`、`DeploymentKey`、`TargetLock`（见 [filesystem-primitives.md](filesystem-primitives.md)）
- `fgm-infra/src/process/game_process_probe.rs`：部署/还原前的游戏进程检测
  - Toolhelp32 快照按 EXE 文件名匹配；再用 `QueryFullProcessImageNameW` 比对完整路径
  - `OpenProcess` 或查询路径失败时，按“名称匹配即运行”处理，避免受保护进程被误判为未运行
  - 空路径、无法解析的路径返回 false
- 新 manifest 状态使用统一阶段机 `planned/backingUp/writing/verifying/installed/rolledBack/partial`；不兼容旧版 `Installing/Restored` 字段，旧数据不迁移。
- 尚未实现：Smooth Motion/帧生成互斥检查、ReShade/网络、FFI/UI 接线。- 测试：`rust/crates/fgm-infra/tests/backup_store.rs`、`fs_primitives.rs`、`process_probe.rs`、`deployment_engine.rs`、`pack_planner.rs`。

## 部署流程

0. 与 Smooth Motion 互斥：部署前读取目标 EXE 的驱动 profile，`Smooth Motion - Enable` 为 1 时返回 `Conflict`，界面显示红色徽章“冲突：已开启 Smooth Motion”并锁死部署/更新按钮；已部署的帧生成仍可还原/清理，用于解除冲突。
1. 校验目标：绝对路径、目录存在、不是盘符根/Windows/Program Files 等受保护目录。
2. 通过 `FGM_PACKS_DIR`、`<exe>/packs`、数据目录 `packs` 或仓库 `packs`/`assets/packs` 定位 pack；同名 pack 取版本最高且满足 `minAppVersion` 的一份。
3. 校验 pack schema 和源文件大小/哈希。
4. 选择代理槽（默认 `version.dll`）：只把选中的那个代理写到游戏根目录，其余代理留在 pack 里不部署，再加入所有非代理文件并生成 INI。
   - 与上游一致：上游 `docs/INSTALL` 写明“`alternatives\` is not copied normally”，任何时候游戏目录里只放一个本程序代理；备用代理只在游戏不加载 `version.dll` 时逐个替换。
   - 本程序把“换代理”做成交互：在「帧生成」页选另一个代理槽重新部署，**旧槽的代理在写入新文件之前就被移除**（见第 7 步），新槽随后写入根目录，游戏目录里始终只有一个。备用代理仍随 pack 分发（`packs\<packId>\alternatives\`），需要手工替换时可以从那里取，不必重新下载资产。
   - 备用代理不进部署文件表，所以换槽不会留下 `Missing`/多余文件；用户若按上游说明把 `alternatives\` 整个拷进游戏目录，清理时也能按哈希认出这些文件。
   - 部署前若关于页开启“RE 引擎自动安装 REFramework”且检测到 RE Engine，会先安装 `reframework` pack 的 `dinput8.dll`；已有 REFramework 时跳过并弹窗说明。因此不能同时选择 `dinput8.dll` 作为帧生成代理。
   - 部署前检查目标目录里被 DLSS5/ReShade 占用的代理模块（`d3d11.dll`/`d3d12.dll`/`dxgi.dll` 等，按版本信息 ProductName=ReShade 识别）。若用户选中的槽正好被占用，界面会禁用该选项、自动切到其他槽并显示提示；后端也会返回 `Conflict`，避免覆盖 DLSS5 的代理入口。
   - 同时检查 REFramework 的 `dinput8.dll`：存在 REFramework manifest 时禁用/拒绝 `dinput8.dll` 代理槽，避免帧生成覆盖 REFramework。
5. 获取跨进程文件锁（`state/locks/<family>-<targetHash>.lock`）。
6. 对每个目标文件：
   - 目标是目录 → `Conflict`。
   - 目标被占用 → `FileLocked`，在任何写入前失败。
   - 目标不存在 → 创建，`ExistedBefore=false`。
   - 目标哈希等于当前 pack 或旧 manifest 记录 → 视为我们的文件，不备份。
   - 目标是外部文件 → 备份到 `backups/<family>/<targetHash>/files/<relative>`，`ExistedBefore=true`。
   - 旧 manifest 已有原始备份 → 复用原始备份，不覆盖。
7. 写入 manifest（`Installing`），随后移除旧 manifest 里不再部署的代理文件（`role=proxy`，其它角色不动）：上游要求“先删掉旧的代理，再把新的复制进去”，因为同一目录里有两个代理时，被先加载的那个会决定跑哪个版本。旧代理若原本是游戏自带文件（`ExistedBefore`），先把备份还原回去再写新代理。
8. 临时文件写入 → 原子替换 → 校验哈希。
9. 清理旧 manifest 中其余不再属于当前 pack 的文件（仅当内容仍是我们的；第 7 步已移走的代理此时已不存在）。
10. 写入 manifest（`Installed`）。
11. 任一步失败：回滚已写文件（恢复备份或删除新建文件），manifest 标记 `RolledBack`。注意第 7 步删掉的旧代理不会被回滚（我们不再持有它的源文件），此时状态为 `Partial`，重新部署即可。

## 部署前预检与会话

- 部署、还原、清理前按渲染 EXE 检查游戏进程；检测到进程仍在运行直接返回 `GameRunning`，不等待文件锁报错。
- 写入前先对所有 pack 源文件做分块 SHA-256 预检：1 MiB 一块、30 秒无进展超时、逐文件回报进度。任一文件缺失、哈希不符或读取停滞都立即失败，不备份、不写游戏目录。
- 获取目标锁后写 `state/sessions/<family>_<targetHash>.json`：记录阶段、进度和心跳。成功标记 `completed`，失败/中断标记 `failed`；新部署开始时把超过 2 分钟没有心跳的 `running` 会话标记为 `abandoned`，用于诊断“上次卡在哪一步”。

## 还原流程

1. 获取锁，读取 manifest；没有 manifest → `ManifestMissing`。
2. 对每个记录：
   - 文件仍匹配部署哈希：有原始备份则恢复，否则删除。
   - 文件被修改：保留，计入 `Conflicts`。
   - 文件缺失但存在原始备份：恢复原文件。
   - 备份缺失：警告，不删除当前文件。
3. 删除空目录（不删除非空目录）。
4. 删除 pack 的 `ownedDirectories`（`pack.json` 声明，例如 `dlssg_sm86`）：插件运行时会往里写日志和缓存，这些文件不在 manifest 里，旧实现会把它们算成遗留文件、让每次还原都变成 `Partial`。目录下没有任何“被用户修改的部署文件”时才整目录删除；否则保留并警告。
5. 收集剩余遗留文件，最多 20 条。
6. 全部干净（无冲突、无警告、无遗留）才标记 `Restored`；否则 `Partial`，用户可以修复后再次还原。
7. 还原返回 `RestoreReport`；存在冲突/残留/警告时前端必须显示未完成原因（冲突文件保留在游戏目录），不能提示“还原成功”。

## 无记录的部署（清理）

目标目录有我们的文件但没有 manifest（旧工具装的、或记录已丢）时，检查结果为 `InstalledWithoutManifest`。这类部署不能“还原”（没有原件备份），只能清理：

1. 候选文件来自 pack 的完整文件表（所有代理槽 + 运行库），每项带 pack 记录的 SHA-256；未选中代理的 `alternatives\<名>` 也入候选，这样用户按上游说明手工拷进去的备用代理同样能被认出（哈希相等才删）。
2. 如果旧版工具留下的 manifest（`%APPDATA%\FrameGen-Manager\backups\*\manifest.json`，更早的 `%DLSSG-Manager%`）记录的 `game_dir` 与目标一致，只读解析其中的 `deployed_sha256`，把这些旧哈希也加入候选。这样旧资产版本部署的文件也能被确认并删除；旧备份本身不导入、不改动。
3. 同一路径允许多个候选哈希，文件匹配其中任意一个就删除。
4. 只有当前文件哈希仍等于某个候选哈希才删除；被用户替换过的文件保留并计入 `Conflicts`。
5. 至少删掉一个文件后，才删 INI（INI 按用户选项生成，哈希永远对不上 pack 模板）。
6. 再按还原流程第 4 条清扫 `ownedDirectories`。
7. 一个可匹配文件都没有 → `NothingToUninstall`，不写任何内容。
8. 若该目录存在我们自己的部署记录（manifest），拒绝清理并返回 `Conflict`，避免丢掉可还原的备份。

UI 上这类游戏显示黄色徽章：当前 pack 能匹配时是“已部署（无记录）”，来自旧工具 manifest 时是“已部署（旧版工具）”；部署按钮锁定，主按钮为【清理】。

## 路径安全

- 所有相对路径走 `SafePath`：拒绝绝对路径、`..`、UNC、ADS、保留设备名。
- 目标目录禁止盘符根、Windows、System32、Program Files、Program Files (x86)。
- 备份路径同样走 `SafePath.CombineUnderRoot`。

## FrameGen 选项

默认值：`version.dll`、`Optimized=1`、`MaxGeneratedFrames=3`（4X）、`Preset=A`、`Engine=DLSS`、`SuperResolution=Off`、XeSS flow `Middle`、HUD-less `Auto`。

> 与旧版默认值的差异：旧版配置里 `Preset` 默认是 `C`（FG Lite，生成帧 GPU 开销更低），且旧配置升级时会把 `Optimized=1` 迁移为 `2`；新版默认是 `Preset=A`、`Optimized=1`。资产相同但这两项会直接改变帧数，横向对比时必须把游戏目录 `dlssg_sm86.ini` 的 `[DLSSG] Preset/Optimized` 调成一致。

| 选项 | 规则 |
|---|---|
| Proxy | 必须是 pack 中 `proxySlot=true` 的文件；只把选中的那个复制到游戏根目录，其余留在 pack 里不部署（上游：同一目录任何时候只放一个代理） |
| Optimized | 0–3 |
| MaxGeneratedFrames | `1`=2X、`2`=3X、`3`=4X、`5`=6X；`4`（5X）不是上游支持的模式。XeSS 只支持 1–3，>3 自动降为 3 |
| Preset | A/B/C |
| Engine | DLSS/XeSS |
| SuperResolution | Off/M-INT8/L-INT8/M-FP16/L-FP16 |
| XeSS Flow | Coarse/Middle/Fine |
| XeSS HUD-less | Auto/1/0 |

INI 使用 pack 的 stock 值：与 stock 相同的缺失 key 不写入；模板里已有的不同值按选项覆盖；注释和未知行保留。`Optimized` 与 `Preset` 属于 `[DLSSG]`，只在 `Engine=DLSS` 时写入；选择 XeSS 时保留模板 stock 值，改用 `[XeSSFG]` 的 Flow/HUD-less。

## 部署目标

- 部署弹窗默认列出目标目录下的 EXE，并预选自动识别出的渲染 EXE。
- 额外提供“手动选择游戏实际启动 EXE…”；选中后把帧生成文件写入该 EXE 所在目录，而不是旧的目标目录。
- 选择结果写入游戏库的 `targetOverrides[gameId]`，之后的检查、部署、还原和重启都使用纠正后的目录。
- 手动目标必须是存在的 `.exe`，且父目录不能是受保护系统目录；选错时部署引擎仍会拒绝写入。
- 关闭游戏是部署前提；目标 EXE 被占用时整次操作在写入前失败。

## 命令

```powershell
fgm-next deploy inspect <target-dir> [--proxy version.dll]
fgm-next deploy framegen <target-dir> [--proxy version.dll]
fgm-next deploy restore <target-dir>
```

Rust CLI（Phase 3）：

```powershell
fgm-next deploy <pack-dir> <target-dir> --family <id> --proxy-slot version.dll --data-dir <dir> --game-exe <game.exe> --app-version 2.3.4
fgm-next deploy <pack-dir> <target-dir> --dry-run
fgm-next restore <target-dir> --family <id> --data-dir <dir>
fgm-next inspect <target-dir> --family <id> [--pack-dir <dir>] [--data-dir <dir>]
```

- `deploy` 执行 pack 校验、代理槽选择、INI 合并、游戏运行预检、文件事务与 manifest。
- `--game-exe` 指定渲染 EXE；检测到运行中返回 `GameRunning`，不写文件。
- `--dry-run` 只打印计划文件，不读写目标。
- `restore` 按 manifest 还原；存在冲突时退出码为 1。
- `inspect` 只读输出 `NotDeployed/Installed/Outdated/Partial/Conflict/InstalledWithoutManifest` 和明细。
- `--data-dir` 不传时使用 `AppPaths::create_default`（`FGM_DATA_DIR` / `portable.flag` / `%LOCALAPPDATA%`）。

UI：“帧生成”页点击某个游戏的“部署”后才展开代理/优化/帧数/Preset/引擎/SR 参数；确认后部署。其余时间不占用页面空间。

部署弹窗：绿色“部署”按钮；不提供“取消”按钮（右上角关闭或点遮罩退出）；“还原”只有在当前游戏的部署状态可还原、且所选目标目录与部署目录一致时才启用，否则显示灰色锁定。游戏卡片的主动作在部署成功后从“部署”变为“还原”；已部署徽章显示 `已部署 资产版本：<packVersion>`。

显卡检测：帧生成页仍显示当前显卡和驱动，但**不再用 GPU 路由拦截部署**。自资产 0.4.2 起上游支持 RTX 20–50 系（40/50 走 PTX 内核），`NotNeeded` 不再是阻断原因；GTX 16/AMD/Intel 不属于上游支持范围，界面不拦截但运行时不保证生效。Smooth Motion 仍保留自己的显卡门控。资产 0.4.2 的部署文件表与 0.4.1 相同（`version.dll`、`dlssg_sm86.ini`、`dlssg_sm86\`）；5 个备用代理留在 pack 的 `alternatives\` 里，按上游说明只在游戏不加载 `version.dll` 时才换，本程序通过重新部署换槽。旧部署更新时沿用原备份。游戏卡片同时显示 Smooth Motion/ANR 的部署徽章；部署/还原后广播库变更，其他模式页面与重新显示的页面会刷新徽章。

## 库状态徽章（Rust）

- `fgm-infra/src/deployment/deployment_summary.rs`：只读 manifest 计算 `NotDeployed/Installed/Outdated/Partial`，不哈希文件，供游戏卡片批量显示徽章（逐文件哈希在大资产上过慢）。
- `fgm-ffi/src/commands_library.rs::game_modes`：为库中每局游戏返回 `framegen`/`dlss5`/`anr` 三个状态；pack 版本用于判断 `Outdated`。
- Flutter 游戏行在已部署时显示“帧生成/DLSS5/ANR”小徽章；健康为绿色，`Outdated/Partial` 为黄色。Smooth Motion 状态由平滑运动页单独展示，不进入批量徽章。

## 测试

- `Infrastructure.Tests/DeploymentEngineTests.cs`：部署/检查/还原、备份、冲突、缺失、文件锁、无 manifest 识别。
- `Core.Tests/FrameGenIniBuilderTests.cs`：默认值、非默认值、XeSS、帧数钳制、非法值。
- `Core.Tests/FrameGenDeploymentPlanTests.cs`：只部署选中的代理，备用代理留在 pack 里不落盘；`alternatives\<名>` 仍可被识别用于清理。
- 本机夹具：临时目录部署 8 个文件，备份 2 个原文件，还原后原文件内容恢复、运行时目录移除。

## 已知限制

- 默认代理槽为 `version.dll`；执行部署前可在弹出的部署面板中覆盖。
- 不自动结束游戏进程；检测到文件占用时直接拒绝。
- 不删除 `dlssg_sm86` 下的未知文件（日志/缓存），只在还原报告里列出。
- 不处理符号链接/junction 的最终路径解析；目标目录必须由用户选择，且受保护目录会被拒绝。
