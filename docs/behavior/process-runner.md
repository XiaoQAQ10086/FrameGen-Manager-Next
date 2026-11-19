# 进程运行器（ProcessRunner）

Rust 实现：`rust/crates/fgm-infra/src/process/process_runner.rs`。

用于运行 ReShade 安装器、Elevated helper 等受控子进程。

## 输入

- `executable`：绝对或 PATH 可解析的可执行文件。
- `arguments`：参数数组，不经过 shell，不拼接命令行字符串。
- `working_directory`：可选；不传则继承当前进程目录。
- `timeout`：总运行时限。
- `cancelled`：`AtomicBool`，调用方置位后尽快停止。

## 规则

| 编号 | 条件 | 行为 |
|---|---|---|
| PROC-001 | 启动前已取消 | 不启动进程，返回 `exitCode=-1`、`timedOut=true` |
| PROC-002 | 正常启动 | `stdin=null`、stdout/stderr 管道捕获、Windows 加 `CREATE_NO_WINDOW` |
| PROC-003 | 子进程退出 | 返回真实 exit code，读取剩余 stdout/stderr |
| PROC-004 | 达到 timeout | 终止直接子进程，返回 `exitCode=-1`、`timedOut=true` |
| PROC-005 | 运行中取消 | 同 timeout |
| PROC-006 | 输出不是 UTF-8 | 用 replacement 字符解码，不丢输出、不抛异常 |
| PROC-007 | 可执行文件不存在 | `ErrorCode.FileNotFound` |
| PROC-008 | 权限不足 | `ErrorCode.AccessDenied` |
| PROC-009 | 参数非法 | `ErrorCode.InvalidArgument` |
| PROC-010 | 其他启动/等待错误 | `ErrorCode.Internal` |

## 已知限制

- 当前 `kill()` 只终止直接子进程，不保证终止整个进程树。C# 版使用 `Kill(entireProcessTree: true)`；ReShade 安装器如果自行拉起子进程，后续需要用 Job Object 补上。
- 轮询间隔 25 ms；不追求毫秒级退出通知。
- 不提供 stdin 写入。
- 不解析或重写命令行；调用方必须传参数数组。

## 测试

- `rust/crates/fgm-infra/tests/process_runner.rs`
  - stdout/exit code、stderr、非零退出码
  - 超时终止
  - 启动前取消
  - 可执行文件不存在
