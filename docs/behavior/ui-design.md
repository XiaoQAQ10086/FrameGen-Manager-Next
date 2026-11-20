# UI 设计规格

本文件是 `ui/lib/design/` 的数值基线：动效、排版与颜色的唯一来源。
[flutter-shell.md](flutter-shell.md) 描述页面结构与交互流程，具体数值以本文件为准。

## 动效

所有动画时长与缓动集中在 `ui/lib/design/motion_tokens.dart` 的 `MotionTokens` 里，共四档：

| 常量 | 时长 | 用途 |
|---|---|---|
| `MotionTokens.quick` | 120ms | 下拉菜单等立即弹出的浮层 |
| `MotionTokens.standard` | 180ms | 按钮、开关、导航文字等控件反馈 |
| `MotionTokens.enter` | 220ms | 弹窗及其背景遮罩的入场 |
| `MotionTokens.page` | 300ms | 页面之间的淡入切换 |

曲线：

- `MotionTokens.enterCurve`（`Curves.easeOutCubic`）：元素出现或落位时的减速
- `MotionTokens.exitCurve`（`Curves.easeInCubic`）：元素离场时的加速
- `MotionTokens.pageInCurve` / `pageOutCurve`：页面切换的对称缓动，与旧版 WebView 观感一致

### 规则

- 新增动画必须引用 `MotionTokens`，不得直接写 `Duration(milliseconds: ...)` 或 `Curves.*`。
- 进度轮询的 200ms 定时器、部署后的 250ms 刷新延时属于业务节拍，不是动效，不受本规格约束。
- 动效子树应包在 `RepaintBoundary` 内，避免整页重绘。
- 弹窗背景模糊半径固定为 6px，只做整体淡入，不逐帧改变 sigma：逐帧变半径会让模糊滤镜每帧重算，在弱显卡上直接掉帧。

| 编号 | 条件 | 行为 | 测试 |
|---|---|---|---|
| UI-MOTION-001 | 切换页面 | 300ms 淡入 + 2% 垂直位移 | `app_smoke_test.dart` |
| UI-MOTION-002 | 打开弹窗 | 220ms 遮罩与弹窗同步淡入，Esc 与点击遮罩可关闭 | 手动 |
| UI-MOTION-003 | 展开下拉菜单 | 120ms 淡入缩放 | 手动 |
| UI-MOTION-004 | 切换开关或导航项 | 180ms 过渡 | 手动 |

## 排版

字体使用系统 `Microsoft YaHei UI`，回退 `Microsoft YaHei` → `Segoe UI`，不打包字体文件。字号只有八档，全部为整数像素（半像素值在 Windows 上会被子像素定位拉糊）：

| 字号 | 用途 | 主题槽位 |
|---|---|---|
| 11 | 微标签、角标、绶带 | `labelSmall` |
| 12 | 次级标签、列表头辅助文字 | `labelMedium` |
| 13 | 卡片正文、说明文字 | `bodySmall` / `labelLarge` |
| 14 | 正文、卡片标题、导航项 | `bodyMedium` / `titleMedium` |
| 16 | 小节标题 | `titleLarge` |
| 20 | 弹窗与页面标题 | `headlineSmall` |
| 28 | 展示数字 | `displaySmall` |
| 44 | 首字母水印（仅单字符） | 无 |

字重只有四档：`w400` 正文、`w500` 标签、`w600` 小标题、`w700` 强调。不使用 `w800` 及更重的字重，粗体在 12–14px 下会糊成一团。

行高：正文 `1.5`，标题 `1.25`–`1.35`，标签 `1.35`–`1.4`。

文字颜色：

- 主文字 `AppTokens.textPrimary`（`#172033`），次文字 `AppTokens.textSecondary`（`#606B7D`）。
- 浅色底上的强调文字用加深版本：`AppTokens.accentText`（`#0062CC`）配 `accentSoft`，`AppTokens.warningText`（`#8F4800`）配 `warningSoft`，保证小字号也达到 WCAG AA 4.5:1。`LegacyPill` 会自动替换这两组前景色，边框仍用原色。
- 首字母水印统一用 `AppTokens.monogram`（`#4E6F9D`）。

护栏：`ui/test/design_tokens_test.dart` 扫描 `lib/` 源码，字号不在八档内、字重超出四档、或在 `MotionTokens` 之外直接写 `Curves.*` 都会让测试失败。

## 已知限制

- 不响应 Windows 的系统文字缩放比例：布局使用固定逻辑像素（52×70 缩略图、56×32 开关、340px 侧栏），放大文字会破坏排版。
- 页面切换时各页面重建并重新通过 bridge 取数，切页首帧会短暂留白（另案处理）。
