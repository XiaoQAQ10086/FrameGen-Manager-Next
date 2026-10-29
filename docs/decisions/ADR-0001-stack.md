# ADR-0001：C# / .NET 10 + WPF 单进程

- 状态：已接受
- 日期：2026-10-29

## 背景

旧版是 Rust core + 2783 行单文件 sidecar + Electron/原生 JS UI。主要问题：

- 两个进程、自研 JSON 行协议、超时与重启逻辑。
- 资产拷贝、备份还原、迁移逻辑在 Rust 和 JS 各有一份。
- 核心逻辑难以脱离 UI 测试。
- 多个 agent 接手后目录和命名混乱。

## 决策

使用 **C# / .NET 10 LTS**，核心层为普通类库，UI 为 **WPF + Wpf.Ui**，单进程，不保留 Rust sidecar。

## 理由

- Windows 独占且重度使用 Win32 / 注册表 / NVAPI / 文件锁 / UAC，C# 的 P/Invoke 和 Windows API 支持成熟。
- 单进程消除 IPC、sidecar 生命周期和跨语言数据拷贝。
- 强类型 + DI + 接口 + xUnit，核心可测试。
- WPF 在 .NET 10 上稳定，Wpf.Ui 提供现代外观；WinUI 3 的打包/身份模型会引入新复杂度。
- 性能足够：应用是 IO 密集型，不做实时渲染。

## 备选方案

- **Rust core + C# UI（C ABI / FFI）**：能复用旧逻辑，但保留两套工具链和 FFI 编组，旧代码仍需重构。
- **Rust + Tauri/Electron**：旧项目已经证明维护成本高。
- **C# + WinUI 3**：外观现代，但打包和运行时依赖更复杂。
- **C# + Avalonia**：只有在明确需要跨平台时才考虑。

## 后果

- 需要重写 13.6k 行 Rust 和 3.5k 行 JS 的行为，但用行为规格和旧版对照测试控制风险。
- 所有 OS 调用必须收敛到 Infrastructure，Core 通过端口接口访问。
- 发布体积和启动速度优于 Electron。
