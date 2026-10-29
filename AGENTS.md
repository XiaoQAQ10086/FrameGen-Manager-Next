# AGENTS.md

本文件是给所有 AI/人类协作者的硬约束。违反其中任何一条，任务视为未完成。

## 1. 项目定位

FrameGen Manager-Next 是 Windows 独占的帧生成部署管理器，使用 **C# / .NET 10 LTS + WPF**，进程内单进程架构。

当前旧项目 `D:\AI Work\FrameGen Manager` **只读**，是新版的行为对照（oracle），不是代码来源。

## 2. 不可违反的规则

1. **只使用最新稳定版**。不使用 preview / RC NuGet 包；版本必须写进 `Directory.Packages.props`，禁止在 csproj 里写 `Version=`。
2. **不再引入 Rust / Electron / Node / Python 运行时**。核心是 C# 类库，不是 sidecar，不是 IPC 服务。
3. **不复制旧代码**。旧项目是 Rust/JS，新版是 C#；需要移植的是规则、测试场景和踩坑经验，写进 `docs/behavior/` 后再实现。
4. **不修改旧项目**，不覆盖旧 GitHub 仓库，不改旧版更新源。
5. **Domain/Core 不引用 WPF / WinForms / Windows 桌面程序集**。UI 只能在 `FrameGenManagerNext.App`。
6. **P/Invoke / `LibraryImport` 只能出现在 `Infrastructure`**（`Platform/`、`NvApi/`、`Win32/`）。
7. **Core/Domain 不返回用户可见中文/英文字符串**。返回 `ErrorCode` + 结构化参数；UI 用 resx 渲染。
8. **禁止硬编码绝对路径**（`D:\...`、`C:\Users\...`）。所有路径从 `AppPaths` 或注入的端口取。
9. **所有写操作必须可取消、可回滚、串行化**。走 `OperationRunner`，不要直接并发写游戏目录。
10. **文件软上限 400 行，CI 硬上限 500 行**；一个文件一个公开类型。
11. **一个任务只改一个功能文件夹 + 对应测试**。跨模块改动必须拆任务。
12. **新增 NuGet 依赖必须写 ADR**；没有 ADR 的依赖不接受。

## 3. 构建与测试

```powershell
dotnet restore FrameGenManagerNext.slnx
dotnet format FrameGenManagerNext.slnx --verify-no-changes
dotnet build FrameGenManagerNext.slnx -c Debug --no-restore
dotnet test  FrameGenManagerNext.slnx -c Debug --no-build
pwsh ./build/check-quality.ps1
```

- 本机 SDK 可能装在 `%LOCALAPPDATA%\Microsoft\dotnet`；如果 `dotnet` 不在 PATH，重开终端或设置：
  - `DOTNET_ROOT=%LOCALAPPDATA%\Microsoft\dotnet`
  - 把该目录加入 `PATH`
- 测试使用 **xUnit v3 + Microsoft.Testing.Platform**。`global.json` 里的 `"test.runner"` 不能删；.NET 10 SDK 已不支持 MTP 项目的 VSTest 模式。
- 交付前必须运行完整门禁：`pwsh ./build/verify.ps1`；没有 PowerShell 7 时用
  `powershell -NoProfile -ExecutionPolicy Bypass -File ./build/verify.ps1`。

## 4. 架构与目录

```
Domain  ←  Core  ←  Infrastructure
                 ↗
App / Cli  ─────┘（App 只在组合根引用 Infrastructure）
```

- `src/FrameGenManagerNext.Domain`：模型、规则、错误码、资产包协议。零外部依赖。
- `src/FrameGenManagerNext.Core`：用例服务、端口接口（`I...`）、结果类型。只依赖 Domain。
- `src/FrameGenManagerNext.Infrastructure`：所有 OS/网络/文件/NVAPI/ReShade 实现。
- `src/FrameGenManagerNext.App`：WPF View + ViewModel + 组合根。code-behind 只做纯 UI 转发。
- `src/FrameGenManagerNext.Cli`：无 UI 验收入口；新功能应先能在这里跑通。
- `tests/`：Domain/Core 单测、Infrastructure 单测、Integration 集成测试。

功能按垂直切片组织：

```
Core/Features/<Feature>/
  I<Feature>Service.cs
  <Feature>Service.cs
  Models/
```

删除一个功能 = 删除文件夹 + 删一行 DI 注册。

## 5. 资产包（Asset Pack）

上游文件布局变化**不允许改代码**。文件表、目标路径、代理槽、INI 模板和安装步骤都在 `pack.json` 里。

- 代码只实现通用步骤：`copyFiles`、`writeIni`、`runInstaller`、`removeStaleFiles`。
- Pack 必须通过校验器：路径穿越、哈希、大小、步骤类型、目标目录白名单。
- `runInstaller` 只允许受信任 pack；安装器哈希固定在代码里。
- 资产不进 git；发布为 Release 资产 + `packs.json` 索引 + 离线包。

## 6. 部署引擎

- 所有资产族（FrameGen / SmoothMotion / DLSS5）共用同一个 `BackupStore` 和部署状态机。
- 状态机：`planned → backingUp → writing → verifying → installed / rolledBack / partial`。
- 还原时只有“当前文件哈希 == 部署时哈希”才删除；用户改过的文件报告冲突。
- 路径安全统一走 `SafePath`；禁止自行拼接目标路径。
- 不做启动时自动清理旧部署；状态由每局游戏显式展示。

## 7. 错误与本地化

- Domain 定义 `ErrorCode` 枚举 + 参数对象。
- 日志用英文结构化输出，带 operation id。
- UI 文案只在 `FrameGenManagerNext.App` 的 resx 中；中英文运行时切换。
- 错误弹窗显示本地化文案 + 错误码 + 诊断复制按钮。

## 8. Definition of Done

每个任务必须同时满足：

- [ ] 对应的 `docs/behavior/<module>.md` 已更新
- [ ] 规则有单元测试；涉及文件系统/OS 的有集成测试或明确说明为什么没有
- [ ] 错误路径有 `ErrorCode`
- [ ] 日志含 operation id，且不打印敏感信息
- [ ] `dotnet format --verify-no-changes` 通过
- [ ] `pwsh ./build/verify.ps1` 全绿
- [ ] 没有新增硬编码路径、中文 UI 串进入 Core、超长文件

## 9. Git 与发布

- 新仓库：`FrameGen-Manager-Next`。旧仓库 `FrameGen-Manager` 不动。
- 分支：`main` 受保护；功能分支 `feat/<module>`，修复 `fix/<module>`。
- 提交信息：`<type>(<scope>): <summary>`，例如 `feat(deployment): add backup manifest state machine`。
- 版本：`2.0.0` 起步；程序与资产包版本独立。
- 发布：Velopack + GitHub Releases；`packId = FrameGenManagerNext`。
- 代码签名暂缓，但发布前必须校验 SHA-256。
