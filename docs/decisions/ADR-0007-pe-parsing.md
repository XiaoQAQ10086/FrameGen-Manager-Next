# ADR-0007：流式解析 PE 导入表

- 状态：已接受（取代 PeNet 方案）
- 日期：2026-10-29

## 背景

游戏库扫描需要从 EXE 的导入表判断图形 API（D3D9/10/11/12、Vulkan、OpenGL）。部分游戏 EXE 达到 200–450 MB；PeNet 的 `PeFile(string)` 会把整个文件读入内存，切换到 DLSS5 页面时会触发数百 MB 的瞬时分配。

## 决策

- 使用 BCL `System.Reflection.PortableExecutable.PEReader` 读取 PE 头和节表，按 RVA → 文件偏移只读取导入描述符和模块名。
- 同时读取普通导入表与延迟导入表；解析失败视为 `Unknown`，不阻断扫描。
- 不再依赖 PeNet；`GraphicsApiClassifier` 与 `GraphicsApiFacts` 保持纯函数。
- DLSS5 检查优先使用游戏库已存储的图形 API，只有 `Unknown` 时才重新探测。

## 后果

- 读取 400 MB EXE 的导入表时不再分配整文件大小的缓冲区。
- 导入表格式的边界处理由 `PeImportReaderTests` 覆盖；缺失/损坏文件返回空列表。
- 不再新增第三方 PE 解析依赖，发布目录更小。
