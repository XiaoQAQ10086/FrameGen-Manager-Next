# ADR-0012：Rust 核心 + Flutter 界面重构

- 状态：已接受
- 日期：2026-11-19
- 取代：ADR-0001（C# / .NET 10 + WPF）

## 背景

现有 2.x 实现已完成功能验证，但技术栈为 C# / .NET + WPF + WebView2。现在决定保持现有布局和功能不变，整体迁移到 Rust + Flutter，并要求：

- 结构规范、解耦清晰，便于长期维护和新功能扩展
- 尽可能轻量，不引入 sidecar、Node、Electron 或 WebView2
- Rust 与 Flutter 都使用当前最新版本，允许非稳定版
- 公开 README 不再描述技术栈或 C# 实现

## 决策

1. **核心使用 Rust**：Domain、Core、Infrastructure、CLI 均为普通 Rust crate。
2. **界面使用 Flutter / Dart**：Material 3，桌面 Windows 优先。
3. **单进程优先**：Rust 以 `cdylib` 由 Flutter 通过 FFI 加载；不引入 sidecar、本地 HTTP 服务或 JSON 行协议。
4. **FFI 是唯一跨语言边界**：`fgm-ffi` 只做 C ABI、句柄、DTO 和错误映射，不承载业务规则。
5. **工具链追随最新版**：
   - Rust：最新 nightly，`rust/rust-toolchain.toml` 固定到具体日期
   - Flutter：master channel，`ui/.flutter-version` 固定到具体 commit/版本
   - `Cargo.lock` 与 `pubspec.lock` 提交，升级单独提交并跑完整门禁
6. **现有 C# 树冻结**：不再接受新功能或修复；只作为行为 oracle 和临时可运行版本，Rust/Flutter 达到对等后删除。
7. **重构分支**：`refactor/rust-flutter` 推进，`main` 在切换前保持现有可发布版本。

## 目录

```
rust/
  Cargo.toml
  rust-toolchain.toml
  crates/
    fgm-domain/
    fgm-core/
    fgm-infra/
    fgm-ffi/
    fgm-cli/
ui/
  pubspec.yaml
  .flutter-version
  lib/
    app/
    bridge/
    design/
    features/
  test/
tests/
docs/behavior/
docs/decisions/
build/
```

## 依赖与解耦规则

```
fgm-domain ← fgm-core ← fgm-infra
                  ↑          ↑
               fgm-ffi ─────┘
                  ↑
             Flutter (bridge)
```

- `fgm-domain` 零外部平台依赖；只放值对象、规则、`ErrorCode`、Pack schema。
- `fgm-core` 只依赖 domain；通过 port trait 访问文件系统、进程、NVAPI、网络。
- `fgm-infra` 实现 port；所有 `unsafe`、Win32、NVAPI、注册表、文件锁、网络在这里。
- `fgm-ffi` 只依赖 core + infra；导出稳定 ABI；无业务逻辑。
- Flutter 只依赖 `bridge`；feature 页面不直接碰平台 API，不持有业务真相。

## 轻量化规则

- 优先标准库；新增 crate/包必须有 ADR，并说明为什么现有依赖不能完成。
- 不引入大型异步运行时、状态管理框架、HTTP/DB 框架，除非 ADR 证明必要。
- 不复制状态：业务状态在 Rust；Flutter 只保留视图状态和缓存。
- 不为“以后可能用到”造抽象；抽象必须隔离真实变化。
- 生成代码、FFI 绑定、资产清单不计入文件行数限制。

## FFI 边界

- C ABI 稳定，句柄不透明；不把 Rust 引用、panic 或内部错误类型跨边界。
- 每个命令有明确输入/输出 DTO、`ErrorCode`、结构化上下文和 operation id。
- 长操作支持取消/进度；Flutter 不阻塞 UI 线程。
- panic 在边界捕获并转成错误码；禁止 unwind 穿过 FFI。
- FFI 版本号与 UI 兼容性检查在启动时执行。

## 迁移阶段

1. Phase 0：约束、ADR、Rust workspace、Flutter 壳、FFI 冒烟、固定 nightly/master、门禁脚本。
2. Phase 1：Domain/Core 行为规格与测试；Pack schema/校验器；CLI。
3. Phase 2：Infra 平台层：路径、哈希、文件锁、备份、PE、进程、NVAPI、ReShade。
4. Phase 3：统一部署引擎 + 游戏库 + 诊断；CLI 与旧版对照。
5. Phase 4：Flutter 逐页对等；布局保持现有信息结构。
6. Phase 5：便携版、安装包、更新源、卸载；真实游戏回归。
7. Phase 6：删除冻结的 C# 树；门禁切到 Rust + Flutter。

对等门槛：所有用户可见功能、部署/还原/清理语义、错误码、更新源、便携/安装行为可用；关键路径有 Rust 单测/集成测试、Flutter widget test 和至少一轮真实游戏回归。

## 风险与对策

| 风险 | 对策 |
|---|---|
| nightly / master 破坏性变更 | 固定日期/commit；升级单独提交；CI 固定工具链 |
| FFI 边界复杂、崩溃 | 边界薄、panic 捕获、错误码、跨语言冒烟测试 |
| Flutter 桌面打包/提权 | Phase 0 先验证 Windows 构建、UAC、安装器 |
| Rust 重写行为回归 | 旧版只读 oracle + `docs/behavior` + 夹具/真实游戏回归 |
| 依赖膨胀 | ADR、`cargo deny`/`cargo tree` 审查、禁止大型运行时 |
| 迁移期两套代码漂移 | C# 树冻结；只修 2.x 发布阻塞问题；新功能只进 Rust/Flutter |

## 后果

- 需要重写核心、UI、部署引擎和测试，周期以月计；不能一次性替换。
- 换来更小的运行时、更清晰的边界和 Flutter 更成熟的桌面 UI/动效能力。
- 在功能对等前，旧版仍是用户可用的发布版本。
