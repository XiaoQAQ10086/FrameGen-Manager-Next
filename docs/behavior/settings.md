# 设置与封面行为规格

来源：旧版 `ui/settings.js`、`ui/app.js` 的封面/设置部分。
新实现：`Domain/Settings`、`Infrastructure/Settings`、`Infrastructure/Library/SteamCoverService.cs`。

## 设置文件

- 位置：`<数据目录>\config\app.json`。
- 字段：`schemaVersion`、`language`、`theme`、`autoScanOnStartup`、`showCovers`。
- 读取失败或 JSON 损坏：隔离为 `app.json.bad`，使用默认值。
- 写入：临时文件 + 原子替换；未知枚举值回退到默认。
- `schemaVersion = 1` 的旧文件在读取时迁移到 `schemaVersion = 2`，`language` 一并改为 `system`；之后手动选择会以 `schemaVersion = 2` 保存。

默认值：

| 字段 | 默认 |
|---|---|
| language | `system`（跟随 Windows 显示语言：中文 → 简体中文，其他 → English） |
| theme | `light`（固定浅色，暂不适配深色） |
| autoScanOnStartup | `false` |
| showCovers | `true` |

可选语言值：`system`、`chineseSimplified`、`english`。选择 `system` 时每次启动重新检测系统语言；手动选择中文或 English 后保持该选择。

## 行为

| 编号 | 条件 | 行为 |
|---|---|---|
| SET-001 | 启动主题 | 固定浅色；不读取系统深色模式 |
| SET-002 | 保存设置 | 立即写盘并应用浅色主题；页面显示“设置已保存” |
| SET-003 | 自动扫描开启 | 启动加载缓存后执行一次完整扫描；默认关闭 |
| SET-004 | 封面关闭 | 游戏库不解析/下载封面，表格封面列为空 |
| SET-005 | HAGS 快捷入口 | 打开 `ms-settings:display-advancedgraphics-default`（Win11）或 `ms-settings:display-advancedgraphics`（Win10）；不写注册表 |
| SET-006 | 窗口背景 | 使用 `WindowBackdropType=None`；Mica 在本机启动时会导致内容不绘制 |
| SET-007 | 切换语言 | 立即更新已绑定文案；已生成的动态状态在下次刷新/操作后更新 |
| SET-008 | 语言为 `system` | 启动时读取 `CultureInfo.InstalledUICulture`；`zh` 使用简体中文，其他使用 English |

## Steam 封面

| 编号 | 条件 | 行为 |
|---|---|---|
| COVER-001 | 缓存 `<cache>\covers\<appid>.jpg` 存在且 >1 KB | 直接使用 |
| COVER-002 | 安装目录向上 6 层内找到 `steamapps` | 以父目录为 Steam 根 |
| COVER-003 | `userdata\*\config\grid\<appid>_library_600x900.jpg` 存在 | 选择最新修改的一张 |
| COVER-004 | 本地无网格封面 | 依次尝试 `appcache\librarycache` 的竖版/header/hero |
| COVER-005 | 本地仍无且 appid 全为数字 | 从 Steam CDN 三个主机依次下载 2x 竖版 → 竖版 → header |
| COVER-006 | 下载内容 <1 KB、>8 MB 或不是 JPEG 魔数 | 丢弃，不写缓存 |
| COVER-007 | 所有封面来源失败 | 提取游戏 EXE 关联图标并缓存为 PNG，作为封面兜底 |
| COVER-008 | 图标也提取失败 | 返回 `null`，界面显示首字母占位 |

## 已知限制

- 只有 Steam 游戏有封面；Epic/WeGame/散装仍为空。
- 语言切换不会重建已经显示的游戏状态文本，需要刷新或重新进入页面。
