# ADR-0008：WebView2 帧生成页 UI spike

- 状态：试验中（仅“帧生成”页）
- 日期：2026-10-29

## 背景

WPF/Wpf.Ui 页面在功能上已经可用，但视觉和动效达不到旧版 HTML/CSS UI 的观感。用户提出改用 Electron。直接切 Electron 会把现有 C# 核心推回 sidecar/IPC，和项目既定的单进程架构冲突。

## 决策

保留现有 C# Domain/Core/Infrastructure，新增 **WebView2 单页 spike**：

- 只在“帧生成”页用 WebView2 承载本地 HTML/CSS/JS。
- `WebAssets/` 下的静态资源通过 `https://fgm.local` 虚拟主机加载，不允许远程页面。
- C# 通过 `WebMessageReceived` / `PostWebMessageAsJson` 暴露白名单消息：
  - `ready`、`scan`、`refresh`、`addGame`、`cancelScan`
  - `deploy`、`restore`、`remove`、`openFolder`、`openHags`
- 前端只接收结构化状态和进度，不直接访问文件系统。
- WebView2 用户数据放在 `%LOCALAPPDATA%\FrameGen-Manager-Next\webview2`（避免便携目录中的空格导致浏览器进程异常）。
- WPF 侧保留 `LibraryPage` 作为回退；`MainWindow` 切回 `LibraryPage` 即可撤销本 spike。

## 依赖

- NuGet：`Microsoft.Web.WebView2` `1.0.4314.35`（最新稳定版）。
- 运行环境：Microsoft Edge WebView2 Runtime（Windows 11 自带；便携版 README 说明）。
- 不引入 Node/Electron 运行时；WebAssets 是纯静态文件。

## 结果与限制

- 已验证：WebView2 初始化、C# 状态下发、GPU/游戏列表渲染、部署参数面板、刷新消息。
- 未验证：部署/还原的真实游戏执行；目前仅“帧生成”页迁移，其他页面仍是 WPF。
- 风险：WebView2 Runtime 缺失时显示回退提示；多一套前端资源需要维护；离线安装包需确认 Runtime 可用。
- 后续若验收通过，再逐页迁移 Smooth Motion、DLSS5、日志、设置，并删除对应的 WPF 页面。
