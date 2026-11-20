# ADR-0017: Electron UI with a napi-rs core bridge

- 状态：Accepted
- 日期：2026-11-20
- 取代：ADR-0012（Rust + Flutter 重写）中的 UI 选型

## 背景

Rust 核心、部署引擎、资产包和 CLI 已完成并通过门禁；Flutter 版 UI 已达到旧版功能与布局对等。项目维护者决定停止 Flutter UI 的继续开发，改用 Electron + HTML/CSS/JS 承载界面，以便：

- 直接复用旧 WebView2 版本的 `WebAssets`（布局、样式、组件、页面结构）；
- 用 Web 技术实现更细的动效与交互；
- 让 Rust 核心继续以单进程方式嵌入桌面程序，不引入 sidecar 或本地 HTTP。

同时接受 Electron 自带 Chromium 带来的体积下限。

## 决策

1. **UI 使用 Electron**，渲染层是静态 HTML/CSS/JS，不引入前端框架、打包器或 Node 运行时依赖。
2. **Rust 核心通过 napi-rs 原生插件嵌入 Electron 主进程**：
   - `fgm-node`（cdylib → `.node`）只做类型转换、错误映射和异步任务调度；
   - 业务规则仍在 `fgm-domain` / `fgm-core` / `fgm-infra`；
   - 不使用 sidecar、本地 HTTP、WebSocket、JSON 行协议。
3. 保留 `fgm-ffi` 作为 C ABI，用于 Flutter 冻结构建、CLI 与集成测试；后续抽出 `fgm-api` 门面后，`fgm-ffi` 与 `fgm-node` 都只依赖该门面。
4. Electron 侧边界：
   - 主进程只创建窗口、加载静态资源、调用 `fgm-node`；
   - `preload` 通过 `contextBridge` 暴露窄 API；
   - 渲染进程 `nodeIntegration=false`、`contextIsolation=true`，不接触 Node/文件系统。
5. **Flutter 进入冻结状态**：不再新增功能；Electron 达到功能对等后删除 `ui/`、Flutter 构建脚本和 CI 步骤。冻结期间 `ui/` 只作为视觉与行为对照。
6. 构建与发布改为 Rust workspace + `ui-electron`；安装器继续使用 Inno Setup 流程，便携版目录固定、仅保留最新安装器。

## 体积与性能预算

- 安装包（压缩后，不含资产包）：目标 ≤ 110MB；Electron 运行时是主要成本，napi-rs 不改变 Chromium 体积。
- 启动：冷启动 ≤ 3s（本机、8 个游戏缓存）。
- 空闲内存：主进程 + 渲染进程 + GPU/utility 合计目标 ≤ 220MB。
- 长操作（扫描/部署/下载）不得阻塞主进程；通过 napi 异步任务执行，进度用事件推送。

## 迁移阶段

1. 搭建 `ui-electron` 壳与 `fgm-node` 最小命令集（版本、库列表、扫描、GPU、封面、启动、打开目录）。
2. 帧生成页迁移并与 Flutter 版对照。
3. Smooth Motion、DLSS5、ANR、日志、关于、打赏逐页迁移。
4. 补齐部署/还原/更新/GPU 顶号/诊断导出，完成真实游戏回归。
5. 删除 Flutter 树与旧构建脚本，门禁切到 Rust + Electron。
6. 打包裁剪、内存/启动测量与发布。

## 后果

- 体积显著大于 Tauri，但运行时完全自带、渲染版本可控。
- Electron 是多进程模型，"单进程优先"约束改为"核心单进程嵌入主进程，UI 允许 Electron 自身的渲染/GPU 子进程"。
- 需要维护 napi-rs 构建链和 Electron 版本固定；原生插件必须与 Electron 的 Node ABI 匹配（N-API 降低重编频率）。
- WebAssets 的 CSS 可直接复用，但旧 JS 桥接层（WebView2 消息）需要重写为 preload IPC。
- RTSS/MSI Afterburner 的 OSD 注入需按 exe 建排除 profile；沿用已知方案。
