# ADR-0008：WebView2 UI 架构

- 状态：已接受（全部业务页面）
- 日期：2026-10-29

## 背景

WPF/Wpf.Ui 页面在功能上已经可用，但视觉和动效达不到旧版 HTML/CSS UI 的观感。用户提出改用 Electron。直接切 Electron 会把现有 C# 核心推回 sidecar/IPC，和项目既定的单进程架构冲突。

## 决策

保留现有 C# Domain/Core/Infrastructure，新增 **WebView2 单页 spike**：

- 全部业务页面由 WebView2 承载本地 HTML/CSS/JS；WPF 只保留窗口外壳。
- `WebAssets/` 下的静态资源通过 `https://fgm.local` 虚拟主机加载，不允许远程页面。
- C# 通过 `WebMessageReceived` / `PostWebMessageAsJson` 暴露白名单消息：
  - `ready`、`scan`、`refresh`、`addGame`、`cancelScan`
  - `deploy`、`restore`、`remove`、`openFolder`、`openHags`
- 前端只接收结构化状态和进度，不直接访问文件系统。
- WebView2 用户数据放在 `%LOCALAPPDATA%\FrameGen-Manager-Next\webview2\<安装路径哈希>`：既避开便携目录中的空格，也避免开发版、便携版和不同更新目录共用同一份浏览器锁文件。
- 单实例运行（`Local\FrameGenManagerNext.App` 互斥体），防止两个宿主同时占用同一 WebView2 配置。
- WPF 旧页面、ViewModel 和主题服务已删除；需要回退时从 git 历史恢复。

## 依赖

- NuGet：`Microsoft.Web.WebView2` `1.0.4314.35`（最新稳定版）。
- 运行环境：Microsoft Edge WebView2 Runtime（Windows 11 自带；便携版 README 说明）。
- 不引入 Node/Electron 运行时；WebAssets 是纯静态文件。

## 结果与限制

- 已验证：WebView2 初始化、导航、GPU/游戏列表、部署参数面板、扫描/刷新、Smooth Motion/DLSS5 状态页、日志、设置和关于页。
- 未验证：部署/还原/SM/DLSS5 的真实游戏写操作仍待验收。
- 风险：WebView2 Runtime 缺失时显示回退提示；多一套前端资源需要维护；离线安装包需确认 Runtime 可用。
- 前端结构：`WebAssets/js/api.js`、`i18n.js`、`ui.js`、`app.js`、`pages/*.js`；C# 桥按功能拆分 `AppWebBridge.*.cs`。

## 浏览器进程故障处理

`CoreWebView2.ProcessFailed` 会记录 `Kind`、`Reason`、`ExitCode`、`FailureSourceModulePath` 和 `ProcessDescription`。宿主最多自动重建三次 WebView2（第二次起改用软件渲染），成功后在原位置恢复界面；三次都失败才显示回退面板和“重新加载界面”按钮。浏览器进程已经退出时，桥接层丢弃迟到的响应，不把异常抛回命令处理器。

已确认的外部冲突：RivaTuner Statistics Server（RTSS）/ MSI Afterburner 的 `RTSSHooks64.dll` 注入 `msedgewebview2.exe` 后在浏览器进程内触发访问冲突（`0xC0000005`），表现为 `BrowserProcessExited`。RTSS 自带 `chrome.exe`、`msedge.exe` 的 `EnableHooking=0` 排除模板，但缺少 `msedgewebview2.exe`。在 RTSS 中把 `msedgewebview2.exe` 的 Application detection level 设为 None，或更新 RTSS，可以消除该冲突；应用检测到 RTSS 时会显示对应提示。
