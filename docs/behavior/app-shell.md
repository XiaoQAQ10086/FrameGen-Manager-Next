# 应用外壳与 WebView2 宿主

## 启动

1. `Program.Main` 先执行 Velopack 启动钩子，再尝试获取 `Local\FrameGenManagerNext.App` 互斥体。
2. 已有实例在运行时，新进程直接退出，不创建窗口、不打开第二份 WebView2 配置。
3. 主机启动后读取设置语言，创建 `MainWindow`，在 `Loaded` 事件中显示加载层并初始化 WebView2。
4. 加载层是独立 `Popup`，不会把 WebView2 的父容器设为 `Collapsed`（WebView2 在不可见时会延迟初始化）；WebView2 就绪后关闭加载层。
5. 页面自身也带启动骨架；游戏库状态读取完成前显示骨架卡片，不显示空白页。

## WebView2 配置

- 用户数据目录：`%LOCALAPPDATA%\FrameGen-Manager-Next\webview2\<安装路径 SHA-256 前 12 位>`。
- 同一安装目录重复启动复用同一配置；开发版、便携版和不同更新目录互不干扰。
- 本地资源通过虚拟主机 `https://fgm.local` 映射到 `WebAssets/`，不允许加载远程页面。
- 初始化日志记录配置目录、Runtime 版本和附加浏览器参数。

## 浏览器进程故障

- `ProcessFailed` 记录失败类型、原因、退出码、失败模块和进程描述。
- 宿主最多自动重建三次 WebView2：
  1. 第一次重建使用默认参数。
  2. 第二次及以后追加 `--disable-gpu --disable-gpu-compositing --disable-software-rasterizer`。
  3. 三次都失败后显示回退面板，提供“重新加载界面”按钮。
- 重建时移除旧控件、在原位置插入新控件，并重新绑定桥接事件。
- 浏览器进程已退出时，桥接层丢弃迟到的 `PostWebMessageAsJson` 响应，不抛出异常。

## 外部注入冲突

RivaTuner Statistics Server（RTSS）/ MSI Afterburner 会把 `RTSSHooks64.dll` 注入 `msedgewebview2.exe`。部分 RTSS 版本在 Chromium 154 上会在浏览器进程内触发访问冲突（`0xC0000005`），WebView2 报 `BrowserProcessExited`。

- 失败模块包含 `RTSS` / `RivaTuner`，或本机存在 RTSS 且进程正在运行时，回退面板显示 RTSS 专用说明。
- 处理方式：在 RTSS 中把 `msedgewebview2.exe` 的 Application detection level 设为 None，或更新 RTSS。
- 应用不修改 RTSS 配置，也不要求用户降低 WebView2 的安全设置。
