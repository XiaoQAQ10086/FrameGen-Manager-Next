# 路径安全行为规格

来源：旧版 `crates/core/src/util.rs::safe_join`、sidecar 的 `checked_dir`/`checked_exe`，以及实际遇到的系统目录误操作。

新实现：`src/FrameGenManagerNext.Domain/Safety/SafePath.cs`

## 输入

- `relativePath`：来自 `pack.json`、配置或用户输入，必须是相对于目标根目录的路径。
- `rootDirectory`：部署目标根目录（游戏渲染目录），必须是绝对路径。

## 规则

| 编号 | 条件 | 行为 | 错误码 | 测试 |
|---|---|---|---|---|
| SAFE-001 | 空或全空白 | 拒绝 | `InvalidPath` | `NormalizeRejectsUnsafePaths("")` |
| SAFE-002 | 首尾有空白 | 拒绝 | `InvalidPath` | `NormalizeRejectsUnsafePaths("foo ")` |
| SAFE-003 | 以 `/` 或 `\` 开头、UNC | 拒绝 | `UnsafePath` | `NormalizeRejectsUnsafePaths("\\\\server\\share")` |
| SAFE-004 | 含 `:`（盘符、ADS） | 拒绝 | `UnsafePath` | `NormalizeRejectsUnsafePaths("C:foo")` |
| SAFE-005 | 含 `< > " \| ? *` | 拒绝 | `InvalidPath` | `NormalizeRejectsUnsafePaths("a\|b")` |
| SAFE-006 | 含控制字符 | 拒绝 | `InvalidPath` | `NormalizeRejectsUnsafePaths("line\nbreak")` |
| SAFE-007 | 空段、`.`、`..` | 拒绝 | `UnsafePath` | `NormalizeRejectsUnsafePaths("a/../b")` |
| SAFE-008 | 单段超过 255 字符 | 拒绝 | `InvalidPath` | 见测试 |
| SAFE-009 | 段以空格开头，或以 `.`/空格结尾 | 拒绝 | `InvalidPath` | `NormalizeRejectsUnsafePaths("foo.")` |
| SAFE-010 | Windows 保留设备名（CON、NUL、COM1-9、LPT1-9、CONIN$、CONOUT$，任意扩展名） | 拒绝 | `InvalidPath` | `NormalizeRejectsUnsafePaths("CON.txt")` |
| SAFE-011 | 规范化后总长度超过 240 | 拒绝 | `InvalidPath` | 见测试 |
| SAFE-012 | 与绝对根组合后逃出根目录 | 拒绝 | `UnsafePath` | `CombineUnderRootRejectsTraversal` |
| SAFE-013 | 根目录是相对路径 | 拒绝 | `InvalidPath` | `CombineUnderRootRejectsRelativeRoot` |
| SAFE-014 | 目标是盘符根、Windows、System32、Program Files、Program Files (x86) | `IsProtectedSystemDirectory` 返回 true | — | `IsProtectedSystemDirectoryRejectsWindowsAndDriveRoots` |

## 规范化

- 输入中的 `\` 统一转换为 `/`。
- 输出使用 `/`，便于 pack.json 跨工具阅读；实际文件操作由 `CombineUnderRoot` 转成原生路径。
- 比较大小写不敏感（Windows 语义）。
- 不做 Unicode 归一化。

## 已知限制

- `Path.GetFullPath` 不解析符号链接/junction。部署引擎在 Phase 3 写文件前必须解析最终路径，防止根目录内的 junction 指向外部。
- 不检查 NTFS 权限、ACL、文件是否被占用；这些属于部署引擎的错误模型。
- 不检查路径是否超过 Windows 长路径上限之外的其他限制。

## 测试

- `tests/FrameGenManagerNext.Domain.Tests/SafePathTests.cs`
- `tests/FrameGenManagerNext.Infrastructure.Tests/AppPathsTests.cs`（数据目录/便携模式）
