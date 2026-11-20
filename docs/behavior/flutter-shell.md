# Flutter 外壳

本规格描述 Rust + Flutter 版本的外壳。旧版 [app-shell.md](app-shell.md) 是 2.x 的行为对照；本文件覆盖新壳已实现的全部页面与交互。

## 范围

- 顶栏：应用图标、名称、版本、标语、7 个导航项（帧生成、平滑运动、DLSS5、ANR、日志、关于、打赏赞助）与语言下拉。
- 帧生成页：左侧显卡 / 顶号 / 资产 / 模式来源面板；右侧游戏库列表头与游戏卡片（部署、启动、打开目录、移除）。
- 平滑运动页：兼容性面板、来源与说明、每局游戏开关与组件状态。
- DLSS5 页：封面墙、详情弹窗（图形 API、代理槽、快捷键）、安装 / 卸载。
- ANR 页：资产包面板（带图标的在线下载 / 导入 zip，百度与夸克网盘为整宽按钮）、来源、要求说明、每局部署与还原。
- 日志页：打开日志目录、清空、导出诊断；可选中的日志正文。
- 关于页：版本与更新检查、GitHub / 许可证入口、启动扫描与 RE Engine 开关、反馈说明。
- 打赏赞助页：微信 / 支付宝二维码与说明，二维码卡片 300px 宽。
- 不再显示旧版迁移阶段徽章、核心连接状态或底部状态栏。
- 错误统一用“操作失败”对话框：本地化文案 + 错误码 + 可选详情 + “复制诊断”（错误码、详情、系统版本、核心版本、最近 80 行日志）。
- 帧生成部署使用左下角非阻塞进度卡（不遮挡页面，可取消）；DLSS5 安装 / ANR 部署与下载 / 更新下载仍使用模态进度。可取消的操作用 `fgm_operation_cancel_json`，取消后按 `OperationCancelled` 静默结束。
- 中英文运行时切换；所有文案来自 ARB。
- Rust 核心通过 `fgm_ffi.dll` 加载，不启动 sidecar、不开本地端口。

## 启动与 FFI

| 编号 | 条件 | 行为 | 测试 |
|---|---|---|---|
| SHELL-001 | 设置了 `FGM_FFI_DLL` | 优先加载该路径 | `tool/ffi_smoke.dart`（门禁） |
| SHELL-002 | 未设置环境变量 | 依次尝试 exe 目录、开发目录下的 `rust/target/{release,debug}/fgm_ffi.dll` | 手动/门禁 |
| SHELL-003 | 所有候选都失败 | 壳正常启动，不抛异常；页面显示空库，不出现“核心未加载”状态条 | `app_smoke_test.dart` |
| SHELL-004 | 加载成功 | 关于页显示 `fgm_version()` 返回的版本 | `ffi_smoke.dart` |
| SHELL-005 | 查询未知部署阶段 | FFI 返回空指针，Dart 侧返回 `null` | `ffi_smoke.dart` |
| SHELL-006 | 打开帧生成页 | 读取缓存游戏库并显示卡片；不自动扫描 | `app_smoke_test.dart` |
| SHELL-007 | 点击“扫描游戏” | 在 isolate 中调用 `fgm_library_scan_json`，完成后刷新列表 | 手动/真机 |
| SHELL-008 | FFI 返回错误信封 | 弹窗显示本地化错误 + 错误码，可复制诊断 | 手动 |
| SHELL-009 | 点击游戏“部署” | 部署弹窗（代理槽 / 引擎 / 帧数 / 优化 / 预设 / 超分）→ 进度 → 完成/失败 | 手动/真机 |
| SHELL-010 | 点击游戏“还原” | isolate 调用 `fgm_restore_json`；用户改过的文件报告冲突 | 手动/真机 |
| SHELL-011 | 自动定位 pack | `FGM_PACKS_DIR` → exe/packs → 数据目录/packs → 仓库 packs → assets/packs | `PackLocator` 单测 |
| SHELL-012 | 打开“已移除游戏” | 显示已移出库的游戏，可“恢复”或“忘记” | 手动 |
| SHELL-013 | 点击“打开显卡设置” | 调用 `fgm_open_hags_json`，仅允许 `ms-settings:` 白名单 | `process_launcher` 单测 |
| SHELL-014 | 切换 GPU 顶号 | 应用后提示重启；`fgm_gpu_spoof_apply_json` / `restore` | 手动/真机 |
| SHELL-015 | 显卡信息为空 | 平滑运动页保留上次成功读取的 GPU，避免误判“无法识别显卡” | 手动 |

FFI 只暴露稳定键和一次性 JSON DTO，不返回用户文案（见 ADR-0014）：

- 基础：`fgm_version`、`fgm_alloc`、`fgm_free_bytes`、`fgm_string_free`、`fgm_operation_phase_name`
- 库：`fgm_library_scan_json`、`fgm_library_list_json`、`fgm_library_removed_json`、`fgm_library_remove_json`、`fgm_library_restore_removed_json`、`fgm_library_forget_json`、`fgm_library_add_manual_json`、`fgm_pick_executable_json`、`fgm_cover_json`
- 游戏：`fgm_game_modes_json`、`fgm_inspect_json`、`fgm_launch_game_json`、`fgm_deploy_json`、`fgm_restore_json`、`fgm_proxy_status_json`
- 平滑运动：`fgm_sm_inspect_json`、`fgm_sm_inspect_many_json`、`fgm_sm_enable_json`、`fgm_sm_disable_json`、`fgm_sm_restore_json`
- DLSS5：`fgm_dlss5_install_json`、`fgm_dlss5_inspect_json`、`fgm_dlss5_restore_json`、`fgm_dlss5_uninstall_json`、`fgm_dlss5_set_hotkey_json`、`fgm_dlss5_clean_legacy_json`、`fgm_dlss5_process_hotkey_queue_json`
- ANR：`fgm_anr_current_json`、`fgm_anr_import_json`、`fgm_anr_download_json`、`fgm_anr_deploy_json`、`fgm_anr_restore_json`
- 显卡：`fgm_gpu_json`、`fgm_gpu_spoof_inspect_json`、`fgm_gpu_spoof_apply_json`、`fgm_gpu_spoof_restore_json`
- 系统：`fgm_paths_json`、`fgm_open_folder_json`、`fgm_open_path_json`、`fgm_open_hags_json`、`fgm_restart_computer_json`
- 设置与日志：`fgm_settings_get_json`、`fgm_settings_set_json`、`fgm_logs_tail_json`、`fgm_logs_clear_json`、`fgm_diagnostics_export_json`
- 更新：`fgm_update_check_json`、`fgm_update_download_json`、`fgm_update_install_json`、`fgm_update_open_json`
- 窗口：`fgm_window_restore_json`、`fgm_window_save_json`
- 操作控制：`fgm_operation_progress_json`、`fgm_operation_cancel_json`
- 阶段键：`planned`、`backingUp`、`writing`、`verifying`、`installed`、`rolledBack`、`partial`。
- 游戏库信封：`{"ok":true,"games":[...]}` 或 `{"ok":false,"code":"...","context":[...]}`。
- 部署/还原信封包含 `state`、文件计数、`warnings`/`conflicts`。
- `fgm_paths_json` 返回数据目录、pack 目录、内置 pack 目录、许可证、日志、仓库与网盘链接；ANR 云地址来自设置。
- `fgm_game_modes_json` 返回帧生成版本与 pack 版本、平滑运动组件、DLSS5 状态，供卡片徽章使用。

## 布局

- 默认窗口 1280×850，标题 `FrameGen Manager-Next`；图标取 `ui/assets/app.png`。
- 渐变背景 `#F3F6FB → #EEF2F8`，面板白底、圆角 16、描边 `#DDE4EE`、阴影 `0 12px 34px rgba(24,39,75,.10)`。
- 顶栏高 56：左侧图标 + 名称 + 版本胶囊 + 标语（窗口宽度 < 1360 时隐藏标语），导航与语言下拉整体靠右；导航项包裹在浅灰 `#EEF1F5` 圆角组内，当前项为白色胶囊 + 强调色文字。导航项禁用 ink splash，但保留浅蓝 hover 背景与手型指针；选中胶囊白色底 + 浅投影瞬时切换（不做半透明过渡，避免两枚按钮同时发灰），文字颜色/字重在蓝色与灰色 #606B7D 之间按 `MotionTokens.standard` 过渡，旧按钮不会闪黑。
- 按钮沿用旧版 `.btn` 观感：上下渐变、1px 描边、轻阴影、悬停提亮、按下翻转；变体为标准、主色蓝、绿色部署、绿色扫描、浅红危险、黄色启动、幽灵文字、34px 图标按钮与 30px 卡片图标按钮。
- 下拉统一用 `LegacySelect`：34px 渐变描边触发框（文字居中）+ Overlay 自绘菜单，`MotionTokens.quick` 淡入缩放（比 Material 默认更快），12px 圆角、阴影、选中项浅蓝底 + 强调色加粗。
- 平滑运动在两种语言下都显示为 `Smooth Motion`（与旧版一致），不再使用“平滑运动”字样。
- 页面外边距 22×20；帧生成 / 平滑运动 / ANR 使用 340px 左栏 + 右侧列表，宽度 < 980 时改为纵向滚动。
- 扫描进行中在列表头下方显示 3px 不确定进度条，按钮同时显示转圈。
- 页面宿主使用 `StackFit.expand`，About/Donate 等短页面不会被垂直居中，标题始终贴左上。
- 开关使用自绘 `LegacySwitch`：固定 26px 白圆钮，关 `#D9DDE3`、开 `#007AFF`，`MotionTokens.standard` 滑动，开关两态圆钮大小一致。
- 部署弹窗上限 800×780，顶部是 48×48 圆角封面 + 游戏名（20px）+ 安装路径 + 圆形关闭按钮；下方按旧版表单排版：左侧 72px 固定标签列、右侧控件，部署目标、图形 API、代理入口、优化等级、倍率上限、DLSS-G 预设、帧生成引擎、超分逐行排列，节与节之间用 1px 分隔线隔开，底部还原/部署按钮加高到 44px；DLSS5 详情弹窗的图形 API 与代理槽使用同一套行布局。
- 游戏卡片：52×70 缩略图、名称、来源、状态胶囊、底部操作按钮；列表头包含标题、状态、添加、已移除、扫描，其中三个操作按钮统一 38px 高、最小 116px 宽。
- 卡片与侧栏面板使用纯白底、`#DCE5F2` 描边，均不加投影；圆角保持 16。
- 侧栏整宽按钮（打开设置、应用伪装、打开文件夹、ANR 下载/导入）统一 44px 高；图标按钮为 34px 无阴影平面方块，避免小尺寸阴影产生暗角。
- DLSS5 使用 168px 封面墙；ANR 使用与帧生成相同的卡片样式。
- 全局滚动使用 `BouncingScrollPhysics` 并允许鼠标拖拽滚动，减轻滚轮生硬感。
- 页面切换使用 `MotionTokens.page` 淡入 + 2% 垂直位移，进入曲线 `MotionTokens.pageInCurve`，退出曲线 `MotionTokens.pageOutCurve`；动效常量见 [ui-design.md](ui-design.md)。
- 弹窗统一走 `showLegacyDialog`：背景固定 6px 模糊 + 10% 遮罩随 `MotionTokens.enter` 整体淡入（模糊半径不再逐帧变化，见 [ui-design.md](ui-design.md)），弹窗白底 + 18 投影并以 `MotionTokens.enterCurve` 淡入；Esc 与点击遮罩可关闭。
- 卡片操作按钮 38px 高、最小宽 72（部署比启动短）；打开文件夹/删除为无边框 38px 图标按钮，图标 21px，悬停浅蓝底。

- 已解析的封面路径由 `CoverCache` 保存，并写入 `<数据根>/cache/covers/index.json`：同一进程内切换页面直接复用，重启后启动时先读回索引，因此卡片首帧就带封面，不再闪首字母占位，也不会每次启动重新向核心请求。“暂无封面”不写入缓存，核心后续生成的封面下次进入仍会显示。封面解析并发进行，且不再阻塞页面状态刷新——部署状态徽章不必等封面。

## 本地化

- 源文件：`ui/lib/l10n/app_en.arb`、`app_zh.arb`；生成文件不提交。
- Rust Core/Domain 不返回中文或英文句子；错误只返回 `ErrorCode` 和结构化字段。
- 平台键（`dx12`、`vulkan` 等）在 UI 侧统一经 `GraphicsApiLabel` 转为大写渲染，卡片、状态胶囊、部署弹窗、DLSS5 与已移除列表保持一致。

## 构建集成

- exe 与安装包图标使用 `ui/windows/runner/resources/app_icon.ico`（旧版品牌图标，含 16–256 多尺寸）。
- `windows/runner/CMakeLists.txt` 的 POST_BUILD 把 `rust/target/{debug,release}/fgm_ffi.dll` 复制到 exe 目录。
- 核心尚未构建时只告警，不阻塞 `flutter run`。
- 完整门禁：`build/verify-rust-flutter.ps1`。

## 不做 / 已知限制

- WeGame 扫描按用户决定不做。
- 窗口位置交给 Windows（`SWP_NOMOVE`），避免保存坐标在换显示器后把窗口放到屏幕外；运行中每 1 秒保存一次尺寸，最小化时不覆盖记录。首次启动没有存档时，尺寸取主显示器工作区的 85%（下限 1180×760，且始终留出 48px 边距）；存档尺寸在还原前会按当前屏幕重新收拢，换到更小的显示器也不会超出屏幕。
- 不使用第三方状态管理、HTTP 或数据库包。
- RTSS/MSI Afterburner 的帧数覆盖层由 RTSS 注入绘制，程序内无法拦截；已在本机创建 `Profiles/fgm_ui.exe.cfg`（`[Hooking] EnableHooking = 0`）把本程序排除，安装 RTSS 的用户可照此添加，不影响游戏内 OSD。
