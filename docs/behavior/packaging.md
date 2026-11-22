# 便携版与安装包（Rust + Electron）

脚本：

- `build/publish-electron.ps1`：构建 napi 插件、Electron 应用、便携目录/zip 与安装包。
- `build/installer-electron.iss`：Inno Setup 安装脚本。
- `ui-electron/scripts/package.js`：Electron 打包裁剪与 rcedit 品牌信息。

## 便携版

- 输出：`artifacts/portable-electron/FrameGen-Manager-Next/` 和同名 zip。
- 内容：`FrameGen-Manager-Next.exe`、Electron 运行时（裁剪 locale）、`resources/app.asar`（`native/*.node` unpack）、`packs/`（清单 + `assets/packs/` 载荷）、`licenses/`。
- 数据目录：exe 同目录 `userdata/`；`packs/` 位于 exe 同目录。
- 重新打包时保留便携目录里的 `userdata/`（构建前临时移出、构建后放回），但 zip 不包含 `userdata`，避免测试库被分发。
- `packs/` 先复制仓库清单，再把 `assets/packs/` 的载荷叠加进去；载荷不进 git，但发布包必须包含，否则部署会报 `FileNotFound`。
- 打包时会删除除 `en-US.pak`、`zh-CN.pak` 外的 locale，移除 `resources/default_app.asar`。

## 安装包

- Inno Setup 6.7.4，`UseSetupLdr=x64`，避免旧 32 位 loader 在 Win10 22H2 上无响应；`Compression=lzma2/ultra64`。
- 每用户安装：`{localappdata}\Programs\{AppName}`。
- 输出：`artifacts/installer-electron/releases/FrameGenManagerNext-2.3.5-win-Setup.exe`；每次编译前清空 releases，只保留最新安装包。
- 安装器必须沿用 C# 2.x 的 AppId `{B7E7...}` 并按同一 `DefaultDirName` 升级；安装前清理旧 `FrameGenManager-Next.exe` 和 .NET payload，但不删除 `data`/`userdata`。安装版数据固定为 `%LOCALAPPDATA%\FrameGen-Manager-Next`；便携版用 `portable.flag` 选择 `<app>\userdata`，并迁移 C# 的 `<app>\data`。
- 安装包从 `ui-electron/dist/FrameGen-Manager-Next` 取文件，排除 `userdata`。
- 当前实测（含资产载荷）：解压约 1081MB、便携 zip 约 489MB、安装包约 376MB。不含载荷时约 312MB / 144MB / 101MB，但部署与 SM/DLSS5/ANR 会失败。

## 更新清单

- `build/publish-release-next.ps1`：调用 `publish-electron.ps1`，读取 `rust/Cargo.toml` 工作区版本，计算安装包 SHA-256/大小，输出 `artifacts/release-next/update.json`。
- `urls` 指向 `https://github.com/XiaoQAQ10086/FrameGen-Manager-Next/releases/download/<tag>/<安装包>`；客户端下载时自动展开 `ghfast.top` / `gh-proxy.com` / `ghproxy.net` 镜像，并逐个校验大小与 SHA-256。
- 发布步骤：创建 tag/Release 并上传安装包；把 `update.json` 提交到 `main`，客户端从 raw.githubusercontent.com 读取。

## 卸载

- 安装版卸载通过 Inno `[UninstallDelete]` 删除 `{app}\userdata` 与 `{app}\logs`。
- 便携版不写注册表；删除程序目录即完成卸载。

## 尚未完成

- 发布前资产注入/校验。
- 代码签名。
- 便携包使用 Deflate zip；如需更小可改用 7z/LZMA 或自解压包。

产物均可本地生成；GitHub Release 仍由人工确认后推送。
