# 显卡名称伪装行为规格

来源：旧版 `gpu.rs` 的 `enumerate` / `apply` / `restore` / `run_elevated`。
新实现：`Infrastructure/Gpu/GpuSpoofOperations.cs`、`GpuSpoofService.cs`、`FrameGenManagerNext.Elevated`。

## Rust 迁移状态（Phase 4）

- `fgm-infra/src/gpu_spoof/`：直接调用 Advapi32 读写注册表（`RegOpenKeyExW/RegQueryValueExW/RegSetValueExW`），不使用提权助手；`IsUserAnAdmin` 判断权限，非管理员返回 `AccessDenied`。
- 枚举：`Enum\PCI\VEN_10DE*` 实例，保留 `ClassGUID` 为显示适配器且 `Service=nvlddmkm` 的项；从 `Driver` 值取驱动记录名（如 `0002`），再读类键 `DriverDesc`；显示名取 `DriverDesc`，缺失时取 `DeviceDesc` 最后一个分号后的文本。
- 写入：白名单固定 2 个型号（5090/5060），按用户要求收敛可选列表。首次写入前把原始 `DeviceDesc`、驱动名、实例路径、目标名写入 `state/gpu-name/manifest.json` 并读回校验；写入后读回不一致会尝试写回原值并报错。重复应用保留最早的原始值，只更新 `applied`。
- 还原：`driver` 写回 `DriverDesc`（纯名称）；`backup` 使用 manifest 原始 `DeviceDesc` 并删除 manifest；没有备份返回 `ManifestMissing`。
- CLI：`fgm-next gpu spoof inspect|apply|restore`。FFI：`fgm_gpu_spoof_inspect_json` / `fgm_gpu_spoof_apply_json` / `fgm_gpu_spoof_restore_json`。
- Flutter：显卡页“显卡名称伪装”卡片显示适配器、白名单型号、当前伪装与还原按钮；非管理员时禁用并提示以管理员身份重启。
- 真机验证（2026-11-19，RTX 3070）：`inspect` 读取到实例/驱动名/DeviceDesc；应用 5070 Ti 后注册表读回一致；`restore backup` 恢复 `@oem49.inf,%nvidia_dev.2484%;NVIDIA GeForce RTX 3070`；再次应用后 `restore driver` 写入纯名称，随后 `restore backup` 恢复原始值并删除 manifest。

## 目标

少数游戏只按显卡型号决定是否开放帧生成设置。该功能把 NVIDIA 显示适配器的
`HKLM\SYSTEM\CurrentControlSet\Enum\PCI\<VEN_10DE...>\<实例>\DeviceDesc`
改成固定白名单中的 50 系型号。

## 枚举规则

| 编号 | 条件 | 行为 |
|---|---|---|
| SPOOF-001 | 类键 `...\Control\Class\{4d36e968-...}` 下四位数字子键 | 读取 `DriverDesc`，仅保留含 `NVIDIA` 且 `MatchingDeviceId` 以 `PCI\` 开头的项 |
| SPOOF-002 | `Enum\PCI\VEN_10DE*` 实例 | 仅保留 `ClassGUID` 为显示适配器、`Service` 为 `nvlddmkm` 的实例 |
| SPOOF-003 | 实例 `Driver` 值形如 `{...}\0000` | 用最后一段匹配类键子键，得到驱动记录名称 |
| SPOOF-004 | `DeviceDesc` 为 `@oem.inf,%...%;显示名` | 显示名取最后一个分号后的文本 |

## 写入规则

| 编号 | 条件 | 行为 |
|---|---|---|
| SPOOF-010 | 目标名称不在白名单 | 返回 `InvalidArgument`，不写注册表 |
| SPOOF-011 | 首次修改 | 把 `DeviceDesc` 原始值、驱动名、时间写入 `state/gpu-name/manifest.json` |
| SPOOF-012 | 已存在备份 | 保留最早的原始值，只更新 `applied` |
| SPOOF-013 | 备份写入后读回失败 | 返回 `BackupCorrupt`，不写注册表 |
| SPOOF-014 | 写 `DeviceDesc` 后读回不一致 | 尝试写回旧值，返回 `GpuSpoofFailed` |
| SPOOF-015 | 只修改 `DeviceDesc` | 不修改 `HardwareID`、`CompatibleIDs`、`Driver`、`Service`、`Mfg` 或类键 `DriverDesc` |
| SPOOF-016 | 还原为驱动名称 | 写回 `DriverDesc`，彻底去掉伪装 |
| SPOOF-017 | 还原为原始值 | 使用备份中 `ExistedBefore && Original`；缺失时返回 `BackupCorrupt` |

## 提权

- 主程序不是管理员时，启动同目录 `fgm-next-elevated.exe`，`Verb=runas`，只传：
  - `--gpu-apply <preset> <result.json>`
  - `--gpu-restore driver|backup <result.json>`
- 助手不接受其他命令；结果写临时 JSON（`ok`、`code`、`message`），父进程读取后删除。
- 用户取消 UAC（Win32 1223）→ `ElevationCancelled`；助手缺失 → `ElevationRequired`。
- 主程序已经是管理员时直接写入，不再启动助手。

## 已知副作用

- 显卡名与硬件 ID 不一致，内核级反作弊可能判定异常。
- NVIDIA App / 驱动安装程序可能识别错误型号。
- 重装驱动后可能被重置。
- 需要重启才在系统界面生效。
- 只在用户明确确认后执行；原始值在写入前已备份。
- 应用/恢复确认使用应用内模态框，不使用浏览器原生 `confirm()`；应用确认中目标型号以徽章展示，关键警告使用红色，按钮跟随当前界面语言。

## 测试

- `GpuSpoofTests.DisplayNameUsesTextAfterLastSemicolon`
- `GpuSpoofTests.PresetsExposeTwoUiChoicesButKeepFullWhitelist`
- CLI `gpu spoof inspect` 在 RTX 3070 上读取到驱动名、硬件 ID、Enum 键。
