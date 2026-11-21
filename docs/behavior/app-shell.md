# 应用外壳与 Electron 宿主

## 启动

1. Electron 主进程创建无边框 `BrowserWindow`，渲染进程加载本地 `renderer/index.html`，不加载远程页面。
2. 窗口 `ready-to-show` 后再显示，避免白屏闪烁；窗口尺寸和位置从 `userdata/config/window.json` 恢复。
3. 渲染层先画应用壳和骨架卡片，再并行读取 paths、GPU、游戏库、GPU 伪装和设置；`gameModes` 独立异步加载，不阻塞首屏。
4. GPU 尚未返回时，游戏卡片照常显示，只有依赖 GPU 的部署按钮禁用并显示“加载中”；模式状态未返回时同样禁用，不显示错误的“未部署”。
5. 首屏完成后才执行资产版本清理和更新检查；两者都不阻塞页面交互。

## 权限模型

- Electron 主进程以当前用户权限启动，不整体提权；安装版和便携版行为一致。
- 写游戏目录时由文件系统决定权限：受保护目录返回 `AccessDenied`，界面显示错误码和诊断信息。
- GPU 型号伪装需要管理员权限；未提权时按钮禁用并提示，不整体提权主程序。
- 游戏启动继承当前进程令牌；Steam/Epic 协议交给已运行的客户端，手动/散装游戏按当前用户权限启动。
- Xbox/商店游戏的 `WindowsApps` 即使管理员也只有读+执行权限，仍然按只读处理。

## Electron 配置

- `BrowserWindow` 使用 `sandbox: true`、`contextIsolation: true`、`nodeIntegration: false`；渲染进程只能通过 preload 暴露的窄 API 访问原生能力。
- 打包版关闭 DevTools；只加载本地 `renderer/index.html`，CSP 不允许远程脚本。
- 用户数据目录：便携版 `<程序目录>\userdata`；安装版 `%LOCALAPPDATA%\FrameGen-Manager-Next`；可用 `FGM_DATA_DIR` 覆盖。
- 原生插件 `native/fgm_node.node` 由 Electron 主进程加载，不启动独立核心进程。

## 日志清理

- 日志页【清理日志】删除 `logs/` 下除最新文件以外的 `*.log`；最新文件是 Serilog 当前写入的 sink，始终保留。
- 清理后返回删除数量并重新读取日志尾部；没有历史文件时提示“没有可清理的历史日志”。被占用而无法删除的文件计入失败数，不影响其他文件。

## 窗口缩放

- `WebView2` 是独立子窗口，会吞掉边框上的鼠标消息。`BrowserHost` 保留 6px 外边距，`WindowChrome.ResizeBorderThickness` 设为 6 DIP，让缩放命中区落在 WPF 窗口本身；窗口四个方向都可拉伸。
- 关闭窗口时把宽度、高度和最大化状态写入 `app.json`；最大化时记录 `RestoreBounds`，下次启动恢复上次的普通尺寸和最大化状态。读取时按 `MinWidth`/`MinHeight` 与当前虚拟屏幕尺寸钳制，显示器布局变化后不会恢复出屏幕外的尺寸。
- 关于页内容高于可视区时由 `.view` 容器滚动到底，底部“反馈”卡片完整可见；页面主体使用 `height: auto`，不会被 `height: 100%` 的页面容器裁切。
- 顶栏导航在英文下使用短标签（FrameGen / DLSS5 / ANR）；空间不足时导航自身横向滚动，语言下拉固定在右侧、不参与压缩。
- 各模式页左侧卡片显示该模式的支持显卡，并提供带 GitHub 图标的资产来源按钮；点击后通过 `updates.openPage` 交给系统浏览器打开对应仓库。
- 游戏卡片的“打开文件夹/移除”使用内联 SVG 图标按钮（不再使用 emoji），按钮保留 `title` 与 `aria-label`；图标按钮为 30×30，位于卡片操作区右侧。
- 键盘交互：按钮/选择框/链接有 2px accent 焦点环；弹窗打开时聚焦弹窗容器，关闭后把焦点还给触发元素；全局 toast 带 `role="status"` 和 `aria-live="polite"`；`prefers-reduced-motion` 下关闭动画和过渡。

## 第三方许可

- 根目录 `licenses/` 与程序一起发布，安装包和便携版都带（`build\publish-installer.ps1`、`build\publish-portable.ps1` 各拷一次；Inno 的 `[Files]` 递归收录）。
- `licenses\README.md` 是组件 × 许可 × 全文位置的索引；各子目录放上游原文（`dlssg_for_sm86`、`gitc_uplift`、`reshade`、`reshade_shaders`、`reframework`、`smooth_motion`）。
- “关于”页提供“打开许可目录”，走 `settings.openFolder` 的 `which=licenses`；目录缺失时返回 `ErrorCode.FileNotFound`，不静默创建空目录。
- 许可文本不写进游戏目录：部署只放运行所需文件，卸载不会留下我们的文档。

## 页面状态缓存

- 每个页面首次进入时挂载到一个 `.page-host`；切页时把旧 host 从 `#view` 摘下并保留在内存，返回时重新挂载，不再重建 DOM。
- 离开页面前保存 `#view`、`.game-list` 和 `.side` 的滚动位置，返回时恢复；部署、还原或扫描后的显式刷新不会跳回顶部。
- 页面 DOM 最多缓存 5 秒；超过 5 秒再进入会重新渲染并刷新数据。页面模块在数据真正渲染完成后才标记 `ready`，切页中途离开的骨架页不会被视为已完成。
- `gameModes` 由渲染层做 5 秒 TTL 缓存；部署、还原、扫描、增删游戏、SM 开关、DLSS5/ANR 安装卸载和 GPU 伪装后立即失效。SM、ANR、帧生成页共享同一份结果，避免切页重复扫描。
- DLSS5 状态最多 4 个并发检查，逐卡出现，结果缓存 60 秒；安装/卸载后立即失效。SM 使用 `gameModes` 返回的驱动配置详情，不再单独调用 `sm.inspectMany`。
- 封面只请求视口内（含 200px 预加载边距）的卡片，最多 2 个并发；优先 600×900 原图，失败封面有 10 分钟渲染层负缓存和 1 小时原生负缓存，离线时只使用本地素材和 EXE 图标。
- 进度轮询统一 500 ms，增加防重入；窗口隐藏或最小化时暂停，恢复可见时立即拉取一次。
- 全屏 `backdrop-filter` 模糊已移除，骨架 shimmer 和进度流光已移除；保留必要的 spinner 和一次性入场动画。
- Chromium 关闭未使用的 WebGL 和拼写检查；V8 半空间上限设为 8 MB，降低渲染器提交内存。
- 窗口尺寸和位置写入 `config/window.json`；窗口最小尺寸 1360×820。

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
