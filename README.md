# FrameGen Next

FrameGen Next 是 FrameGen Manager 的从零重写版：Windows 桌面端帧生成部署管理器，使用 **C# / .NET 10 LTS + WPF + Wpf.Ui**，单进程架构。

> 当前状态：**Phase 0（基础脚手架）本地部分已完成**。UI 空壳、DI、日志、测试与 CLI 已跑通；GitHub 仓库待创建，业务模块尚未迁移。

## 这个程序做什么

- 扫描 Steam / Epic / WeGame / 手动添加的游戏库。
- 把帧生成代理 DLL、运行时和配置部署到游戏渲染目录，并支持安全还原。
- 管理 Smooth Motion、DLSS5 / ReShade 等可选组件。
- 显示 GPU、驱动和兼容性状态。
- 导出日志与诊断包。

它不是帧生成渲染器：真正的帧生成由游戏、NVIDIA 运行时或 ReShade 执行。

## 技术栈

| 层 | 选型 |
|---|---|
| 运行时 | .NET 10 LTS（`net10.0-windows`） |
| UI | WPF + [Wpf.Ui](https://github.com/lepoco/wpfui) + CommunityToolkit.Mvvm |
| 组合 | Microsoft.Extensions.Hosting / DI / Options |
| 日志 | Serilog（滚动文件） |
| 测试 | xUnit v3 + Microsoft.Testing.Platform + Shouldly |
| 更新 | Velopack |
| 打包 | `dotnet publish` + `vpk` |

所有 NuGet 版本集中在 [`Directory.Packages.props`](Directory.Packages.props)，只使用最新稳定版，不使用 preview。

## 环境要求

- Windows 10/11 x64
- .NET 10 SDK **10.0.403 或更新**（`global.json` 锁定 10.0.4xx 特性带）
- 本机如果通过 `dotnet-install.ps1` 安装到用户目录，需要保证：
  - `%LOCALAPPDATA%\Microsoft\dotnet` 在 `PATH` 中
  - `DOTNET_ROOT` 指向同一目录

检查：

```powershell
dotnet --version
dotnet --list-sdks
```

## 构建与运行

```powershell
dotnet restore FrameGenNext.slnx
dotnet build FrameGenNext.slnx -c Debug
dotnet test  FrameGenNext.slnx -c Debug

dotnet run --project src/FrameGenNext.Cli -- --version
dotnet run --project src/FrameGenNext.App
```

完整门禁（格式 + 构建 + 测试 + 结构检查）：

```powershell
pwsh ./build/verify.ps1 -Configuration Debug
# 没有 PowerShell 7 时：
powershell -NoProfile -ExecutionPolicy Bypass -File ./build/verify.ps1 -Configuration Debug
```

## 目录结构

```
src/
  FrameGenNext.Domain/          纯领域模型、规则、错误码、资产包协议
  FrameGenNext.Core/            用例服务 + 端口接口
  FrameGenNext.Infrastructure/  Windows / 文件 / 网络 / NVAPI / ReShade 实现
  FrameGenNext.App/             WPF（View + ViewModel）
  FrameGenNext.Cli/             无 UI 验收入口
tests/                          单元、基础设施、集成测试
docs/                           architecture / behavior / decisions / roadmap
build/                          质量门禁与构建脚本
assets/                         本地资产（不进 git）
```

依赖方向：`Domain ← Core ← Infrastructure`，`App → Core + Infrastructure`（只在组合根引用 Infrastructure）。

## 数据目录

默认：

```
%LOCALAPPDATA%\FrameGenNext\
  config\  library\  packs\  backups\  logs\  cache\  state\
```

测试或便携场景可用环境变量覆盖：

```powershell
$env:FGM_DATA_DIR = "D:\FrameGenNextData"
```

## 与旧项目的关系

- 旧项目目录 `D:\AI Work\FrameGen Manager` 和旧 GitHub 仓库**只读**，不再修改。
- 旧版自动更新依赖旧仓库 `main/Cargo.toml`，因此**绝不覆盖旧仓库 main**。
- 新项目从零重写，但会把旧版行为规则和测试场景整理进 [`docs/behavior`](docs/behavior/README.md)。
- 旧数据不迁移：新版使用独立数据目录，与旧版并存。

## 文档

- [架构说明](docs/architecture.md)
- [行为规格](docs/behavior/README.md)
- [架构决策](docs/decisions)
- [路线图](docs/roadmap.md)
- [AGENTS.md](AGENTS.md)（面向 AI/协作者的硬约束）
