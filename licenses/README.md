# 第三方许可与声明（Third-party licenses and notices）

本目录随安装包与便携版一起发布到程序的 `licenses\` 子目录，列出本程序分发的每一个第三方组件的来源、
版权归属与许可全文。界面上可在「关于」页点「打开许可目录」直接打开本目录。

本程序自身以 MIT 许可发布，全文见 `FrameGenManagerNext-LICENSE.txt`。

## 组件一览

| 组件 | 版本 | 随哪个资产包分发 | 许可 | 全文 |
| --- | --- | --- | --- | --- |
| FrameGen Manager-Next | 见「关于」页 | 程序本体 | MIT，Copyright (c) 2026 XiaoQAQ10086 | `FrameGenManagerNext-LICENSE.txt` |
| dlssg for sm86（sdli1995） | 0.4.2 | `framegen.sm86`（`version.dll`、`dlssg_sm86.ini`、`dlssg_sm86\`；`alternatives\` 只随 pack 分发，按上游说明不部署到游戏目录） | MIT，Copyright (c) 2026 sdli1995；另含 Intel XeSS SDK、NVIDIA DLSS 等第三方条款 | `dlssg_for_sm86\LICENSE.md`、`dlssg_for_sm86\THIRD_PARTY_NOTICES.txt` |
| Smooth Motion 运行时（RTX 20 / RTX 30） | 2.8.5 / 1.0.0 | `smooth-motion.rtx20`、`smooth-motion.rtx30` | MIT，Copyright (c) 2026 Pipo（打包仓库）；DLL 本身的来源与版本差异见 `smooth_motion\PROVENANCE.txt` | `smooth_motion\LICENSE.txt`、`smooth_motion\PROVENANCE.txt` |
| ReShade | 6.8.0 | `dlss5.reshade-gitc`（官方安装器 `ReShade_Setup_6.8.0_Addon.exe`，校验哈希记录在 pack 里） | BSD 3-Clause，Copyright 2014 Patrick Mours | `reshade\LICENSE.md` |
| GITC-Uplift | 2.0.0 | `dlss5.reshade-gitc`（`gitc-uplift.addon64/32`、`gitc-uplift-helper64.exe`、`nvngx_dlssnr.dll`、`Uplift.fx`） | MIT，Copyright (c) 2026 Hussain Abdullah；其 notices 另含 ReShade add-on 头文件（BSD-3-Clause）、ImGui、minhook | `gitc_uplift\LICENSE.txt`、`gitc_uplift\THIRD_PARTY_NOTICES.md` |
| ReShade effects（reshade-shaders 分发包） | — | `dlss5.reshade-gitc`（写进游戏的 `reshade-shaders\Shaders\`、`Textures\`） | 逐文件版权头：多数 MIT；iMMERSE 为 Pascal Gilcher「All rights reserved」；FXAA 为 NVIDIA FXAA 3.11 | `reshade_shaders\README.md` |
| REFramework | 1.0.0 | `reframework`（`dinput8.dll`） | MIT，Copyright (c) 2019 praydog | `reframework\LICENSE.txt` |
| ANR 帧生成（基于 OptiScaler） | 运行时获取 | **不随包分发**：安装时由程序按上游清单下载，或用户自行准备 | 见上游项目 | — |

## 分发方式

- 资产包只携带运行所需的文件；上表列出的许可与声明文本作为 `licenses\` 目录与程序一起发布，
  不塞进每个游戏目录，避免污染游戏安装。
- 每个资产包的完整文件清单与哈希在 `packs\<packId>\pack.json`；校验命令：
  `fgm-next pack validate packs\<packId>`。
- 上游资产发布新版本导致文件变化时，`licenses\` 里的对应文本要一起更新。

## 已知待确认项

- **iMMERSE**（Pascal Gilcher）：文件头写明未经许可不得以任何介质复制，但这些文件来自作者本人在
  ReShade 官方 shader 仓库的公开发布，本程序未修改、原样分发。若要求彻底规避该条款，需要从
  `dlss5.reshade-gitc` 的打包内容中剔除 `iMMERSE` 目录（相关效果将不可用）。详见
  `reshade_shaders\README.md`。
- **Smooth Motion 的 DLL 来源**：打包仓库声明其中的 DLL 并非其本人所作，仅做打包；上游参考实现列在
  `smooth_motion\PROVENANCE.txt` 里，其中 RTX 20 的 `version.dll` 为 Ultimate-ASI-Loader。若后续
  需要更严格的合规，应与对应上游项目分别确认分发条款。
