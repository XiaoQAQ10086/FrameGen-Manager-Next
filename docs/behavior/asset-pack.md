# 资产包（Asset Pack）行为规格

目标：上游文件布局变化只改 `pack.json` 和资产，不改程序代码。

实现：

- `Domain/Packs/AssetPack.cs` 及同目录模型
- `Domain/Packs/AssetPackValidator.cs`：纯 schema 校验
- `Infrastructure/Packs/PackJsonSerializer.cs`：严格 JSON 读取/写入
- `Infrastructure/Packs/PackPayloadValidator.cs`：文件存在性、大小、SHA-256
- `Cli/PackValidationCommand.cs`：`fgm-next pack validate`

## 文件布局

```
pack-directory/
  pack.json
  <payload files...>
```

`pack.json` 的 `source` 相对于 pack 目录；`target` 相对于游戏渲染目录。

## 顶层字段

| 字段 | 规则 | 错误码 |
|---|---|---|
| `schemaVersion` | 必须为 1 | `PACK-001` |
| `id` | `^[a-z0-9][a-z0-9._-]{1,63}$` | `PACK-002` |
| `version` | 语义化版本 `x.y.z[-prerelease][+build]` | `PACK-003` |
| `displayName` | 非空，≤120 字符 | `PACK-004` |
| `minAppVersion` | 可选；语义化版本；不能高于当前程序版本 | `PACK-005` / `PACK-020` |
| `gpuRoutes` | 至少一个；取值 `Any/Sm86/Sm75/Gtx16/Pascal/NotNeeded/Unsupported/Unknown`；不重复 | `PACK-006` / `PACK-023` |
| `trust` | `bundled` 或 `verified` | JSON 解析错误 |
| `files` | 至少一个 | `PACK-007` |

## 文件条目

| 字段 | 规则 | 错误码 |
|---|---|---|
| `source` | 安全相对路径 | `PACK-008` |
| `target` | 安全相对路径；大小写不敏感唯一 | `PACK-008` / `PACK-009` |
| `role` | `proxy/runtime/sr/addon/model/shader/effect/texture/preset/installer/config/other` | `PACK-010` |
| `proxySlot` | 只允许 `role=proxy` | `PACK-011` |
| `sha256` | 64 位十六进制 | `PACK-012` |
| `size` | 非负整数 | `PACK-013` |
| `required` | 布尔 | — |

## INI

| 字段 | 规则 | 错误码 |
|---|---|---|
| `target` | 安全相对路径；不能与文件 target 冲突 | `PACK-008` / `PACK-019` |
| `template` | 安全相对路径 | `PACK-008` |
| `mode` | `merge` 或 `replace` | `PACK-014` |
| `templateSha256` | 64 位十六进制 | `PACK-024` |
| `templateSize` | 非负整数 | `PACK-025` |
| `stockValues[]` | section/key/value；section/key 不能重复 | `PACK-015` / `PACK-021` |

INI 合并语义见 [ini.md](ini.md)。

## 步骤

| `type` | 允许参数 | 规则 |
|---|---|---|
| `copyFiles` | 无 | 执行 `files` 列表 |
| `writeIni` | 无 | 执行 `ini` 规则 |
| `removeStaleFiles` | `target` | target 必须是安全相对路径 |
| `runInstaller` | `source`、`sha256`、可选 `arguments` | 只允许 `trust=bundled`；source 安全；sha256 必须 64 位十六进制 |

错误码：`PACK-016`（未知类型）、`PACK-017`（参数/必填项）、`PACK-018`（非 bundled 包执行安装器）。

## 安全边界

- 远程 pack 默认可下载但不可执行安装器；`runInstaller` 只允许随程序分发的 bundled pack。
- 所有 source/target 走 `SafePath`，拒绝绝对路径、`..`、UNC、保留设备名。
- 远程 pack 必须带 SHA-256；下载后再做一次 payload 校验。
- 程序不信任 pack 中的路径，也不自动清理 pack 未声明的文件。

## Payload 校验

| 错误码 | 场景 |
|---|---|
| `PACK-101` | 文件缺失、source 路径非法、pack 无文件 |
| `PACK-102` | 文件大小不匹配 |
| `PACK-103` | SHA-256 不匹配或缺失 |
| `PACK-104` | INI 模板缺失 |
| `PACK-105` | 读取文件时的 IO/权限/加密错误 |
| `PACK-106` | INI 模板大小或 SHA-256 不匹配 |
| `PACK-107` | `runInstaller` 的安装器缺失或 SHA-256 不匹配 |

## 命令

```powershell
fgm-next pack validate <pack-dir>
fgm-next pack validate <pack-dir> --no-hash
```

退出码：0 通过；1 校验失败；2 参数或目录错误。

## 测试

- `tests/FrameGenManagerNext.Domain.Tests/AssetPackValidatorTests.cs`
- `tests/FrameGenManagerNext.Infrastructure.Tests/PackJsonSerializerTests.cs`
- `tests/FrameGenManagerNext.Infrastructure.Tests/PackPayloadValidatorTests.cs`

## 状态

- 已从旧项目生成四个 pack：`framegen.sm86`、`smooth-motion.rtx30`、`smooth-motion.rtx20`、`dlss5.reshade-gitc`。
- 四个 pack 均通过完整 payload 校验（存在性、大小、SHA-256、INI 模板、安装器）。
- 待完成：`packs.json` 版本索引、下载器、离线包、部署引擎步骤执行器（Phase 3）。
- DLSS5 的 ReShade.ini 搜索路径合并属于功能逻辑，暂由 Phase 4 的 DLSS5 模块实现，不放进通用 pack 步骤。
