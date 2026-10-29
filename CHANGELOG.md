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

### Notes

- 不读取或迁移旧版数据。
- 旧仓库和旧版更新源保持不变。
- 业务模块（游戏库、部署引擎、SM、DLSS5、更新）尚未实现。
