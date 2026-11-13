# ANR 帧生成（实验性）

来源：[apex-compute/ANR](https://github.com/apex-compute/ANR)。ANR FG 目前是 Beta，主程序源码未公开；本项目只负责下载/导入、代理选择、备份和还原，不修改 ANR 自身文件。

## 支持范围

- NVIDIA RTX 20/30/40/50、GTX 16；AMD RX 5000/6000/7000/9000、Steam Deck；Intel Arc A/B。
- Windows 10/11。游戏必须已有 DLSS Frame Generation 选项；没有该选项的游戏安装后不会生效。
- 需要 Microsoft Visual C++ 2015-2022 x64 运行库。
- ANR full 使用 OptiScaler，会占用 `dxgi.dll`、`winmm.dll` 或 `version.dll` 之一；与 DLSS5/ReShade 或现有帧生成可能冲突。

## 包来源与校验

- 在线下载：读取 GitHub `releases/latest`，只选择 `anr-fg-*-full.zip`（排除 addon、optiscaler-source 和 kit），使用 GitHub API 的 `digest` 校验 SHA-256；下载源优先大陆镜像，失败回退官方地址。
- API 额度不足时不阻塞下载：回退到 `https://github.com/apex-compute/ANR/releases/latest` 的重定向获取 tag，再从同一 release 的 `SHA256SUMS.txt` 读取 full 包哈希；仍走镜像优先。可选的 `GITHUB_TOKEN` / `GH_TOKEN` 会作为 Bearer token 发送给 GitHub API，提升额度。
- 导入：同时接受官方 full 和 addon 压缩包。full 以 `game/dxgi.dll` + `game/OptiScaler.ini` 识别；addon 以 `Apex Neural Rendering FG/dlssg/nvngx_dlssg.dll` 识别；版本优先读 `nr_fg_package.json`。两种布局都不匹配时返回 `PackInvalid`。
- 缓存：`packs/anr-fg/<version>/payload/`，`current.json` 记录当前版本。不上传或内置 ANR 二进制。
- 缓存位置：安装版在 `%LOCALAPPDATA%\FrameGen-Manager-Next\packs\anr-fg\`；便携版在 `<程序目录>\data\packs\anr-fg\`。仓库的 `assets/` 不包含 ANR。

## 自动部署

- 新增独立 family `anr`，与 `framegen`、`dlss5` 分开；不会复用 DLSS5 的 manifest。
- 目标目录为游戏渲染 EXE 所在目录；部署前检查游戏进程。
- 代理选择顺序：`dxgi.dll` → `winmm.dll` → `version.dll`，跳过已被帧生成/DLSS5/REFramework/其他 Mod 占用的入口；三个都被占用时禁用部署。
- 复制包内 `game/` 全部文件；`game/dxgi.dll` 写成选中的代理名，其余保持 `OptiScaler/...`、`OptiScaler.ini` 布局。
- 已存在非本程序的 `OptiScaler` 目录时拒绝 full 部署，提示改用 addon 包。
- addon 部署：要求游戏目录已有 `OptiScaler.ini`（通常位于渲染 EXE 目录，`OptiDllPath=auto` 时优先 `<游戏>/OptiScaler`）；把包内 `Apex Neural Rendering FG/` 复制到 `<OptiScaler>/custom/Apex Neural Rendering FG/`，并在 `[FrameGen]` 写入 `FGInput=nvngxfg`、`FGNvngxReplacement=Apex Neural Rendering FG`，保留原文件的换行和 BOM。已存在同名 custom 目录且不是本程序部署时拒绝。
- 所有文件通过统一部署引擎备份、写入、校验；还原时恢复代理、`OptiScaler.ini` 和 custom 目录的原有文件，删除本次 ANR 文件。
- OptiScaler/ANR 运行时会在自有目录写入 `nr_fg-*.log`、`anr-vkloader.dll` 等文件。还原时这些清单外文件只允许在 full 的 `OptiScaler/` 或 addon 的 `custom/Apex Neural Rendering FG/` 内删除，然后重新执行一次还原，使 manifest 进入 `Restored`；用户目录中的其他遗留文件仍按冲突/遗留规则保留。
- 版本升级不需要先卸载：打开页面会检查上游最新版本；确认后先下载并校验新包，再对已部署游戏执行原地部署（统一引擎复用原始备份、替换旧文件、清理旧版独有文件）。下载失败不会影响现有部署；单个游戏更新失败会保留原部署并单独报告。
- 不执行官方 `install.cmd`/`install.ps1`，避免绕过 manifest 和备份。

## 界面

- 顶部 `ANR 帧生成` 页面左侧显示包状态、当前显卡、支持显卡和使用前提；右侧为游戏库。
- 【在线下载】在应用内下载并实时显示进度条、百分比和下载速度（字节/秒，界面换算为 KB/s 或 MB/s）；【导入 ANR 压缩包】选择本地 zip；【百度网盘下载】【夸克网盘下载】打开对应网盘链接（链接由桥接层提供，不在 Core 出现）。
- 未检测到组件时，包状态用红色文字提示缺少 ANR 并引导下载/导入；组件卡片标题旁用徽章显示【未下载】【已就绪】【有更新】。
- 游戏卡片用醒目徽章显示部署状态：已部署、未部署、有更新（已部署版本低于当前组件版本）、需修复；已部署时同时显示 ANR 模式徽章、包版本和代理入口。
- 游戏卡片提供【启动游戏】按钮，复用游戏库的启动方式；无法确定启动方式时按钮禁用。
- 跨模式徽章：任意一个游戏部署过帧生成、Smooth Motion 或 ANR，三个模式页面对应的游戏卡片都会显示对应模式徽章，便于发现同一游戏的并存部署。
- 游戏库与其他页面共用同一份数据：显示封面，支持`扫描游戏`、`添加游戏`和`已移除的游戏`，增删会在页面间同步。ANR 的部署/还原完成后会广播库变更，其他已挂载页面重新拉取状态；页面重新显示时也会刷新，避免徽章停留在旧状态。
- 包未下载/未导入时不显示部署按钮，卡片提示先获取 ANR；只有实际部署后才显示代理入口徽章。
- 打开页面时静默检查上游最新版本；有新版本时显示顶部更新条，可直接更新已部署游戏，也可稍后处理。游戏卡片不再显示“进游戏后打开 DLSS Frame Generation”的常驻提示，原因（缺 EXE、缺组件、代理冲突等）仍会在需要时显示。
- 当前包为 addon 时，包状态标注 `Addon（需 OptiScaler）`；游戏没有 `OptiScaler.ini` 时部署按钮禁用并提示先装 OptiScaler 或改用 full 包。
- 部署/还原按钮走统一进度和错误提示。
- 进游戏后仍需手动打开 DLSS Frame Generation；ANR 没有独立开关。
- `资产文件夹`（帧生成页）同时列出内置资产（程序目录 `packs/`）和下载/导入资产（数据目录 `packs/`）；下载/导入的 ANR 会以 `anr-fg/` 出现在后者。

## 测试

- `Infrastructure.Tests/AnrDeploymentServiceTests.cs`：full 代理映射、备份、还原（含运行日志清理）；addon 自定义 provider、`OptiScaler.ini` 改写与还原；新版本原地覆盖并保留原始备份。
- `Infrastructure.Tests/AnrPackageServiceTests.cs`：full/addon 导入识别、无效 zip 拒绝。
- 在线下载依赖网络，不进入单元测试；手工验收时检查包版本和 SHA-256。
