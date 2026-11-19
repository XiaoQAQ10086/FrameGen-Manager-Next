# Flutter 外壳

本规格描述 Rust + Flutter 版本的外壳。旧版 [app-shell.md](app-shell.md) 仍是 2.x 的行为对照；本文件只覆盖新壳已实现的部分。

## 范围

- 顶栏导航：帧生成、平滑运动、DLSS5、ANR、显卡、日志、设置、关于。
- 帧生成页：核心状态卡 + 游戏库（扫描、部署、还原）；不再显示迁移进度占位。
- 平滑运动页：每局游戏开关、组件部署状态、驱动配置结果。
- DLSS5 页：部署状态、ReShade/旧版残留标签、安装/更新/卸载。
- ANR 页：导入 zip、在线下载最新包、部署/还原。
- 显卡页：驱动版本、Smooth Motion 支持与路由。
- 日志页：操作日志尾部与复制；设置页：语言、自动扫描、封面、REFramework。
- 关于页：版本信息 + 更新检查/下载/安装（安装版）与发布页入口。
- 底部状态栏：核心连接状态、平台标识。
- 中英文运行时切换；所有文案来自 ARB。
- Rust 核心通过 `fgm_ffi.dll` 加载，不启动 sidecar、不开本地端口。

## 启动与 FFI

| 编号 | 条件 | 行为 | 测试 |
|---|---|---|---|
| SHELL-001 | 设置了 `FGM_FFI_DLL` | 优先加载该路径 | `tool/ffi_smoke.dart`（门禁） |
| SHELL-002 | 未设置环境变量 | 依次尝试 exe 目录、开发目录下的 `rust/target/{release,debug}/fgm_ffi.dll` | 手动/门禁 |
| SHELL-003 | 所有候选都失败 | 壳正常启动，状态栏和核心卡显示未加载，不抛异常 | `app_smoke_test.dart` |
| SHELL-004 | 加载成功 | 显示 `fgm_version()` 返回的版本 | `ffi_smoke.dart` |
| SHELL-005 | 查询未知部署阶段 | FFI 返回空指针，Dart 侧返回 `null` | `ffi_smoke.dart` |
| SHELL-006 | 打开帧生成页 | 读取缓存游戏库并显示游戏行；不自动扫描 | `app_smoke_test.dart` |
| SHELL-007 | 点击“扫描游戏” | 在 isolate 中调用 `fgm_library_scan_json`，完成后刷新列表 | 手动/真机 |
| SHELL-008 | FFI 返回错误信封 | 卡片显示本地化错误 + 错误码，不崩溃 | 手动 |
| SHELL-009 | 点击游戏“部署” | 确认框 → isolate 调用 `fgm_deploy_json` → 完成/失败 SnackBar | 手动/真机 |
| SHELL-010 | 点击游戏“还原” | isolate 调用 `fgm_restore_json`；有冲突时提示 | 手动/真机 |
| SHELL-011 | 自动定位 pack | `FGM_PACKS_DIR` → exe/packs → 数据目录/packs → 仓库 packs → assets/packs | `PackLocator` 单测 |

FFI 只暴露稳定键和一次性 JSON DTO，不返回用户文案（见 ADR-0014）：

- `fgm_version() -> *const c_char`
- `fgm_operation_phase_name(u32) -> *const c_char`
- `fgm_library_scan_json() -> *mut c_char`
- `fgm_library_list_json() -> *mut c_char`
- `fgm_deploy_json(*const u8, usize) -> *mut c_char`
- `fgm_restore_json(*const u8, usize) -> *mut c_char`
- `fgm_inspect_json(*const u8, usize) -> *mut c_char`
- `fgm_sm_inspect_many_json(*const u8, usize) -> *mut c_char`
- `fgm_sm_enable_json(*const u8, usize) -> *mut c_char`
- `fgm_sm_disable_json(*const u8, usize) -> *mut c_char`
- `fgm_sm_restore_json(*const u8, usize) -> *mut c_char`
- `fgm_settings_get_json() -> *mut c_char`
- `fgm_settings_set_json(*const u8, usize) -> *mut c_char`
- `fgm_gpu_json() -> *mut c_char`
- `fgm_logs_tail_json(*const u8, usize) -> *mut c_char`
- `fgm_dlss5_install_json` / `fgm_dlss5_inspect_json` / `fgm_dlss5_set_hotkey_json` / `fgm_dlss5_uninstall_json`
- `fgm_anr_current_json` / `fgm_anr_import_json` / `fgm_anr_download_json` / `fgm_anr_deploy_json` / `fgm_anr_restore_json`
- `fgm_update_check_json` / `fgm_update_download_json` / `fgm_update_install_json` / `fgm_update_open_json`
- `fgm_alloc(usize) -> *mut u8` / `fgm_free_bytes(*mut u8, usize)`
- `fgm_string_free(*mut c_char)`
- 阶段键：`planned`、`backingUp`、`writing`、`verifying`、`installed`、`rolledBack`、`partial`。
- 游戏库信封：`{"ok":true,"games":[...]}` 或 `{"ok":false,"code":"...","context":[...]}`。
- 部署/还原信封包含 `state`、文件计数、`warnings`/`conflicts`。

## 本地化

- 源文件：`ui/lib/l10n/app_en.arb`、`app_zh.arb`。
- 生成文件不提交。
- Rust Core/Domain 不返回中文或英文句子；错误只返回 `ErrorCode` 和结构化字段。

## 布局与动效

- 默认窗口 1280×850，标题 `FrameGen Manager-Next`。
- 顶栏高度 72，导航在空间不足时横向滚动。
- 页面切换使用 180ms 淡入 + 2% 垂直位移。
- 选中导航使用浅蓝胶囊；不使用持续动画或高开销模糊层。
- 宽度低于 900 时，帧生成页改为纵向滚动布局。

## 构建集成

- `windows/runner/CMakeLists.txt` 的 POST_BUILD 把 `rust/target/{debug,release}/fgm_ffi.dll` 复制到 exe 目录。
- 核心尚未构建时只告警，不阻塞 `flutter run`。
- 完整门禁：`build/verify-rust-flutter.ps1`。

## 不做 / 已知限制

- 手动添加游戏、封面加载、启动游戏、跨模式状态徽章尚未接入。
- 不保存窗口尺寸和位置（对等阶段补上）。
- DLSS5 快捷键录制界面、更新下载进度/取消、启动时静默更新检查尚未接入。
- 不使用第三方状态管理、HTTP 或数据库包。
