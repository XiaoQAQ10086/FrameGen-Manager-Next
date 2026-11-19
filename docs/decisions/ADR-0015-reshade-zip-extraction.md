# ADR-0015：ReShade 嵌入式 ZIP 解压依赖

- 状态：已接受
- 日期：2026-11-19
- 范围：Phase 4，DLSS5 / ReShade 离线安装

## 背景

ReShade 的 setup EXE 末尾附加一个 ZIP，里面是 `ReShade64.dll` / `ReShade32.dll`。2.x 默认离线提取该模块，避免运行 setup 时联网下载兼容数据。

标准库没有 DEFLATE 解压。手写 inflate 会引入长代码和边界风险；用外部 `Expand-Archive` / PowerShell 又违反单进程和轻量约束。

## 决策

- 新增 `flate2`（默认 `miniz_oxide` 后端）作为 `fgm-infra` 依赖，仅用于读取 ZIP 中的 DEFLATE 条目。
- ZIP 容器解析仍在项目内：EOCD、central directory、local header 都做边界检查；不支持 ZIP64（ReShade setup 远小于 4 GB）。
- 不引入完整 `zip` crate：只需要“从追加 ZIP 中按文件名提取一个条目”，完整 crate 的文件系统、写入、加密等功能用不到。
- 解压结果只在内存中存在，随后通过 `AtomicFile` 写入目标目录。
- 只接受 `ReShade64.dll` / `ReShade32.dll` 两个文件名；不执行 setup。

## 后果

- 构建增加 `flate2`、`miniz_oxide`、`crc32fast` 等小型纯 Rust 依赖。
- 不需要 TLS、网络或外部解压工具。
- 如果未来需要完整 ZIP 读写（例如 ANR 包），再评估 `zip` crate 或扩展当前解析器，不在此 ADR 预先抽象。
