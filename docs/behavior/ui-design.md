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

| 编号 | 条件 | 行为 | 测试 |
|---|---|---|---|
| UI-MOTION-001 | 切换页面 | 300ms 淡入 + 2% 垂直位移 | `app_smoke_test.dart` |
| UI-MOTION-002 | 打开弹窗 | 220ms 遮罩与弹窗同步淡入，Esc 与点击遮罩可关闭 | 手动 |
| UI-MOTION-003 | 展开下拉菜单 | 120ms 淡入缩放 | 手动 |
| UI-MOTION-004 | 切换开关或导航项 | 180ms 过渡 | 手动 |

## 已知限制

- 不响应 Windows 的系统文字缩放比例：布局使用固定逻辑像素（52×70 缩略图、56×32 开关、340px 侧栏），放大文字会破坏排版。
- 页面切换时各页面重建并重新通过 bridge 取数，切页首帧会短暂留白（另案处理）。
