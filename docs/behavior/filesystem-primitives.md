# 文件系统原语（DeploymentKey / AtomicFile / TargetLock）

Rust 实现：`rust/crates/fgm-infra/src/fs/`。

## DeploymentKey

| 编号 | 规则 |
|---|---|
| FSKEY-001 | 输入路径先转绝对路径 |
| FSKEY-002 | 分隔符统一为 `\`，转小写 |
| FSKEY-003 | 去掉末尾分隔符；盘符根（如 `C:\`）保留 |
| FSKEY-004 | 对 UTF-8 字节做 SHA-256，取前 16 个十六进制字符，小写 |
| FSKEY-005 | 同一目录不同大小写/斜杠得到同一 key |

用途：备份目录、锁文件名、部署会话键。不要用它判断路径相等；只作为稳定短键。

## AtomicFile

| 编号 | 条件 | 行为 | 错误码 |
|---|---|---|---|
| FSATOM-001 | 目标父目录不存在 | 递归创建 | `AccessDenied` / `Internal` |
| FSATOM-002 | 写入 | 同目录临时文件 `<name>.<pid>.<seq>.tmp`，`create_new` | `FileLocked` / `AccessDenied` |
| FSATOM-003 | 写完 | `flush` + `sync_all` 后 `rename` 覆盖目标 | `FileLocked` / `Internal` |
| FSATOM-004 | 任一步失败 | 删除临时文件，不留下半写文件 | — |
| FSATOM-005 | Windows 共享冲突（错误 32/33） | 映射为 `FileLocked` | `FileLocked` |

不保证目录项持久化（不调用父目录 fsync）；这是 Windows 桌面应用的取舍。

## TargetLock

| 编号 | 条件 | 行为 |
|---|---|---|
| FSLOCK-001 | 锁目录不存在 | 创建 |
| FSLOCK-002 | family 非法路径 | 用 `unknown` 作为前缀 |
| FSLOCK-003 | 锁文件 | `<lock>\<family>-<DeploymentKey>.lock` |
| FSLOCK-004 | 已被其他进程/句柄独占 | `try_acquire` 返回 `Ok(None)`，不阻塞、不抛异常 |
| FSLOCK-005 | 获得锁 | 写入 `pid`、`utc` 诊断信息（best-effort） |
| FSLOCK-006 | Drop | 关闭句柄，锁释放 |
| FSLOCK-007 | 锁目录创建失败 | 返回 `AccessDenied` / `Internal` |

Windows 使用 `share_mode(0)`，与 C# 的 `FileShare.None` 对齐。锁是进程/句柄级，不是命名互斥体。

## 测试

- `rust/crates/fgm-infra/tests/fs_primitives.rs`
  - key 稳定性、不同路径不同 key
  - 原子写创建/覆盖/无临时文件残留
  - 锁互斥、Drop 后重新获取、非法 family 清理
