# ADR-0014：FFI 复杂 DTO 使用一次性 JSON 信封

- 状态：已接受
- 日期：2026-11-19
- 范围：Phase 4，Flutter ↔ Rust 桥接

## 背景

`fgm-ffi` 已经导出稳定标量函数（版本、阶段键）。游戏库返回 `Vec<GameEntry>`，部署返回报告和错误上下文；如果全部映射成 `#[repr(C)]` 数组，需要额外的长度、字符串生命周期、释放函数和嵌套结构，边界会迅速变厚。

约束禁止 sidecar、本地 HTTP 和 JSON 行协议；没有禁止在单次 FFI 调用中传递一个自包含 JSON 文档。

## 决策

- 复杂结果使用**每次调用一个 JSON 文档**：
  - 成功：`{"ok":true,"games":[...]}`
  - 失败：`{"ok":false,"code":"AccessDenied","context":[["path","..."]]}`
- Rust 分配并以 NUL 结尾返回；Dart 读取后立即调用 `fgm_string_free`。
- 不保留连接、不按行分隔、不做请求/响应多路复用；每次调用完全独立。
- `fgm-ffi` 只负责调用 infra、捕获 panic、把结构化错误转成信封；业务规则仍在 core/infra。
- 耗时调用（游戏库扫描）由 Flutter 放到 isolate 中执行，避免阻塞 UI 线程。
- 后续若某个 DTO 成为高频路径，再为它单独增加 typed C struct；不预先抽象。

## 后果

- 游戏库和部署结果可以快速接入 Flutter，不需要为每个 DTO 写 C 结构。
- JSON 序列化/解析成本可接受：游戏库一次扫描几十到几百条，调用频率低。
- 字符串所有权明确：Rust 分配、Dart 释放一次；禁止把返回指针保存超过一次调用。
- 错误信封中的 `context` 是结构化键值，不包含用户可见文案；Flutter 仍按 `code` 本地化。
