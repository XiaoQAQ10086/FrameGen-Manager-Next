# ADR-0011：Inno Setup 安装器与可选安装目录

- 状态：已接受
- 日期：2026-11-01

## 背景

Velopack 的 Windows Setup 是 one-click 安装器，没有原生的目录选择页；用户要求安装时可以选择安装目录。Velopack 的 `--instLocation` 只能由打包方固定路径，不能由用户在安装向导中选择。

## 决策

- 使用 Inno Setup 6 生成每用户安装包 `build/installer.iss`，保留安装向导的目录选择页。
- 安装/卸载界面跟随 Windows 显示语言：中文系统用简体中文，其余语言统一用英文；关闭语言选择弹窗。
- 安装目录写入 `install-manifest.txt`；升级时检测到旧 manifest 后先清空旧安装目录，再完整写入新版本，避免旧文件残留。
- 默认目录为 `%LOCALAPPDATA%\Programs\FrameGen Manager-Next`，用户可以在向导中修改。
- 安装器创建开始菜单/桌面快捷方式、标准卸载项，并保留应用图标。
- 更新流程：应用退出后，`updates.install` 以 `/SILENT /DIR="<当前安装目录>"` 启动新安装包，Inno 静默覆盖当前目录；安装后由 `[Run]` 重新启动程序。
- 卸载流程：Inno 删除安装目录、快捷方式和卸载注册项；卸载前调用 `FrameGenManager-Next.exe --uninstall-cleanup` 删除本地数据、更新临时目录、旧版 Next 数据目录、桌面/开始菜单快捷方式和开始菜单磁贴缓存。
- 2.0.1 使用 Velopack 安装；2.0.2 安装器会在安装前移除旧 Velopack 安装目录、快捷方式和卸载项，但保留 `%LOCALAPPDATA%\FrameGen-Manager-Next` 中的游戏库、配置和备份。
- 移除 Velopack 运行时依赖和 `vpk` 工具；应用更新继续使用现有 GitHub Release 检查和下载流程。
- 代码签名暂缓；安装包未签名时 SmartScreen 可能提示。

## 构建

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ./build/publish-installer.ps1
```

需要 Inno Setup 6（`ISCC.exe`，通常在 `Program Files (x86)\Inno Setup 6`）。输出为 `FrameGenManagerNext-<version>-win-Setup.exe`，不生成便携 zip。
