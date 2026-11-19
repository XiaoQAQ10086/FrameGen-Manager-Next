# 操作日志行为规格

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

- `tail(maxLines)` 读取最后 N 行；文件不存在返回空。
- 最多返回 5000 行，避免一次拉取过大。
- UI 日志页显示等宽文本，支持复制和刷新。

## 错误

| 场景 | ErrorCode |
|---|---|
| 日志目录无法创建/写入 | `AccessDenied` / `Internal` |
| 读取失败 | `AccessDenied` / `Internal` |
| 文件不存在 | 返回空列表，不报错 |

## 尚未完成

- 日志滚动/大小上限。
- 诊断包打包。
- 所有 Core 操作统一带 operation id（目前 FFI 层按时间戳记录关键操作）。
