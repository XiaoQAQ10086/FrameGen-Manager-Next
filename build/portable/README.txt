FrameGen Manager-Next（便携版 / Portable）
=========================================

启动：
  双击 “FrameGen Manager-Next.lnk”，或直接双击 FrameGenManager-Next.exe。
  fgm-next.exe 是命令行；fgm-next-elevated.exe 是提权助手，不要直接运行。

便携模式：
  本目录下的 portable.flag 会让程序把数据写到 .\data\
  （配置、备份、日志、缓存都在这里）。
  不写注册表；删除整个文件夹即可卸载。

界面：
  帧生成（WebView2 试验版） / Smooth Motion / DLSS5 / 日志与诊断 / 设置 / 关于
  “帧生成”页由本地 HTML/CSS 渲染（WebView2），左侧显示 GPU 状态、HAGS 和显卡名称伪装；
  右侧是游戏卡片列表。需要系统安装 Microsoft Edge WebView2 Runtime（Win11 通常自带）；
  WebView2 缓存写在系统 LocalAppData，不放在便携目录。
  游戏列表支持扫描 Steam、Epic、WeGame、散装目录，也可手动添加 EXE；
  Steam 游戏会显示本地或缓存封面。
  点某个游戏的“部署”后才会弹出代理、优化、帧数、Preset、引擎等参数。
  设置页可切换主题、启动自动扫描、封面开关，并打开数据目录/HAGS 设置。

命令行：
  fgm-next.exe --version
  fgm-next.exe --paths
  fgm-next.exe gpu
  fgm-next.exe gpu spoof inspect
  fgm-next.exe gpu spoof apply "NVIDIA GeForce RTX 5090"
  fgm-next.exe gpu spoof restore driver
  fgm-next.exe library list [--json]
  fgm-next.exe library scan [--json]
  fgm-next.exe logs tail [--lines N]
  fgm-next.exe pack validate <pack目录> [--no-hash] [--schema-only]

部署：
  “帧生成”页点“部署”后，在弹出的面板里调整代理槽/优化/帧数/Preset/引擎/SR，
  确认后写入 FrameGen 组件。
  “还原”会恢复部署前备份的原文件；用户改过的文件会保留并提示冲突。
  Smooth Motion 页面可逐游戏开启/关闭/还原。
  DLSS5 页面可逐游戏安装/还原/卸载；卸载会清理 ReShade、GITC 组件和旧版 RenoDX 残留。
  请先关闭游戏，再执行部署、还原或卸载。

资产包：
  packs\framegen.sm86\、packs\smooth-motion.rtx30\、packs\smooth-motion.rtx20\、
  packs\dlss5.reshade-gitc\ 内含本版部署所需的资产，可直接使用：
    fgm-next.exe pack validate packs\framegen.sm86

提权助手：
  fgm-next-elevated.exe 只用于显卡名称伪装；主程序会自动请求 UAC，
  请勿手动运行或替换该文件。

注意：
  - 这是测试用便携版，不是安装包。
  - 扫描只读，不会修改游戏目录；首次扫描可能需要几秒到几十秒。
  - 如果 Windows 提示缺少运行库，请确认使用的是自包含版本
    （本目录应包含 hostfxr.dll / hostpolicy.dll / coreclr.dll）。
  - 日志位于 .\data\logs\。
