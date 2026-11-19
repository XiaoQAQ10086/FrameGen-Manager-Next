# 网络下载行为规格

Rust 实现：`fgm-infra/src/net/`（WinHTTP）。

## 支持范围

- `http://` 和 `https://`；HTTPS 使用 Windows 证书存储和 WinHTTP，不引入 TLS crate。
- 绝对 URL；不支持 URL userinfo（`user:pass@host`）。
- 最多 5 次重定向，支持 301/302/303/307/308；`Location` 必须是绝对 URL。

## 下载

| 编号 | 条件 | 行为 |
|---|---|---|
| NET-001 | 默认代理失败 | 自动重试 `NO_PROXY` 一次 |
| NET-002 | 非 200 响应 | 返回 `NetworkError` + `HTTP status N` |
| NET-003 | 重定向缺少 Location | `NetworkError` |
| NET-004 | 下载中取消 | 删除临时文件，返回 `OperationCancelled` |
| NET-005 | 写入 | 先写 `<name>.fgm-download`，`sync_all` 后 rename |
| NET-006 | 读取失败 | 删除临时文件并返回 `NetworkError` |
| NET-007 | 进度 | 每 64 KiB 回调累计字节数 |
| NET-008 | 目标已存在 | rename 覆盖 |

调用方负责下载后的 SHA-256/大小校验；下载器本身不做业务校验。

## 尚未完成

- 断点续传、Range、多镜像回退。
- 下载速度/剩余时间估算。
- GitHub API 解析和更新清单校验（Phase 5）。

## 测试

- `rust/crates/fgm-infra/tests/downloader.rs`：本地 HTTP 服务下载、绝对重定向。
