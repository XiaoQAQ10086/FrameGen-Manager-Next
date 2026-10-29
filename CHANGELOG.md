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
- 便携版发布流程：自包含 win-x64、`portable.flag`、`data\` 数据目录、zip 输出。
- 行为规格：路径安全、INI、资产包。
- 112 个单元/集成测试。

### Notes

- 不读取或迁移旧版数据。
- 旧仓库和旧版更新源保持不变。
- 安装包（Velopack）暂缓；先交付便携版验收。
- 业务模块（游戏库、部署引擎、SM、DLSS5、更新）尚未实现。
