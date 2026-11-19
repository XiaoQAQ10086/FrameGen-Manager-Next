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
- 输出：`artifacts/installer-next/releases/FrameGenManagerNext-2.3.4-win-Setup.exe`。
- 每次编译前清空 releases，只保留最新安装包。

## 更新清单

- `build/publish-release-next.ps1`：调用安装包构建，读取 `rust/Cargo.toml` 的工作区版本，计算安装包 SHA-256/大小，输出 `artifacts/release-next/update.json`（`version/notes/pageUrl/installer{name,size,sha256,urls}`）。
- `urls` 指向 `https://github.com/XiaoQAQ10086/FrameGen-Manager-Next/releases/download/<tag>/<安装包>`；客户端下载时会自动展开 `ghfast.top` / `gh-proxy.com` / `ghproxy.net` 镜像，并逐个校验大小与 SHA-256。
- 发布步骤：创建 tag/Release 并上传安装包；把 `update.json` 提交到 `main`，客户端从 raw.githubusercontent.com 读取。

## 卸载

- 安装版卸载项通过 Inno `[UninstallDelete]` 删除 `%LOCALAPPDATA%\FrameGen-Manager-Next`（库、备份、设置）与 `%TEMP%\FrameGen-Manager-Next`（下载缓存）。
- 便携版不写注册表；删除程序目录即完成卸载，`data/` 由用户自行决定是否保留。

## 尚未完成

- 发布前资产注入/校验。
- 代码签名。

便携版与安装包均可在本地生成；GitHub Release 仍由人工确认后推送。
