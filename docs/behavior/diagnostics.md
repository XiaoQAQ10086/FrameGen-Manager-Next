# 诊断包导出行为规格

## Rust 迁移状态（Phase 5）

- `fgm-infra/src/diagnostics.rs`：收集 `logs/fgm.log` 末尾最多 4 MiB，加 `summary.json`（版本、平台、游戏数、GPU 名称/驱动/路由、设置项），不包含绝对路径、游戏文件或部署备份。
- `fgm-infra/src/archive/zip_writer.rs`：store 方式的最小 ZIP 写入器（CRC32 + local/central/EOCD），不引入新依赖。
- `fgm-ffi/src/commands_diagnostics.rs`：`fgm_diagnostics_export_json` 先构建内存 ZIP，再打开原生“另存为”对话框；取消返回 `cancelled`，不写文件。
- Flutter 日志页“导出诊断包”按钮：导出中禁用按钮，成功提示最终路径，取消静默。
- 测试：`zip_writer.rs` 单测、`tests/diagnostics.rs`（ZIP 头、日志与摘要、无绝对路径）。

## 输入

- 用户在“日志”页点击“导出诊断包”。
- 诊断包包含当前日志、GPU 摘要和游戏库计数；不包含游戏文件或部署备份。

## 规则

| 编号 | 条件 | 行为 |
|---|---|---|
| DIAG-001 | 点击导出 | 先在工作临时目录收集日志和诊断摘要，压缩为 zip |
| DIAG-002 | 压缩完成 | 打开原生“另存为”对话框，默认文件名为 `fgm-diagnostics-<时间戳>.zip`，过滤器仅 ZIP |
| DIAG-003 | 用户确认路径 | 将临时 zip 复制到用户选择的位置，删除临时 zip，toast 显示最终路径 |
| DIAG-004 | 用户取消 | 不复制文件，删除临时 zip，不显示错误或成功提示 |
| DIAG-005 | 复制失败 | 返回错误码并以 toast 提示；临时 zip 仍然清理 |

## 错误

| 场景 | ErrorCode | 用户可见结果 |
|---|---|---|
| 无权限写入目标位置 | `AccessDenied` | 提示保存失败 |
| 目标文件被占用 | `FileLocked` / `IOException` | 提示保存失败 |

## 不做 / 已知限制

- 不自动覆盖已有文件；`SaveFileDialog.OverwritePrompt` 由系统确认。
- 不做后台定时导出；每次导出都是一次用户操作。
