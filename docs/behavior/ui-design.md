# UI 设计规格（Electron）

本文件是 `ui-electron/renderer/` 的数值基线：[electron-shell.md](electron-shell.md) 描述页面结构与交互流程，具体数值以本文件为准。Flutter 版设计令牌已随 `ui/` 删除，不再维护。

## 动效

时长与曲线集中在 `motion.css` 的 `:root`：

| 变量 | 值 | 用途 |
|---|---|---|
| `--motion-quick` | 120ms | 按钮按压、下拉等立即反馈 |
| `--motion-standard` | 180ms | 导航文字、卡片、按钮 hover |
| `--motion-enter` | 220ms | 弹窗、遮罩、Toast、进度卡入场 |
| `--motion-page` | 300ms | 页面/面板入场、导航胶囊滑动 |

曲线统一为 `--motion-ease: cubic-bezier(.22, 1, .36, 1)`（减速落位）。

规则：

- 新增动画必须引用上述变量，不直接写 `ms` 或自定义 `cubic-bezier`。
- 进度轮询的 200ms 定时器属于业务节拍，不是动效。
- 弹窗背景固定 `blur(6px) saturate(1.02)` + `rgba(16,24,40,.10)` 遮罩，只整体淡入，不逐帧改半径。
- 卡片入场按 `animation-delay: min(index * 35ms, 350ms)` 错峰；侧栏面板 0/25/50/75ms。
- `@media (prefers-reduced-motion: reduce)` 下所有动画/过渡压到 0.01ms。

## 设计 token

- 圆角：`--radius-sm:8px`（控件/下拉项）、`--radius-md:12px`（内嵌卡片/菜单）、`--radius-lg:16px`（主卡片/面板/弹窗）；胶囊/开关/进度条保留 999px。
- 间距：`--space-1:4px` … `--space-8:32px`，所有 padding/margin/gap 均引用 token。
- 状态色：success / danger / launch 各有语义 token；内容卡片使用实色 `#fff`。
- 玻璃只用于 modal overlay（6px blur）；内容卡片、表单、日志、进度卡均为实色。

## 排版

系统字体栈：`system-ui, -apple-system, "Segoe UI", "Microsoft YaHei UI", "PingFang SC", sans-serif`，正文 `14px/1.5`；路径/哈希用 `Consolas, "Courier New", monospace`。不打包字体。

字号由 `:root` token 统一：`--text-xs:12px`、`--text-sm:13px`、`--text-md:14px`、`--text-lg:16px`、`--text-xl:22px`、`--text-display:26px`。路径等宽字号保留 11.5/12px，`.tile .ph` 的 40px 首字母水印保留为显示例外。

| 字号 | 用途 |
|---|---|
| 12 | 绶带、tile 角标、品牌辅助文字 |
| 13 | 徽章、说明文字、卡路径、部署选项 |
| 14 | 正文、键值行、小节标题 |
| 16 | 游戏名、更新版本号 |
| 22 | 列表页标题 |
| 26 | 页面大标题、弹窗标题 |

字重只使用 400 / 600 / 650 / 700 / 800：正文 400，徽章与小标签 600/650，按钮与导航 700，首字母水印 800。不使用 500，避免 12–14px 下糊字。

## 颜色与尺寸

- 背景 `--bg1 #f3f6fb` → `--bg2 #eef2f8` 渐变；主文字 `--ink #172033`；次文字 `--muted #606b7d`；分隔线 `--line #dde4ee`。
- 强调色 `--accent #007aff`，浅底 `--accent-soft rgba(0,122,255,.1)`；警告 `--warn #b25a00`；错误文字 `#a52222`。
- 卡片/面板：白色半透明 `--card rgba(255,255,255,.92)`，圆角 `--radius 16px`，阴影 `--shadow`；游戏卡片不加淡阴影。
- 固定尺寸：侧栏 340px（宽屏以下 270px）、封面 52×70、导航胶囊高度随导航项、顶栏 56px。
- 按钮文字始终 700，不使用文字阴影；禁用态只降透明度，不改字重。

## 已知限制

- 不响应 Windows 系统文字缩放：布局使用固定 CSS 像素。
- 原生 `<select>` 由 `motion.css` 覆盖显示；旧版 `custom-select.js` 未接入，下拉观感依赖系统控件。
- 页面切换时模块重建并重新取数，首帧可能出现骨架屏。
