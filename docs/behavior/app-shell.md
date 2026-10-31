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

## 页面状态缓存

- 页面只挂载一次：切换导航时隐藏旧页面、显示已挂载页面，不再销毁 DOM 或重新探测游戏/驱动。
- 离开页面 5 分钟后自动卸载该页 DOM（清理事件监听、移除节点），同时清空前端 API 缓存；再次进入时按需重新挂载。这样长时间停留在某一页时，其他页的封面解码和列表节点不会一直占内存。
- `api.js` 缓存 `app.info`、`games.state`、`gpu.state`、`sm.state`、`dlss5.state` 120 秒；扫描、部署、还原、开关 Smooth Motion、DLSS5 安装/卸载和语言保存会清空缓存。
- 语言切换会清空已挂载页面并重建，避免留下旧语言文案。
- 封面不再以 base64 塞进状态 JSON：缓存目录通过 `fgm-covers.local` 虚拟主机提供图片 URL，减少桥接数据量和 GDI/图片内存。
- 封面解码前统一生成 360px 宽的 JPEG 缩略图（`cache/covers/thumbs`），卡片只加载缩略图，避免 WebView2 为 1200x1800 原图保留大块解码内存。
- 窗口最小化或隐藏时调用 `CoreWebView2.TrySuspendAsync()` 暂停浏览器进程，恢复时 `Resume()`；暂停后顺带压缩一次托管大对象堆。这样游戏期间不会继续占用 WebView2 的 CPU/GPU，也不会持续累积渲染缓存。
- 列表只给前 12 张卡片播放入场动画，其余卡片使用 `content-visibility: auto`；滚动到视口外时不参与布局/绘制，减少大游戏库的 GPU 开销。
- 帧生成与 SM 页的列表重新渲染时保留原 `scrollTop`；分批渲染会继续追加卡片直到恢复原位置，部署、扫描或状态刷新不会跳回顶部。
- 帧生成与 SM 页的左侧栏和游戏列表使用自动隐藏滚动条：仅在滚动时显示，停止约 700 ms 后淡出；未滚动时不占视觉注意力。
- 主机进程启动时同时设置空闲优先级（`SetPriorityClass(IDLE_PRIORITY_CLASS)`）和 EcoQoS（`SetProcessInformation(ProcessPowerThrottling)`）。Windows 11 任务管理器必须同时看到这两者才显示“效率模式”；只设 EcoQoS 不会显示。旧版 Windows 拒绝调用时忽略。
- WebView2 默认追加 `--js-flags=--max-old-space-size=192 --disable-background-networking --disable-component-update --disable-sync --disable-domain-reliability`，给渲染器堆设上限并关闭后台服务。实测不要设置 `MemoryUsageTargetLevel=Low`：在 154.x WebView2 运行时上会把大约 95 MB 的共享内存转为宿主私有工作集，任务管理器反而从约 160 MB 涨到约 260 MB。
- 进程配置 `System.GC.ConserveMemory=7`，用少量 CPU 换取更小的托管堆。
- 窗口最小化/隐藏时在暂停 WebView2 后压缩 LOH 并调用 `SetProcessWorkingSetSize(-1,-1)` 修剪工作集，把空闲页移到 standby。
- 部署清单记录每个已写文件的最后写入时间；再次检查时大小和时间都一致就跳过哈希，避免每次打开页面重读数百 MB 的运行库（旧清单在首次检查通过后自动补写时间戳）。

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

- 启动时若检测到 RTSS 安装目录，且 `msedgewebview2.exe.cfg` 排除配置不存在，应用会尝试写入 `[Hooking] EnableHooking=0`（`Profiles` 和 `ProfileTemplates` 各一份）；已有配置不覆盖。
- 写入需要管理员权限。权限不足时保留 RTSS 专用回退说明，并提示用户在 RTSS 中手动排除 `msedgewebview2.exe`（Application detection level = None）。
- 应用不降低 WebView2 的安全设置，也不修改 RTSS 的其它配置。
