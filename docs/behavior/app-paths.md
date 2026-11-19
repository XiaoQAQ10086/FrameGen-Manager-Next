# 应用数据目录（AppPaths）

## 目标

所有可写路径只从 `AppPaths` 派生；业务代码不拼绝对路径，也不读取环境变量。

Rust 实现：`rust/crates/fgm-infra/src/paths/app_paths.rs`。

## 解析顺序

| 优先级 | 条件 | 根目录 | portable |
|---|---|---|---|
| 1 | `FGM_DATA_DIR` 非空 | 该路径 | false |
| 2 | 程序目录存在 `portable.flag` | `<程序目录>\userdata` | true |
| 3 | 默认 | `%LOCALAPPDATA%\FrameGen-Manager-Next` | false |

- 程序目录默认取当前 exe 的父目录；测试或 CLI 可显式传入。
- `FGM_DATA_DIR` 用于测试和高级用户；为空字符串视为未设置。
- 根目录统一转为绝对路径；空根返回 `ErrorCode.InvalidPath`。
- `LOCALAPPDATA` 缺失时返回 `InvalidPath`，不静默落到当前目录。

## 子目录

| 方法 | 路径 |
|---|---|
| `logs()` | `<root>\logs` |
| `config()` | `<root>\config` |
| `library()` | `<root>\library` |
| `packs()` | `<root>\packs` |
| `backups()` | `<root>\backups` |
| `cache()` | `<root>\cache` |
| `state()` | `<root>\state` |

`config/window.json` 保存主窗口尺寸（`width/height/maximized`），由 `WindowStateService` 读写；窗口位置由 Windows 决定；不在其中保存其他业务状态。

`update_staging()` 固定在 `%TEMP%\FrameGen-Manager-Next\updates`，不进入数据目录，因为安装包是一次性文件。

## 创建

`ensure_created()` 创建根目录和全部子目录。权限不足返回 `ErrorCode.AccessDenied`，其他 IO 错误返回 `Internal`；不吞掉错误。

## 不做 / 已知限制

- 不做旧版数据迁移；新目录与 2.x 可并存。
- 不读取便携版注册表标记；只认 `portable.flag`。
- 便携版状态放在 `userdata`，与 Flutter 引擎的 `data/flutter_assets` 分开；旧版把状态写在 `data` 下，启动时一次性把 `config`/`library`/`logs`/`state`/`cache`/`backups` 迁移到 `userdata`，引擎资源不动。
- 不自动清理旧目录。

## 测试

- 单元测试在 `app_paths.rs` 内：覆盖 override、portable marker、LocalAppData 默认、目录创建，以及旧 `data` 状态迁移到 `userdata`。
