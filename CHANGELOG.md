# Changelog

## 2.1.9

### Added

- 已移除的游戏支持【删除记录】：只从游戏库删除该条目，不删除游戏文件和部署备份；游戏目录不存在时标记“游戏目录不存在”并禁用恢复。

### Fixed

- WebView2 禁用本地 WebAssets 的 HTTP 缓存，升级后不会继续加载上一版本的界面、脚本和样式。

## 2.1.8

### Fixed

- 修复安装完成后无法自动启动新版本的问题：安装程序通过 ShellExecute 启动主程序，由 UAC 正常提权，不再报“错误代码 740：请求的操作需要提升”。
- 卸载清理同样改为通过 ShellExecute 提权，避免卸载时出现 740 错误。

## 2.1.7

### Added

- 帧生成和 Smooth Motion 游戏卡片新增【启动游戏】按钮：Steam 走 `steam://rungameid`，Epic 走 `com.epicgames.launcher://`，手动/散装游戏直接运行所选 EXE；Xbox 游戏保持禁用。
- 主程序改为启动时请求管理员权限，可直接写入 `C:\Program Files` 等受保护目录，解决默认安装在 C 盘的游戏无法部署的问题。Xbox/商店游戏的 `WindowsApps` 仍按只读处理。

### Fixed

- DLSS5 安装或卸载后，帧生成卡片会立即刷新代理占用状态，不再残留 `dxgi.dll · DLSS5` 徽章。
- 更新安装包在运行前重新校验文件名、大小和 SHA-256，避免提权后执行被替换的安装包。

## 2.1.6

### Added

- 更新检查改为可镜像的 `update.json`：GitHub raw、`ghfast.top`、`gh-proxy.com`、`ghproxy.net` 回退；安装包下载强制校验大小和 SHA-256，失败自动换线。
- 更新弹窗新增【夸克网盘】和【百度网盘】按钮；关于页的“网盘下载”同步改名为“夸克网盘”。
- DLSS5 增加代理入口选择：默认项标“（自动）”，DX9 用 `d3d9.dll`、DX10/11/12 用 `dxgi.dll`；可手动覆盖但会检查与游戏 API 的兼容性。
- DLSS5 增加 ReShade 呼出键选择，默认 Home，写入 `ReShade.ini` 的 `[INPUT] KeyOverlay`。
- 新增 RE Engine 检测（`re_chunk_000.pak` 等标记）和 REFramework 可选组件；卡普空 RE Engine 游戏默认勾选，MT Framework 游戏不误装。REFramework 同时服务帧生成和 DLSS5。
- 部署 REFramework 前先检测已有安装：已装过 REFramework（含自更新版本）自动跳过并弹窗说明；其他 Mod 的 `dinput8.dll` 不再误判为 REFramework，会提示入口已被其他 Mod 占用并跳过自动安装。
- 帧生成游戏卡片直接标出代理占用：`dxgi.dll · DLSS5`、`dinput8.dll · Reframework` 等徽章与 Steam/Epic、API、部署状态排在同一行；DLSS5 与帧生成部署弹窗也会说明代理被哪个组件占用，占用项不可选。

### Fixed

- 还原时单个文件被游戏/杀毒软件占用或拒绝访问，不再中断整个还原；该文件会列入冲突或残留，其他文件继续处理。
- 只有警告或残留、没有实际冲突文件时，不再显示“强制还原”提示；强制还原入口只在存在冲突文件时出现。
- 强制还原可处理“原备份丢失”的文件，不再让部署记录永远停在 Partial。
- 还原日志记录具体的冲突文件、残留文件和警告内容，不再只有数量。

## 2.1.4 (unreleased)

### Fixed

- 还原时单个文件被游戏/杀毒软件占用或拒绝访问，不再中断整个还原；该文件会列入冲突或残留，其他文件继续处理。
- 只有警告或残留、没有实际冲突文件时，不再显示“强制还原”提示；强制还原入口只在存在冲突文件时出现。
- 强制还原可处理“原备份丢失”的文件，不再让部署记录永远停在 Partial。
- 还原日志记录具体的冲突文件、残留文件和警告内容，不再只有数量。

## 2.1.3 (unreleased)

### Fixed

- 帧生成还原失败时列出具体冲突文件，并提供“强制还原”：确认后删除这些文件，或恢复部署前的原文件，不需要用户手动去游戏目录删除。
- 冲突不再一律说成“用户修改”：ReShade、游戏、插件或另一个 Mod 在运行时改写文件也会造成哈希不一致。
- 加固 Win10 上安装后启动/关闭时的 `Cannot access a disposed object`：WebView2 事件解绑、窗口关闭后的 UI 回调、后台任务取消和信号量释放都处理已释放对象。

## 2.1.2 (unreleased)

### Fixed

- Smooth Motion 开启/关闭失败时不再只显示 `enable failed`：错误按错误码本地化，日志记录游戏、目录、错误码、诊断和上下文。
- Smooth Motion 不再硬拦截反作弊游戏：仍显示红色“反作弊”徽章和风险提示，但允许用户自行决定是否开启。
- 反作弊检测避免把 `BepInEx.dll` 误判为 BattlEye。
- DLSS5 旧版文件检测会列出具体文件名，并识别第三方 `renodx-dlss-*` 整合包；仅检测到旧版文件时按钮显示“清理旧版文件”，不再让人误以为本软件部署过 ReShade。

## 2.1.1

### Fixed

- Witcher 3 等只导入 `dxgi.dll` 的游戏，使用 `x64_dx12` 目录名补判 DX12。
- 扫描结果 API 为 Unknown 时，DLSS5 安装使用详情页选中的 API，不再强制自动检测失败。
- 扫描不到渲染 EXE 的游戏，在详情页手动选择 EXE 后可以安装 DLSS5，不再因为 `available=false` 永久锁死。

## 2.1.0

### Added

- 新增 Xbox/Microsoft Store 游戏扫描：识别 `XboxGames` 下的 `Content`/`MicrosoftGame.config`/`AppxManifest.xml`，并支持可移动磁盘；受保护目录只读展示，不绕过 ACL。

### Changed

- DLSS5 pack 更新到 `6.8.1`，内置 GITC Uplift `1.8.0`：更新 `gitc-uplift.addon64`、`addon32`、`gitc-uplift-helper64.exe` 和 `Uplift.fx`；模型与 ReShade 6.8.0 不变。
- 移除内置 WebView2 运行时，应用使用系统安装的 WebView2。
- 保留 .NET 10 CET 关闭和启动失败日志。
- 安装器使用 Inno Setup 6.7.4 + 64 位 Setup Loader。

## 2.0.10 (unreleased)

### Fixed

- 关闭 .NET 10 apphost 的 CET 兼容位。未完全更新的 Windows 10 22H2（KERNELBASE 19041.1949）会因 CET 检查在 CoreCLR 启动阶段以 `0x80131506` 崩溃，应用无日志、无窗口。
- 日志收集器支持自定义安装目录（读取 Inno 的 InstallLocation/App Path/DisplayIcon/UninstallString），并收集 WER `Report.wer`、CPU 和 KERNELBASE 版本。

## 2.0.9 (unreleased)

### Changed

- 应用目录内置固定版 WebView2 x64 运行时，优先使用它启动，不再依赖用户系统里的 Edge WebView2。
- 新增 `collect-fgm-logs.cmd`，即使主程序打不开也能把系统、WebView2、安装文件和日志收集到桌面 zip。
- 启动失败时写入 `startup-failure.log` 并显示日志路径；单实例冲突不再静默退出。

## 2.0.8 (unreleased)

### Changed

- 安装包改用 Inno Setup 6.7.4，并启用 64 位 Setup Loader，避免部分 Windows 10 22H2 用户双击安装包无响应或出现 “Unknown Hard Error”。

## 2.0.7 (unreleased)

### Changed

- 非 Vulkan 的 DLSS5 / ReShade 恢复默认离线解包安装，避免官方 setup 每次 60–100 秒的联网等待；离线失败时才自动回退官方 setup，Vulkan 仍然只能使用官方 setup。
- 界面不提供手动选择官方安装器的入口。

### Fixed

- DLSS5 自动模式对 DX10/11/12 改用 `dxgi.dll`，与旧项目一致；此前自动检测 DX12 时误写 `d3d12.dll`，可能导致部分游戏（如永劫无间）无法启动。显式选择 API 时仍使用该 API 自己的代理。
- 彻底卸载会清理所有检测到的 ReShade 代理模块，而不只清理一个。

## 2.0.6 (unreleased)

### Changed

- DLSS5 / ReShade 恢复使用官方 ReShade setup 安装；官方安装失败时才回退到内置 DLL 解包（Vulkan 只能走官方安装器）。

### Fixed

- 彻底卸载会按 pack 文件表清理 `reshade-shaders` 下遗留的 shader 与纹理；仍有残留时如实报告，不再提前丢弃部署清单。
- 卸载后不再因为残留文件停留在“已部署（无记录）”并锁死安装按钮；该状态下允许重新部署。
- 安装或卸载结束后关闭全局进度提示，不再残留“正在安装 ReShade”。

## 2.0.5 (unreleased)

### Fixed

- Steam 校验或手动恢复把游戏文件还原成原版后，还原流程不再误判为“文件冲突”，会正常完成还原。

## 2.0.4 (unreleased)

### Fixed

- 还原帧生成时若文件被游戏或用户修改，明确提示“还原未完成”和冲突数量，不再误报还原成功。

## 2.0.3 (unreleased)

### Fixed

- 修复部署目标下拉框滚动条无法拖动、点击后菜单关闭的问题。

## 2.0.2 (unreleased)

### Fixed

- 显卡型号伪装卡片中的驱动记录名称过长时溢出徽章，现在会自动换行并保持在卡片内。
- 还原帧生成时若游戏进程未完全退出，显示本地化的失败原因而不是英文诊断文本。
- 修正 `Program Files\Steam\steamapps\common\...` 等游戏目录被误判为受保护系统目录的问题。
- 帧生成部署被 Smooth Motion 锁定时，卡片上直接显示红色原因提示。

### Changed

- 安装器改为 Inno Setup，安装向导允许用户选择安装目录。
- 更新下载完成后静默安装到当前目录并重新启动程序。
- 安装器会清理 2.0.1 Velopack 安装目录、快捷方式和注册项，但保留本地游戏库、配置和备份数据。
- 安装/卸载界面跟随 Windows 显示语言；中文系统显示中文，其他语言显示英文。
- 升级时先按旧安装清单清空旧目录，再写入新版本，避免旧文件残留。
- 安装包文件名带版本号，发布目录只保留最新安装包。

## 2.0.1 (unreleased)

### Fixed

- 扫描游戏库时正确释放 DRS 会话并调用 `NvAPI_Unload`，修复连续扫描内存持续增长。
- Smooth Motion 页面恢复 NVAPI 状态读取，不再显示接口导出错误。
- 帧生成与 Smooth Motion 列表重渲染时保留滚动位置，部署后不再跳回顶部。
- 帧生成/SM 页滚动条改为滚动时显示、停止后淡出。
- 语言默认跟随 Windows 显示语言，旧设置迁移到 `system`。
- 诊断包导出改为系统“另存为”对话框，取消或完成后清理临时文件。
- 安装版更新下载完成后自动退出并启动版本化安装包。
- 显卡型号伪装确认框改为应用内模态框：目标型号徽章展示，关键警告红色显示。

## 2.0.0 (unreleased)

### Added

- 从零搭建 .NET 10 / WPF / Wpf.Ui 单进程架构。
- 五层项目结构：Domain / Core / Infrastructure / App / Cli。
- 中央 NuGet 版本管理、Nullable、分析器、warnings-as-errors。
- Serilog 文件日志、`AppPaths` 数据目录、DI 组合根。
- xUnit v3 + Microsoft.Testing.Platform 测试基础设施。
- CLI `--version` / `--paths`。
- CI 与本地质量门禁：格式、构建、测试、文件行数、分层、硬编码路径、Core 本地化字符串检查。
- 架构文档、行为规格模板、ADR 和路线图。

### Phase 1

- 领域错误模型：`ErrorCode`、`AppError`、`Result<T>`。
- 路径安全：`SafePath`，覆盖绝对路径、`..`、UNC、ADS、保留设备名、系统目录。
- INI 解析/编辑/stock 覆盖：保留注释和未知行，大小写不敏感。
- 资产包协议：`pack.json` schema、严格 JSON 读取、payload 大小/SHA-256/INI 模板/安装器校验。
- 四个真实 pack：FrameGen、Smooth Motion RTX30、Smooth Motion RTX20、DLSS5/ReShade。
- CLI：`pack validate <dir> [--no-hash] [--schema-only]`。
- 便携版发布流程：自包含 win-x64、`portable.flag`、`data\` 数据目录、纯文件夹输出。
- 行为规格：路径安全、INI、资产包。
- 112 个单元/集成测试。

### Phase 2

- GPU 读取：nvidia-smi 名称/驱动、HAGS 注册表、旧版路由表与 R580 / 616.64 闸门。
- 游戏库：Steam（多库 VDF/appmanifest）、Epic 清单、WeGame 尽力扫描、固定盘散装目录、手动 EXE。
- 渲染 EXE：旧版权重评分与排除表；无信号时返回未识别。
- 图形 API：PE 导入 + 文件名 token + DXVK wrapper + Vulkan/DX12 sibling + Unity/Unreal/CryEngine 回退。
- `library.json` 缓存、忽略列表、手动条目、损坏隔离。
- WPF 外壳与四页：游戏库、GPU 状态、日志与诊断、关于。
- CLI：`gpu`、`library list/scan [--json]`、`logs tail [--lines N]`。
- 行为规格：`library.md`、`gpu.md`。
- 真机验收：7 个游戏，API 与旧版一致；GPU RTX 3070 / SM86。

### Phase 3

- 统一部署引擎：部署计划、跨进程文件锁、原件备份、原子写入、哈希校验、manifest 状态机。
- 备份存储：按目标路径哈希分目录，支持重复部署复用原始备份。
- 安全还原：只删除仍匹配部署哈希的文件；用户修改保留并报告冲突；恢复原始备份。
- 状态检测：未部署 / 已部署 / 需更新 / 不完整 / 文件冲突 / 已安装无记录。
- FrameGen 选项与 INI：代理槽、优化档、帧数、Preset、引擎、SR 模式、XeSS flow/HUD-less。
- CLI：`deploy inspect|framegen|restore`。
- 游戏库 UI：部署状态列、部署/还原按钮、进度与确认。
- 便携版内置 FrameGen 资产包。
- 行为规格：`deployment.md`；测试增至 196 个。

### Phase 4（Smooth Motion）

- NVAPI DRS 适配器：动态加载 `nvapi64.dll`，查找/创建 profile，读写设置 `0xB0D384C0`，保存并用新会话读回复核。
- Smooth Motion 组件部署/还原：RTX 30 单文件、RTX 20 三件套，接入统一部署引擎。
- Smooth Motion 页面与 CLI：`inspect` / `enable` / `disable` / `restore`。
- FrameGen 完整选项 UI/CLI：代理槽、优化档、帧数、Preset、引擎、SR、XeSS flow/HUD-less。
- 真机验证：RTX 3070 + 621.01，开启读回 1、关闭读回 0、还原后组件移除。
- 测试增至 197 个；`smooth-motion.md` 行为规格。

### Phase 4（DLSS5 核心）

- 图形 API 探测接入 DLSS5：DX9/10/11/12、Vulkan、OpenGL 映射到 ReShade 参数。
- ReShade 安装器：哈希校验、`--headless --api <api>`、240 秒超时、stdout/stderr 捕获、失败回滚。
- 114 个 pack 文件部署：addon/helper/model/shaders/effects；`ReShade.ini` 搜索路径写入。
- 状态检查与还原；ReShade 模块/INI 保留并提示。
- CLI：`dlss5 inspect|install|restore`。
- 真机验证：VTube Studio 临时副本，ReShade 安装成功、114 个文件部署、还原无冲突。
- DLSS5 页面：逐游戏安装/还原，带 GPU 路由闸门；便携版改为纯文件夹输出。

### Phase 4（DLSS5 卸载）

- 检测 ReShade 模块（文件版本信息）与旧版 RenoDX 残留；检测到旧版时阻止安装并返回 `LegacyInstallPresent`。
- `dlss5 uninstall`：部署引擎还原、官方 ReShade 卸载器（`--state uninstall`）、直接删除回退。
- 清理 GITC 组件、已知 shader、旧版 addon/model、插件运行时残渣、Agility SDK `D3D12Core.dll`。
- 删除前需用户在 UI 二次确认；返回已删除与仍被占用的文件列表。
- DLSS5 页面增加卸载按钮；夹具验证剩余 0。

### Phase 5（系统集成）

- 显卡名称伪装：固定型号白名单、`DeviceDesc` 单值修改、备份与读回校验、驱动名/原始值两种还原。
- 独立提权助手 `fgm-next-elevated.exe`：只接受 `--gpu-apply` / `--gpu-restore` 白名单参数，结果写临时 JSON。
- GPU 页面伪装卡片：型号选择、风险提示、UAC 提权、状态刷新。
- 设置页：启动自动扫描、Steam 封面开关、数据目录入口、HAGS 系统设置快捷入口。
- 设置持久化到 `config/app.json`，损坏文件隔离为 `.bad`。
- Steam 封面：本地 grid/librarycache 优先，缺失时从 Steam CDN 下载到 `cache/covers`。
- 修复窗口启动时使用 Mica 背景导致内容不绘制的问题，改用不透明背景。
- 中英文运行时切换：导航、页面、按钮、状态与确认框文案；语言设置持久化到 `config/app.json`。
- 界面固定浅色（不做深色适配）；游戏库封面占位、英文列宽和页脚换行。
- 布局按旧版重构：GPU 状态与显卡伪装并入“帧生成”左侧栏，移除独立 GPU 导航；部署参数改为点击游戏“部署”后弹出的确认面板。
- 浅色 Apple 风格调色板与字体：系统蓝强调色、圆角卡片、页面淡入/位移和弹出面板缩放动效。
- 游戏列表改为卡片行，操作按钮固定间距，避免表头挤压和按钮重叠；字号与对比度整体上调。
- WebView2 全 UI：本地 HTML/CSS/JS + 模块化 C# 消息桥；帧生成、Smooth Motion、DLSS5、日志、设置、关于全部迁移；删除旧 WPF 页面/ViewModel/主题服务。
- 顶部横向导航替代左侧栏并右对齐，压缩窗口标题栏；扫描按钮改为绿色；EPIC/无 Steam 封面的游戏使用 EXE 图标兜底。
- 收窄 GPU 侧栏、放大游戏卡片与路径显示；增加导航滑动指示、扫描 loading、封面淡入、进度流光和 reduced-motion。
- 页面切换、导航指示器、卡片悬停/错峰入场、弹层模糊与缩放动效。

### Notes

- 不读取或迁移旧版数据。
- 旧仓库和旧版更新源保持不变。
- 安装包（Velopack）暂缓；先交付便携版验收。
- 真实游戏逐项对照和 UI 视觉打磨仍在进行。
