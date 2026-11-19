# ADR-0016：原生文件选择对话框依赖

- 状态：已接受
- 日期：2026-11-19
- 范围：Phase 4，ANR 压缩包导入

## 背景

ANR 支持导入本地 full/addon zip。Flutter 没有内置 Windows 文件选择器；让用户手输路径不可接受，自己实现 COM `IFileOpenDialog` 又会引入大量 unsafe 和生命周期管理。

## 决策

- 新增 `rfd`（Rust File Dialog）作为 `fgm-infra` 依赖，仅用于打开原生文件选择框。
- 只暴露窄接口：选择 `.zip` 文件，返回 `Option<PathBuf>`；取消返回 `None`。
- 对话框调用在 Flutter isolate 中执行，避免阻塞 UI 线程。
- 不引入文件选择、拖放或 shell 集成的其它能力。

## 后果

- 构建增加 `rfd` 及其 Windows 对话框依赖；运行时使用系统对话框，不打包额外 UI。
- FFI 只新增一个 `fgm_pick_anr_zip_json`，参数和返回值都是简单 JSON。
- 如果未来需要目录选择或多文件选择，再扩展该接口，不在此 ADR 预先设计。
