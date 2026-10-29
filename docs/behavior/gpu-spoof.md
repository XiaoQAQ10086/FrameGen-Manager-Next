# 显卡名称伪装行为规格

来源：旧版 `gpu.rs` 的 `enumerate` / `apply` / `restore` / `run_elevated`。
新实现：`Infrastructure/Gpu/GpuSpoofOperations.cs`、`GpuSpoofService.cs`、`FrameGenManagerNext.Elevated`。

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

## 测试

- `GpuSpoofTests.DisplayNameUsesTextAfterLastSemicolon`
- `GpuSpoofTests.PresetsExposeTwoUiChoicesButKeepFullWhitelist`
- CLI `gpu spoof inspect` 在 RTX 3070 上读取到驱动名、硬件 ID、Enum 键。
