# Electron 外壳

本规格描述 Rust 核心 + Electron 桌面外壳。Flutter 版冻结在 `ui/`，不再新增功能；对等后删除。架构决策见 `docs/decisions/ADR-0017-electron-ui.md`。

## 运行形态

- Electron 主进程持有唯一窗口；Rust 核心通过 `fgm-node`（napi-rs）以 `.node` 插件加载。
- 渲染进程是 `ui-electron/renderer/` 下的静态 HTML/CSS/JS；`contextIsolation=true`、`nodeIntegration=false`、`sandbox=true`。
- `preload.js` 只暴露 `window.fgm.call(name, payload)` 与 `openUrl(url)`；渲染进程不接触 Node、文件系统或原生模块。
- 不使用 sidecar、本地 HTTP、WebSocket 或 JSON 行协议。
- 数据目录：开发时 `ui-electron/data`（可用 `FGM_DATA_DIR` 覆盖）；打包后为 exe 同目录 `userdata`。资产包目录：开发时仓库 `packs/`，打包后 exe 同目录 `packs/`。

## 桥接命令

`fgm-node` 的 `invoke(name, payload)` 异步执行（libuv 线程池），返回 JSON 信封字符串；主进程解析后经 IPC 返回对象。未知命令抛错。

| 名称 | 说明 |
|---|---|
| `version` | 产品版本（同步） |
| `library.list` / `library.scan` / `library.removed` | 库缓存、扫描、已移除 |
| `library.addManual` / `library.remove` / `library.restoreRemoved` / `library.forget` | 手动添加、移除、恢复、忘记 |
| `pickExecutable` | 原生 EXE 选择器 |
| `cover` | 封面路径；主进程补充 `url`（`file://`） |
| `gameModes` | 每局部署状态（帧生成/SM/DLSS5/ANR） |
| `proxyStatus` | 目标目录代理归属 |
| `launch` | 启动游戏 |
| `gpu` / `gpuSpoof.inspect` / `gpuSpoof.apply` / `gpuSpoof.restore` | 显卡与型号伪装 |
| `paths` | 数据/资产/许可证/日志目录与链接 |
| `settings.get` / `settings.set` | 设置 |
| `deploy` / `restore` / `inspect` | 帧生成部署、还原、检查 |
| `sm.inspect` / `sm.inspectMany` / `sm.enable` / `sm.disable` / `sm.restore` | Smooth Motion |
| `dlss5.*` | DLSS5 安装/检查/还原/卸载/热键/旧版清理 |
| `anr.*` | ANR 当前包/导入/下载/部署/还原 |
| `logs.tail` / `logs.clear` / `diagnostics.export` | 日志与诊断 |
| `openFolder` / `openPath` / `openHags` / `restartComputer` | 系统动作 |
| `update.*` | 更新检查/下载/安装/打开 |
| `operation.progress` / `operation.cancel` | 长操作进度与取消 |
| `window.restore` / `window.save` | 窗口尺寸持久化 |

所有返回值沿用 FFI JSON 信封：`{"ok":true,...}` 或 `{"ok":false,"code":"...","context":[...]}`。

## 界面

- 顶栏：品牌图标/名称/版本/标语、7 个导航项、语言下拉；导航右侧对齐，白色胶囊由 JS 按选中项定位，300ms 贝塞尔滑动。
- 帧生成页：列表头 + 340px 侧栏（GPU/HAGS、显卡型号伪装、资产文件夹、来源）+ 游戏卡片。
- 游戏卡片：52×70 封面/首字母、名称、来源/API/状态徽章、盘符大写路径、部署/启动/打开目录/移除。
- 部署弹窗：旧版样式（封面头部、左标签 + 右下拉）、背景 6px 模糊 + 10% 遮罩、淡入缩放；选项暂只把代理槽传给后端。
- 动效：`motion.css` 统一提供页面/面板/卡片入场、导航滑动、按钮按压、弹窗、Toast、缩略图淡入；遵循 `prefers-reduced-motion`。

## 打包与体积

- `npm run package`：裁剪 Electron locale（仅 en-US/zh-CN）、移除 `default_app.asar`、应用文件打进 `resources/app.asar`（原生插件 `native/*` unpack），复制 `packs/` 与 `licenses/`，用 rcedit 写入品牌图标与版本。
- 当前基线：解压约 312MB，zip（Deflate）约 141MB；Inno LZMA2 预计 90–110MB。Electron 运行时是体积下限。
- 内存基线：主/渲染/GPU/utility 合计 WorkingSet 约 380MB、Private 约 300MB；后续通过禁用无用特性、减少 DOM/IPC、延迟加载页面继续压。

## 测试与门禁

- `cargo fmt --check`、`cargo clippy --workspace --all-targets --all-features -- -D warnings`、`cargo test --workspace`。
- `ui-electron`：`npm run check`、`npm test`（native addon 版本/paths/未知命令）、`npm run package`。
- 门禁脚本：`build/verify-rust-electron.ps1`；CI 使用 Node 24 + npm ci + 该脚本，不再安装 Flutter。
- 构建前关闭运行中的 `FrameGen-Manager-Next.exe` / `electron.exe`，否则原生插件被占用、`build:native` 复制失败。

## 已实现页面

- 帧生成：列表、卡片、部署/还原、移除、已移除弹窗、扫描、手动添加、启动、打开目录、GPU 顶号、资产目录、来源。
- Smooth Motion：兼容性面板、来源、说明、每局开启/关闭、启动/目录/移除。
- DLSS5 / ReShade：封面墙、检查、详情弹窗、安装/卸载、启动/移除。
- ANR：组件包在线下载/导入/网盘、当前 GPU、要求说明、每局部署/还原。
- 日志：读取、格式化、打开目录、清理、导出诊断包。
- 关于：版本、更新检查/下载/安装、GitHub、许可证、启动扫描、RE Engine 开关、反馈。
- 打赏：微信/支付宝二维码。
- 顶栏语言选择：中/英运行时切换并写入设置；导航、页面标题与按钮文案跟随。
- 窗口尺寸/位置持久化到数据目录 `config/window.json`，越界时回退居中。
- 部署/还原/SM/DLSS5/ANR 长操作显示底部进度卡并轮询 `operation.progress`，提供取消。

## 未完成 / 后续

- DLSS5 详情已支持 API/代理选择与快捷键录制（字母、数字、F1–F12、Esc、空格、Tab、Enter）。
- 帧生成部署支持 XeSS 光流分辨率/无 HUD 选项；切到 XeSS 时优化档位与预设禁用。
- ANR 下载进度依赖全局进度轮询，连接阶段可能长时间显示不确定态。
- 更新源 `update.json` 尚未发布，更新检查会显示“已是最新”或错误。
- 真实游戏回归、删除 Flutter/C# 遗留树、正式发布流程仍未进行。
