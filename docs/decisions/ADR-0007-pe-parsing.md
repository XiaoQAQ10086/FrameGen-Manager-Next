# ADR-0007：使用 PeNet 解析 PE 导入表

- 状态：已接受
- 日期：2026-10-29

## 背景

游戏库扫描需要从 EXE 的导入表判断图形 API（D3D9/10/11/12、Vulkan、OpenGL）。旧项目手写了 PE 导入表解析；新项目不应重复实现容易出错的二进制解析。

## 决策

- 使用 NuGet 包 **PeNet 6.1.2**，只在 `Infrastructure/Library` 中使用。
- Domain 只接收提取后的模块名列表（`GraphicsApiFacts`），不依赖 PeNet。
- 解析失败视为 `Unknown`，不阻断扫描。
- PeNet 版本锁定在 `Directory.Packages.props`，升级走 Dependabot + 完整 CI。

## 备选方案

- `System.Reflection.PortableExecutable`：能读 PE 头和节表，但导入表仍需手写解析。
- 手写解析器：旧项目已经证明容易遗漏边界情况，且测试困难。

## 后果

- 新增一个纯托管依赖；不引入原生库。
- `GraphicsApiClassifier` 保持纯函数，PE 读取可以单独替换。
