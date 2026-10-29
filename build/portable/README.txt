FrameGen Manager-Next（便携版 / Portable）
=========================================

启动：
  双击 FrameGenManager-Next.exe

便携模式：
  本目录下的 portable.flag 会让程序把数据写到 .\data\
  （配置、备份、日志、缓存都在这里）。
  不写注册表；删除整个文件夹即可卸载。

界面：
  游戏库 / GPU 状态 / 日志与诊断 / 关于
  游戏库支持扫描 Steam、Epic、WeGame、散装目录，也可手动添加 EXE。

命令行：
  fgm-next.exe --version
  fgm-next.exe --paths
  fgm-next.exe gpu
  fgm-next.exe library list [--json]
  fgm-next.exe library scan [--json]
  fgm-next.exe logs tail [--lines N]
  fgm-next.exe pack validate <pack目录> [--no-hash] [--schema-only]

资产包：
  packs\ 下是本版附带的只读清单，可做 schema 校验：
    fgm-next.exe pack validate packs\framegen.sm86 --schema-only
  完整 payload 校验需要 assets\packs 下的资产（开发目录中生成）。

注意：
  - 这是测试用便携版，不是安装包。
  - 扫描只读，不会修改游戏目录；首次扫描可能需要几秒到几十秒。
  - 如果 Windows 提示缺少运行库，请确认使用的是自包含版本
    （本目录应包含 hostfxr.dll / hostpolicy.dll / coreclr.dll）。
  - 日志位于 .\data\logs\。
