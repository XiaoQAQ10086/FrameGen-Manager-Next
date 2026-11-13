namespace FrameGenManagerNext.App.Localization;

internal static partial class StringCatalog
{
    static StringCatalog()
    {
        Add("Nav.Anr", "ANR 帧生成", "ANR");
        Add("Anr.Title", "ANR 帧生成", "ANR frame generation");
        Add("Anr.Subtitle", "Apex Neural Rendering · Beta · 通过 OptiScaler 接管游戏的 DLSS Frame Generation", "Apex Neural Rendering · Beta · takes over the game's DLSS Frame Generation through OptiScaler");
        Add("Anr.Experimental", "实验性", "Experimental");
        Add("Anr.Description", "ANR 不是独立启动器：它替换游戏现有的 DLSS Frame Generation 实现。游戏没有该选项时，安装后也不会生效。", "ANR is not a separate launcher: it replaces the game's existing DLSS Frame Generation implementation. If the game has no such option, installing it has no effect.");
        Add("Anr.GpuTitle", "支持部署的显卡及资产文件来源", "Deployable GPUs & asset sources");
        Add("Anr.GpuNvidia", "NVIDIA RTX 20 / 30 / 40 / 50 系列，GTX 16 系列", "NVIDIA RTX 20 / 30 / 40 / 50 series, GTX 16 series");
        Add("Anr.GpuAmd", "AMD Radeon RX 5000 / 6000 / 7000 / 9000 系列，Steam Deck", "AMD Radeon RX 5000 / 6000 / 7000 / 9000 series, Steam Deck");
        Add("Anr.GpuIntel", "Intel Arc A / B 系列", "Intel Arc A / B series");
        Add("Anr.RequirementTitle", "使用前提", "Requirements");
        Add("Anr.RequirementGame", "游戏必须已有 DLSS Frame Generation 选项；没有该选项的游戏不会生效。", "The game must already offer DLSS Frame Generation; games without it are not supported.");
        Add("Anr.RequirementRuntime", "需要 Microsoft Visual C++ 2015-2022 x64 运行库，否则 OptiScaler 不会加载。", "Microsoft Visual C++ 2015-2022 x64 runtime is required, otherwise OptiScaler will not load.");
        Add("Anr.RequirementProxy", "会占用 dxgi.dll、winmm.dll 或 version.dll 之一，可能与 DLSS5/ReShade 或现有帧生成冲突。", "Uses one of dxgi.dll, winmm.dll or version.dll and may conflict with DLSS5/ReShade or the existing frame generation.");
        Add("Anr.DownloadTitle", "下载 ANR FG V.B15", "Download ANR FG V.B15");
        Add("Anr.DownloadGithub", "GitHub 官方下载", "Download from GitHub");
        Add("Anr.DownloadBaidu", "百度网盘下载", "Baidu Netdisk");
        Add("Anr.DownloadQuark", "夸克网盘下载", "Quark Drive");
        Add("Library.BundledAssets", "内置资产", "Bundled assets");
        Add("Library.DownloadedAssets", "下载/导入资产", "Downloaded assets");
        Add("Mode.SupportedGpus", "支持部署的显卡及资产文件来源", "Deployable GPUs & asset sources");
        Add("Mode.Badge.FrameGen", "帧生成", "FrameGen");
        Add("Mode.Badge.SmoothMotion", "SM", "SM");
        Add("Mode.Badge.Anr", "ANR", "ANR");
        Add("Mode.GithubSource", "GitHub 资产来源", "GitHub asset source");
        Add("Mode.FrameGen.Gpus", "NVIDIA RTX 20 / 30 系", "NVIDIA RTX 20 / 30 series");
        Add("Mode.SmoothMotion.Gpus", "NVIDIA RTX 20 / 30 系", "NVIDIA RTX 20 / 30 series");
        Add("Mode.Dlss5.Gpus", "NVIDIA RTX 20 / 30 系", "NVIDIA RTX 20 / 30 series");
        Add("Anr.StepTitle", "安装步骤", "Installation");
        Add("Anr.Step1", "关闭游戏。", "Close the game.");
        Add("Anr.Step2", "下载并解压 anr-fg-full。", "Download and extract anr-fg-full.");
        Add("Anr.Step3", "运行 install.cmd，把游戏 EXE 所在目录作为参数传入。", "Run install.cmd with the folder that contains the game EXE.");
        Add("Anr.Step4", "启动游戏，在图形设置中打开 DLSS Frame Generation。", "Start the game and enable DLSS Frame Generation in the graphics settings.");
        Add("Anr.Step5", "卸载时关闭游戏，运行 uninstall.cmd 并传入同一目录。", "To uninstall, close the game and run uninstall.cmd with the same folder.");
        Add("Anr.IngameTitle", "开启 ANR 后还要进游戏做什么", "What to do in-game after installing ANR");
        Add("Anr.IngameBody", "必须进游戏打开 DLSS Frame Generation；有多项帧生成时选 NVIDIA DLSS。ANR 会顶替游戏原来的 DLSS-G，不需要额外的 ANR 开关。", "You must enable DLSS Frame Generation in-game; if several frame-generation options exist, choose NVIDIA DLSS. ANR takes the place of the game's DLSS-G; there is no separate ANR toggle.");
        Add("Anr.Disclaimer", "ANR 由 Apex Compute 提供，目前为 Beta。本软件负责下载/导入、代理选择、备份与还原；ANR 与 OptiScaler 的版权归其作者。请自行确认许可证和反作弊风险。", "ANR is provided by Apex Compute and is currently in beta. This app handles download/import, proxy selection, backup and restore; ANR and OptiScaler remain the property of their authors. Check the license and anti-cheat risk yourself.");
        Add("Anr.PackageTitle", "ANR 组件", "ANR package");
        Add("Anr.CurrentGpu", "当前显卡：{0}", "Current GPU: {0}");
        Add("Anr.PackageMissing", "未检测到 ANR 组件，请先在线下载或导入 ANR 压缩包。", "ANR package not detected. Download it online or import an ANR zip first.");
        Add("Anr.PackageVersion", "版本：{0}", "Version: {0}");
        Add("Anr.PackageReadyBadge", "已就绪", "Ready");
        Add("Anr.PackageMissingBadge", "未下载", "Not downloaded");
        Add("Anr.HasUpdate", "有更新", "Update available");
        Add("Anr.OnlineDownload", "在线下载", "Download online");
        Add("Anr.ImportZip", "导入 ANR 压缩包", "Import ANR zip");
        Add("Anr.GameTitle", "游戏库", "Game library");
        Add("Anr.GamesLoaded", "已检查 {0} 个游戏", "Checked {0} games");
        Add("Anr.NotDeployed", "未部署", "Not deployed");
        Add("Anr.Installed", "已部署", "Deployed");
        Add("Anr.Partial", "需修复", "Needs repair");
        Add("Anr.ProxyLabel", "代理：{0}", "Proxy: {0}");
        Add("Anr.Deploy", "部署", "Deploy");
        Add("Anr.Restore", "还原", "Restore");
        Add("Anr.NeedsPackage", "请先下载或导入 ANR", "Download or import ANR first");
        Add("Anr.NeedsOptiScaler", "addon 包需要游戏已安装 OptiScaler；没有的话请导入 full 包", "The addon package needs OptiScaler in the game; import the full package otherwise");
        Add("Anr.KindAddon", "Addon（需 OptiScaler）", "Addon (requires OptiScaler)");
        Add("Anr.NeedsProxy", "dxgi / winmm / version 均被占用", "dxgi / winmm / version are all in use");
        Add("Anr.NeedsExe", "未找到渲染 EXE", "Render EXE not found");
        Add("Anr.OptiScalerConflict", "已有 OptiScaler，请使用 addon 包", "An existing OptiScaler was found; use the addon package");
        Add("Anr.Downloading", "正在下载 ANR…", "Downloading ANR…");
        Add("Anr.Importing", "正在导入 ANR…", "Importing ANR…");
        Add("Anr.DeployedToast", "ANR 已部署", "ANR deployed");
        Add("Anr.RestoredToast", "ANR 已还原", "ANR restored");
        Add("Anr.UpdateAvailable", "发现 ANR 新版本 {0}（当前 {1}）。可直接更新已部署游戏，原始文件备份会保留。", "ANR {0} is available (current {1}). Deployed games can be updated in place; original backups are kept.");
        Add("Anr.UpdateNow", "更新", "Update");
        Add("Anr.UpdateLater", "稍后", "Later");
        Add("Anr.Updating", "正在更新 ANR…", "Updating ANR…");
        Add("Anr.Updated", "ANR 已更新", "ANR updated");
        Add("Anr.UpdatedGames", "已更新 {0} 个游戏", "Updated {0} game(s)");
        Add("Anr.UpdateFailedGames", "有 {0} 个游戏未能更新", "{0} game(s) could not be updated");
        Add("Anr.Empty", "没有可用的游戏。", "No games available.");

    }

    private static void Add(string key, string chinese, string english)
    {
        Entries[key] = (chinese, english);
    }
}
