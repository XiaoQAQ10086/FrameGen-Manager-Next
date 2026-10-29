# Changelog

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
- 设置页：主题、启动自动扫描、Steam 封面开关、数据目录入口、HAGS 系统设置快捷入口。
- 设置持久化到 `config/app.json`，损坏文件隔离为 `.bad`。
- Steam 封面：本地 grid/librarycache 优先，缺失时从 Steam CDN 下载到 `cache/covers`。
- 修复窗口启动时使用 Mica 背景导致内容不绘制的问题，改用不透明背景。
- 中英文运行时切换：导航、页面、按钮、状态与确认框文案；语言设置持久化到 `config/app.json`。
- 主题资源改为可切换调色板：深色/浅色/跟随系统均可用；游戏库封面占位、英文列宽和页脚换行。

### Notes

- 不读取或迁移旧版数据。
- 旧仓库和旧版更新源保持不变。
- 安装包（Velopack）暂缓；先交付便携版验收。
- 真实游戏逐项对照和 UI 视觉打磨仍在进行。
