# FrameGen Manager-Next

[![CI](https://github.com/XiaoQAQ10086/FrameGen-Manager-Next/actions/workflows/ci.yml/badge.svg)](https://github.com/XiaoQAQ10086/FrameGen-Manager-Next/actions/workflows/ci.yml)

FrameGen Manager-Next 是 FrameGen Manager 的从零重写版：Windows 桌面端帧生成部署管理器，使用 **C# / .NET 10 LTS + WPF + Wpf.Ui**，单进程架构。

> 当前状态：**Phase 5 功能完成，等待真实游戏验收**。游戏库（Steam 封面）、GPU 状态与显卡名称伪装、日志诊断、统一部署引擎、FrameGen、Smooth Motion、DLSS5（含彻底卸载/旧版 RenoDX 清理）、设置页、中英文切换、浅色界面、CLI 已完成；剩余真实游戏对照和安装包（等便携版验收后做）。

## 仓库

- 新仓库：<https://github.com/XiaoQAQ10086/FrameGen-Manager-Next>
- 旧仓库：<https://github.com/XiaoQAQ10086/FrameGen-Manager>（只读保留，旧版更新源不受影响）

## 这个程序做什么

- 扫描 Steam / Epic / WeGame / 手动添加的游戏库，Steam 游戏显示本地或缓存封面。
- 把帧生成代理 DLL、运行时和配置部署到游戏渲染目录，并支持安全还原。
- 管理 Smooth Motion、DLSS5 / ReShade 等可选组件，支持彻底卸载和旧版 RenoDX 清理。
- “帧生成”页左侧显示 GPU、驱动、HAGS 和显卡名称伪装，右侧是游戏列表；点“部署”后才弹出该游戏的代理/优化/帧数等参数。
- 设置页：启动自动扫描、封面开关、数据目录入口、HAGS 系统设置快捷入口。
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
dotnet restore FrameGenManagerNext.slnx
dotnet build FrameGenManagerNext.slnx -c Debug
dotnet test  FrameGenManagerNext.slnx -c Debug

dotnet run --project src/FrameGenManagerNext.Cli -- --version
dotnet run --project src/FrameGenManagerNext.App
```

无 UI 验收命令：

```powershell
fgm-next.exe gpu
fgm-next.exe gpu spoof inspect
fgm-next.exe gpu spoof apply "NVIDIA GeForce RTX 5090"
fgm-next.exe gpu spoof restore driver
fgm-next.exe library list [--json]
fgm-next.exe library scan [--json]
fgm-next.exe logs tail --lines 200
fgm-next.exe deploy inspect <target-dir>
fgm-next.exe deploy framegen <target-dir> [--proxy version.dll]
fgm-next.exe deploy restore <target-dir>
fgm-next.exe sm inspect <target-dir> <game-exe>
fgm-next.exe sm enable <target-dir> <game-exe>
fgm-next.exe sm disable <target-dir> <game-exe>
fgm-next.exe sm restore <target-dir> <game-exe>
fgm-next.exe dlss5 inspect <target-dir> <game-exe>
fgm-next.exe dlss5 install <target-dir> <game-exe>
fgm-next.exe dlss5 restore <target-dir>
fgm-next.exe dlss5 uninstall <target-dir> [game-exe]
```

完整门禁（格式 + 构建 + 测试 + 结构检查）：

```powershell
pwsh ./build/verify.ps1 -Configuration Debug
# 没有 PowerShell 7 时：
powershell -NoProfile -ExecutionPolicy Bypass -File ./build/verify.ps1 -Configuration Debug
```

## 便携版（测试用）

```powershell
pwsh ./build/publish-portable.ps1 -Configuration Release
```

输出：

- `artifacts/portable/FrameGen-Manager-Next-2.0.0-win-x64/`（直接打开该文件夹测试，不再打 zip）

便携版带 `portable.flag`，数据写到程序目录下的 `data\`，不写注册表。  
当前便携版内置 FrameGen、Smooth Motion、DLSS5 全部资产包；显卡名称伪装通过同目录的 `fgm-next-elevated.exe` 提权执行。  
**安装包暂不生成**；等便携版验收通过后再做 Velopack 安装器。

## 资产包

```powershell
pwsh ./build/generate-packs.ps1
fgm-next pack validate assets/packs/framegen.sm86
```

- 清单：`packs/<id>/pack.json`（提交到 git）
- payload：`assets/packs/<id>/`（不进 git）
- 详见 [packs/README.md](packs/README.md)

## 目录结构

```
src/
  FrameGenManagerNext.Domain/          纯领域模型、规则、错误码、资产包协议
  FrameGenManagerNext.Core/            用例服务 + 端口接口
  FrameGenManagerNext.Infrastructure/  Windows / 文件 / 网络 / NVAPI / ReShade 实现
  FrameGenManagerNext.App/             WPF（View + ViewModel）
  FrameGenManagerNext.Cli/             无 UI 验收入口
  FrameGenManagerNext.Elevated/        显卡名称伪装提权助手（只接受白名单参数）
tests/                          单元、基础设施、集成测试
packs/                          资产包清单（提交）
assets/                         本地资产与 pack payload（不进 git）
docs/                           architecture / behavior / decisions / roadmap
build/                          质量门禁、打包与资产包生成脚本
```

依赖方向：`Domain ← Core ← Infrastructure`，`App → Core + Infrastructure`（只在组合根引用 Infrastructure）。

## 数据目录

默认：

```
%LOCALAPPDATA%\FrameGen-Manager-Next\
  config\  library\  packs\  backups\  logs\  cache\  state\
```

便携版使用 exe 同目录的 `data\`。

测试或便携场景可用环境变量覆盖：

```powershell
$env:FGM_DATA_DIR = "D:\FrameGenManagerNextData"
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
- [CONTRIBUTING.md](CONTRIBUTING.md)（构建、测试、架构和提交规范）
