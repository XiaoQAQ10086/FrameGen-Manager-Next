# ADR-0006：便携版先于安装包

- 状态：已接受
- 日期：2026-10-29

## 背景

用户要求先拿到可运行的便携版做测试验收，验收通过后再生成安装包。安装包（Velopack）会引入安装目录、更新源、卸载和签名等额外变量，不适合作为第一阶段验收载体。

## 决策

- 新增 `build/publish-portable.ps1`：
  - `dotnet publish` 自包含 `win-x64`（无需预装 .NET）。
  - App 与 CLI 发布到同一目录。
  - 生成 `portable.flag`，`AppPaths.CreateDefault()` 检测到标记后把数据写到 exe 同目录的 `data\`。
  - 复制 `packs/` 清单，便于用 CLI 做 `--schema-only` 校验。
  - 输出文件夹 + zip。
- 安装包（Velopack）推迟到便携版验收通过后的 Phase 5。
- 便携版不写注册表；删除文件夹即视为卸载。

## 后果

- 测试人员拿到 zip 解压即可运行，不需要管理员权限或 .NET 运行时。
- 数据目录与安装版完全隔离，避免测试污染 `%LOCALAPPDATA%`。
- Velopack 的安装/更新/卸载仍需在 Phase 5 单独验证。
