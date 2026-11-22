# 操作日志与反馈

- 顶部导航为“日志与反馈”，导出按钮为“导出日志包”；页面内提供问题/建议反馈卡片，详见 `feedback.md`。

Rust 实现：`fgm-infra/src/logging/operation_log.rs`。

## 位置与格式

- 路径：`<数据目录>\logs\fgm.log`。
- 追加写入，每行一个 JSON 对象：
  ```json
  {"ts":1700000000000,"op":1700000000000,"level":"info","message":"deploy","fields":{"family":"framegen","packId":"framegen.sm86"}}
  ```
- `message` 和 `fields` 只放英文结构化诊断；不写用户文案、不写敏感信息（令牌、完整用户目录可选）。
- `op` 使用操作开始/结束的 Unix 毫秒；后续 `OperationRunner` 接管后改为稳定 operation id。

## 读取

- `tail(maxLines)` 只从文件尾部读取最多 256 KB，丢弃截断的首行后再取最后 N 行；文件不存在返回空。
- 日志达到 5 MB 时轮转为 `fgm.log.1`，保留 3 份历史文件；当前文件继续追加。
- 最多返回 5000 行，避免一次拉取过大。
- UI 日志页显示等宽文本，支持复制和刷新。

## 错误

| 场景 | ErrorCode |
|---|---|
| 日志目录无法创建/写入 | `AccessDenied` / `Internal` |
| 读取失败 | `AccessDenied` / `Internal` |
| 文件不存在 | 返回空列表，不报错 |

## 尚未完成

- 诊断包打包。
- 所有 Core 操作统一带 operation id（目前 FFI 层按时间戳记录关键操作）。

- 启动时写入 `app start`（版本、便携标记、数据目录、Windows 版本）、`gpu detected`（名称/驱动/路由/HAGS）、`settings loaded`、`library cache loaded`；日志页把这些 JSON 行格式化为 `时间 级别 消息 字段=值`。
- 已接入事件：`library scan`、`deploy`、`restore`、`sm enable/disable/restore`、`dlss5 install`、`anr deploy/restore`；每条包含 operation id、级别、英文消息和结构化字段。
