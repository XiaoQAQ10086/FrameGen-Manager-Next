# 便携版与安装包（Rust + Flutter）

脚本：

- `build/publish-portable-next.ps1`
- `build/publish-installer-next.ps1`
- `build/installer-next.iss`

## 便携版

- 输出：`artifacts/portable-next/FrameGen-Manager-Next/` 和同名 zip。
- 内容：`fgm_ui.exe`、Flutter runtime、`fgm_ffi.dll`、`fgm-next.exe`、`packs/`、`licenses/`、`portable.flag`、空 `data/`。
- 复制的 `packs/` 只包含仓库中已存在的文件；发布前必须先把实际资产放进 `packs/`（资产本身不进 git）。
- 启动后 `AppPaths` 看到 `portable.flag`，使用 `<程序目录>\data` 作为数据目录。

## 安装包

- Inno Setup 6.7.4，`UseSetupLdr=x64`，避免旧 32 位 loader 在 Win10 22H2 上无响应。
- 每用户安装：`{localappdata}\Programs\FrameGen Manager-Next`。
- 不包含 `portable.flag`，因此数据目录使用 `%LOCALAPPDATA%\FrameGen-Manager-Next`。
- 输出：`artifacts/installer-next/releases/FrameGenManagerNext-0.1.0-win-Setup.exe`。
- 每次编译前清空 releases，只保留最新安装包。

## 尚未完成

- 发布前资产注入/校验。
- 卸载时是否清理用户数据、`--uninstall-cleanup` 对等。
- 自动更新源、下载、安装和回滚。
- 代码签名。

本轮只在本地生成便携版和安装包，未推送、未发布 GitHub Release。
