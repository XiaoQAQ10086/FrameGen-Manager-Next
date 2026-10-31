# ADR-0010：Velopack 安装包与彻底卸载

- 状态：已接受
- 日期：2026-10-31

## 背景

便携版验收通过后进入 Phase 5，需要每用户安装包、开始菜单/桌面快捷方式、标准卸载入口，并保证卸载后不留下配置、缓存、备份和 WebView2 数据。

## 决策

- 使用 Velopack 1.2.161 打包，`packId = FrameGenManagerNext`，版本从 `Directory.Build.props` 读取。
- 新增本地工具清单 `.config/dotnet-tools.json`，固定 `vpk` 1.2.161；这是构建工具，不是应用运行时依赖。
- 新增 `build/publish-installer.ps1`：
  - 自包含 `win-x64` 发布 App、CLI、提权助手和资产包到暂存目录；
  - 不包含 `portable.flag` 和 `data\`，安装版数据写入 `%LOCALAPPDATA%\FrameGen-Manager-Next`；
  - 生成每用户安装包（`--instLocation PerUser`），默认安装到 `%LOCALAPPDATA%\FrameGenManagerNext`；
  - 输出 `Setup.exe`、完整包、便携 zip、`RELEASES` 和 `releases.win.json`。
- `Program.Main` 在 Velopack 启动钩子中注册 `OnBeforeUninstallFastCallback`，调用 `UninstallCleanup.Run()` 删除：
  - `%LOCALAPPDATA%\FrameGen-Manager-Next`（配置、备份、封面、日志、状态和 WebView2 缓存）；
  - `%LOCALAPPDATA%\FrameGenNext`（早期 Next 开发构建遗留的数据目录）；
  - `%TEMP%\FrameGen-Manager-Next`（更新下载临时目录）；
  - `HKCU\Software\Microsoft\Windows\CurrentVersion\Start\TileProperties\W~velopack.FrameGenManagerNext`（开始菜单磁贴缓存）。
- Velopack 自身负责删除安装目录、桌面/开始菜单快捷方式和 `HKCU` 卸载注册项。
- 代码签名暂缓；首次运行 Setup.exe 可能触发 SmartScreen 提示，发布前提供 SHA-256。

## 验证（2026-10-31）

1. 运行 `FrameGenManagerNext-win-Setup.exe`，安装到 `%LOCALAPPDATA%\FrameGenManagerNext` 并自动启动；数据目录和两部分快捷键正常生成。
2. 注册表出现 `FrameGen Manager-Next` 2.0.0 卸载项，命令为 `Update.exe --uninstall`。
3. 运行 `Update.exe --uninstall --silent`，退出码 0。
4. 复核安装目录、`%LOCALAPPDATA%\FrameGen-Manager-Next`、`%LOCALAPPDATA%\FrameGenNext`、`%TEMP%\FrameGen-Manager-Next`、桌面/开始菜单快捷方式、开始菜单磁贴缓存和卸载注册项均已删除。
5. 旧版 `FrameGen Manager 1.7.0` 的快捷方式和注册项保持不变。
