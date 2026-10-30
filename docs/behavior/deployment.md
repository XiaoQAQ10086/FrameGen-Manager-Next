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

## 部署流程

1. 校验目标：绝对路径、目录存在、不是盘符根/Windows/Program Files 等受保护目录。
2. 通过 `FGM_PACKS_DIR`、`<exe>/packs`、数据目录 `packs` 或仓库 `packs`/`assets/packs` 定位 pack；同名 pack 取版本最高且满足 `minAppVersion` 的一份。
3. 校验 pack schema 和源文件大小/哈希。
4. 选择代理槽（默认 `version.dll`），加入所有非代理文件，生成 INI。
5. 获取跨进程文件锁（`state/locks/<family>-<targetHash>.lock`）。
6. 对每个目标文件：
   - 目标是目录 → `Conflict`。
   - 目标被占用 → `FileLocked`，在任何写入前失败。
   - 目标不存在 → 创建，`ExistedBefore=false`。
   - 目标哈希等于当前 pack 或旧 manifest 记录 → 视为我们的文件，不备份。
   - 目标是外部文件 → 备份到 `backups/<family>/<targetHash>/files/<relative>`，`ExistedBefore=true`。
   - 旧 manifest 已有原始备份 → 复用原始备份，不覆盖。
7. 写入 manifest（`Installing`）→ 临时文件写入 → 原子替换 → 校验哈希。
8. 清理旧 manifest 中不再属于当前 pack 的文件（仅当内容仍是我们的）。
9. 写入 manifest（`Installed`）。
10. 任一步失败：回滚已写文件（恢复备份或删除新建文件），manifest 标记 `RolledBack`。

## 还原流程

1. 获取锁，读取 manifest；没有 manifest → `ManifestMissing`。
2. 对每个记录：
   - 文件仍匹配部署哈希：有原始备份则恢复，否则删除。
   - 文件被修改：保留，计入 `Conflicts`。
   - 文件缺失但存在原始备份：恢复原文件。
   - 备份缺失：警告，不删除当前文件。
3. 删除空目录（不删除非空目录）。
4. 收集遗留文件（例如 `dlssg_sm86/logs/`），最多 20 条。
5. manifest 标记 `Restored`。

## 路径安全

- 所有相对路径走 `SafePath`：拒绝绝对路径、`..`、UNC、ADS、保留设备名。
- 目标目录禁止盘符根、Windows、System32、Program Files、Program Files (x86)。
- 备份路径同样走 `SafePath.CombineUnderRoot`。

## FrameGen 选项

默认值：`version.dll`、`Optimized=1`、`MaxGeneratedFrames=3`（4X）、`Preset=A`、`Engine=DLSS`、`SuperResolution=Off`、XeSS flow `Middle`、HUD-less `Auto`。

| 选项 | 规则 |
|---|---|
| Proxy | 必须是 pack 中 `proxySlot=true` 的文件；只复制选中的一个 |
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

UI：“帧生成”页点击某个游戏的“部署”后才展开代理/优化/帧数/Preset/引擎/SR 参数；确认后部署。其余时间不占用页面空间。

部署弹窗：绿色“部署”按钮；不提供“取消”按钮（右上角关闭或点遮罩退出）；“还原”只有在当前游戏的部署状态可还原、且所选目标目录与部署目录一致时才启用，否则显示灰色锁定。

## 测试

- `Infrastructure.Tests/DeploymentEngineTests.cs`：部署/检查/还原、备份、冲突、缺失、文件锁、无 manifest 识别。
- `Core.Tests/FrameGenIniBuilderTests.cs`：默认值、非默认值、XeSS、帧数钳制、非法值。
- 本机夹具：临时目录部署 8 个文件，备份 2 个原文件，还原后原文件内容恢复、运行时目录移除。

## 已知限制

- 默认代理槽为 `version.dll`；执行部署前可在弹出的部署面板中覆盖。
- 不自动结束游戏进程；检测到文件占用时直接拒绝。
- 不删除 `dlssg_sm86` 下的未知文件（日志/缓存），只在还原报告里列出。
- 不处理符号链接/junction 的最终路径解析；目标目录必须由用户选择，且受保护目录会被拒绝。
