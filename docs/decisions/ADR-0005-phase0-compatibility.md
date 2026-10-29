# ADR-0005：Phase 0 兼容性验证结论

- 状态：已接受
- 日期：2026-10-29

## 验证环境

- Windows 10/11 x64（当前机器）
- .NET SDK **10.0.403**（用户目录安装）
- WPF / `net10.0-windows`

## 验证版本

| 组件 | 版本 | 目标框架资产 |
|---|---|---|
| Wpf.Ui | 4.3.0 | `net10.0-windows7.0` |
| CommunityToolkit.Mvvm | 8.4.2 | `netstandard2.0` / `net8.0` |
| Velopack | 1.2.161 | `net10.0` |
| Serilog.Extensions.Hosting | 10.0.0 | `net8.0` / `net9.0` |
| xunit.v3 | 4.0.2 | `net8.0`（在 net10.0 上运行正常） |
| Shouldly | 4.3.0 | `netstandard2.0` |
| Microsoft.Testing.Platform | 2.5.1（传递依赖） | — |

## 结果

- `dotnet build`：Debug / Release 均 **0 warning / 0 error**（warnings-as-errors 开启）。
- WPF 空壳：
  - `FluentWindow` + `TitleBar` + `ThemesDictionary` + `ControlsDictionary` 编译通过。
  - 进程启动后持续运行 8 秒，未异常退出；Serilog 日志无错误。
- Velopack：
  - `VelopackApp.Build().Run()` 在 `Program.Main` 首行执行，未安装状态下启动正常。
  - 完整安装/更新流程留到 Phase 5 验证。
- 测试：
  - xUnit v3 + MTP：`dotnet test` 跑通 7 个测试，0 失败。
  - 需要在 `global.json` 保留 `"test": { "runner": "Microsoft.Testing.Platform" }`。

## 结论

上述版本组合作为 2.0 基线，全部锁定在 `Directory.Packages.props`。后续升级必须通过 Dependabot PR + 完整 `build/verify.ps1`，并单独提交。

## 已知事项

- WPF 窗口外观（Mica/亚克力）依赖 Windows 11；Windows 10 上会降级，不影响功能。
- 本机 SDK 安装在用户目录，需要 `DOTNET_ROOT` 和 `PATH`；CI 使用 `actions/setup-dotnet`，不受影响。
- 代码签名尚未配置，SmartScreen 提示留到发布阶段处理。
