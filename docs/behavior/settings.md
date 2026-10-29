# 设置与封面行为规格

来源：旧版 `ui/settings.js`、`ui/app.js` 的封面/设置部分。
新实现：`Domain/Settings`、`Infrastructure/Settings`、`Infrastructure/Library/SteamCoverService.cs`。

## 设置文件

- 位置：`<数据目录>\config\app.json`。
- 字段：`schemaVersion`、`language`、`theme`、`autoScanOnStartup`、`showCovers`。
- 读取失败或 JSON 损坏：隔离为 `app.json.bad`，使用默认值。
- 写入：临时文件 + 原子替换；未知枚举值回退到默认。

默认值：

| 字段 | 默认 |
|---|---|
| language | `chineseSimplified` |
| theme | `dark` |
| autoScanOnStartup | `false` |
| showCovers | `true` |

## 行为

| 编号 | 条件 | 行为 |
|---|---|---|
| SET-001 | 启动时读取主题 | `system` 解析系统明暗；`dark` / `light` 直接应用 |
| SET-002 | 保存设置 | 立即写盘并应用主题；页面显示“设置已保存” |
| SET-003 | 自动扫描开启且游戏库为空 | 启动加载缓存后执行一次扫描 |
| SET-004 | 封面关闭 | 游戏库不解析/下载封面，表格封面列为空 |
| SET-005 | HAGS 快捷入口 | 打开 `ms-settings:display-advancedgraphics-default`（Win11）或 `ms-settings:display-advancedgraphics`（Win10）；不写注册表 |
| SET-006 | 窗口背景 | 使用 `WindowBackdropType=None`；Mica 在本机启动时会导致内容不绘制 |

## Steam 封面

| 编号 | 条件 | 行为 |
|---|---|---|
| COVER-001 | 缓存 `<cache>\covers\<appid>.jpg` 存在且 >1 KB | 直接使用 |
| COVER-002 | 安装目录向上 6 层内找到 `steamapps` | 以父目录为 Steam 根 |
| COVER-003 | `userdata\*\config\grid\<appid>_library_600x900.jpg` 存在 | 选择最新修改的一张 |
| COVER-004 | 本地无网格封面 | 依次尝试 `appcache\librarycache` 的竖版/header/hero |
| COVER-005 | 本地仍无且 appid 全为数字 | 从 Steam CDN 三个主机依次下载 2x 竖版 → 竖版 → header |
| COVER-006 | 下载内容 <1 KB、>8 MB 或不是 JPEG 魔数 | 丢弃，不写缓存 |
| COVER-007 | 所有来源失败 | 返回 `null`，界面显示空封面；不影响游戏库扫描 |

## 已知限制

- 只有 Steam 游戏有封面；Epic/WeGame/散装仍为空。
- 语言字段已持久化，但界面中英文切换尚未完成。
