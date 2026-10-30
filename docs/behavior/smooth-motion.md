# Smooth Motion 行为规格

来源：旧版 `sm.rs`、`scan.rs::classify_gpu` 与真机 NVAPI 验证。
新实现：`Core/Features/SmoothMotion`、`Infrastructure/NvApi`、`Infrastructure/SmoothMotion`、四套 SM pack。

## 前置条件

- GPU 路由必须是 `Sm86`（RTX 30 / RTX A）或 `Sm75`（RTX 20 / Quadro RTX）。
- 驱动版本必须 ≥ **616.64**（从 `nvidia-smi` 读取；未知/不可解析直接拒绝）。
- 其他路由：`Gtx16`/`NoTensorCore`/`Unsupported` 返回 `GpuUnsupported`，`NotNeeded` 返回 `GpuNotNeeded`。

## 组件包

| GPU 路由 | Pack | 文件 |
|---|---|---|
| Sm86 | `smooth-motion.rtx30` | `version.dll` |
| Sm75 | `smooth-motion.rtx20` | `version.dll`、`normal.asi`、`runtime-manifest.json` |

组件文件通过统一部署引擎写入游戏目标目录；同名原文件进入备份，记录在 `backups/smooth-motion/<targetHash>/`。

## 状态检查

`sm inspect`：

- 组件状态来自部署引擎（未部署/已部署/需更新/不完整/冲突）。
- 驱动状态来自 NVAPI：找到挂在该 EXE 上的 profile，读取设置 `0xB0D384C0`。
  - 值 1 → 开启；0 → 关闭；没有该设置 → 未知/跟随全局。
- 找不到 profile → `smEnabled=unknown`，profile 为空。

## 开启

1. 校验路由/驱动/目标目录。
2. 部署对应组件包（备份同名原文件，哈希校验）。
3. 打开 NVAPI 会话：
   - 优先使用 `DRS_FindApplicationByName` 找到该 EXE 已归属的 NVIDIA profile。
   - 找不到才创建/复用 `FrameGen Manager - <game>`，并把 EXE 挂上去；挂不上则失败，不写设置。
   - 拒绝写基础/全局 profile。
4. 第一次修改该游戏时，记录 profile 名、旧值是否存在、旧值内容到 `backups/sm-profiles/<targetHash>.json`。
5. 写入 1，保存，**换新会话读回**；只有读回值等于 1 才算成功。
6. 如果 NVAPI 失败：回滚组件文件，返回 `NvApiCallFailed`。
7. 找不到 `nvapi64.dll` → `NvApiUnavailable`。

## 关闭

- 写入 0 并读回；组件文件保留，可用“还原”删除。
- 不修改旧值记录（后续还原仍能恢复成最初的值）。

## 还原

1. 读取 `sm-profiles` 记录；没有记录则尝试按 EXE 找 profile，找不到就视为无需还原。
2. 有旧值 → 写回旧值；没有旧值 → 删除该设置项（跟随全局）。
3. 换新会话读回复核。
4. 删除记录。
5. 通过部署引擎还原本工具写入的组件文件；用户修改过的文件保留并报告冲突。
6. 本工具自建的 profile 可能保留为空壳，不删除 NVIDIA 预置 profile。

## NVAPI 关键点

- `nvapi64.dll` 通过 `nvapi_QueryInterface` 动态调用，不链接导入库。
- 结构体版本字段：`size | (version << 16)`（低 16 位大小，高 16 位版本）；写反会得到 `-9`（结构体版本不兼容）。
- 设置结构体 12,320 字节；profile 4,116 字节；application V4 20,492 字节。
- 设置名必须填 `PLACEHOLDER`，否则驱动可能拒绝。
- `DRS_SetSetting` 的返回码不可信，必须读回复核。
- 基础 profile 句柄一律拒绝写入。

## 命令

```powershell
fgm-next sm inspect <target-dir> <game-exe>
fgm-next sm enable  <target-dir> <game-exe>
fgm-next sm disable <target-dir> <game-exe>
fgm-next sm restore <target-dir> <game-exe>
```

UI：Smooth Motion 页面，逐游戏“打开文件夹 / 开启 / 关闭”。未开启时只显示“开启”；开启成功后同一按钮变为“关闭”，关闭只写驱动开关为 0，组件文件保留，再次开启直接复用。开启不再弹确认框。还原仍保留在 CLI，供需要彻底清理文件的场景使用。

## 真机验证（2026-10-29）

RTX 3070 / 621.01：

- `sm enable`：组件 `Installed`，NVAPI `smOk=True`，profile `Game.exe`，读回 1。
- `sm disable`：读回 0。
- `sm restore`：组件移除，设置回到 0，记录删除。

## 测试

- `Infrastructure.Tests/SmProfileStoreTests.cs`
- NVAPI 读写为硬件依赖测试，未放入 CI；通过 CLI 在真机验证。

## 已知限制

- 首次为没有 NVIDIA profile 的 EXE 建 profile 后，还原只恢复设置，不主动删除空 profile。
- `nvapi64.dll` 不可用时功能整体不可用；不会回退到注册表。
- 不检查游戏是否正在运行；文件被占用时由部署引擎拒绝。
