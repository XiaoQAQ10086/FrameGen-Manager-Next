# ADR-0013：Rust Pack 解析与哈希依赖

- 状态：已接受
- 日期：2026-11-19
- 范围：Phase 1，`pack.json` 校验

## 背景

Phase 1 需要在 Rust 侧读取 `pack.json`、拒绝未知字段、校验 SHA-256。约束要求 Domain 不访问文件系统，并尽量少引入依赖。

手写 JSON 解析器会引入转义、嵌套、数字和错误定位等长期维护风险；用 Windows CNG 做 SHA-256 会把平台代码带进非 FFI/Infra 边界，也会增加 `unsafe`。

## 决策

新增三个纯 Rust 依赖：

| 依赖 | 版本 | 用途 |
|---|---|---|
| `serde` | 1 | `AssetPack` 等 schema 的派生反序列化 |
| `serde_json` | 1 | 严格 JSON 解析；未知字段通过 `deny_unknown_fields` 拒绝 |
| `sha2` | 0.11 | 流式 SHA-256，64 KiB 缓冲区，不整文件读入内存 |

- `serde` 只放在 `fgm-domain`，因为 pack schema 是领域模型。
- `serde_json` 与 `sha2` 只放在 `fgm-infra`；Domain 不读文件、不计算文件哈希。
- 不引入 `regex`：id / 版本 / SHA-256 用小型手写校验，规则固定且已有测试。
- 不引入 `clap`：Phase 1 只有 `pack validate`，参数解析保持在 `fgm-cli` 内。
- 不引入 `tempfile`：测试用系统临时目录 + 进程 id + 时间戳，Drop 时删除。

## 后果

- `cargo build` 会编译 `serde`、`serde_json`、`sha2` 及其小型依赖（`digest`、`hybrid-array` 等）。
- JSON 解析比 C# 的 `System.Text.Json` 更严格：
  - 不接受注释；
  - 不接受尾随逗号；
  - 属性名区分大小写。
  
  `pack.json` 由生成器产出，不使用这些扩展；如果未来需要兼容手写 pack，再单独加宽松读取器，不修改领域模型。
- SHA-256 实现是纯 Rust，可跨平台单测；后续部署引擎直接复用 `FileHasher`。
