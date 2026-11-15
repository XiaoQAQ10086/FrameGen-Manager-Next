# ReShade effects（reshade-shaders 分发包）

本目录说明 `dlss5.reshade-gitc` pack 里随附的 shader / texture 文件的版权归属。

来源：ReShade 官方的 effect 仓库（`crosire/reshade-shaders`）发布的完整 shader 包，原样打包，未做任何修改。
该仓库没有统一的 LICENSE 文件，**每份文件带自己的版权头**；程序在部署时把文件原样写进游戏的
`reshade-shaders\Shaders\` 与 `reshade-shaders\Textures\`，文件头随文件一起保留，用户可在游戏目录里直接查看。

| 目录 | 内容 | 版权与许可 |
| --- | --- | --- |
| `Shaders\`（根） | Blending.fxh、Daltonize.fx、Deband.fx、DisplayDepth.fx、DrawText.fxh、UIMask.fx、ReShade.fxh、ReShadeUI.fxh 等基础效果 | 各文件头部保留作者与许可；例如 Deband.fx 为 Copyright (c) 2015 Niklas Haas（MIT），其余多为 MIT 或同等的宽松许可 |
| `Shaders\GITC-Uplift\` | Uplift.fx、UpliftMask.fx | GITC 2.0.0，Copyright (c) 2026 Hussain Abdullah（MIT），见 `..\gitc_uplift\LICENSE.txt` |
| `Shaders\iMMERSE\` | MartysMods_MXAO / _SMAA / _SHARPEN / _SOLARIS / _FILMGRAIN / _LAUNCHPAD 及 `MartysMods\*.fxh` | Copyright (c) Pascal Gilcher. All rights reserved. — 文件头写明 "Unauthorized copying of this file, via any medium is strictly prohibited / Proprietary and confidential"。这些文件来自作者本人在上述官方仓库的公开发布，本程序未修改、原样分发；如需彻底规避该条款，应在打包时剔除 `effects\Shaders\iMMERSE\` 与 `effects\Textures\iMMERSE\` |
| `Shaders\SweetFX\` | SweetFX 系列（Levels、LumaSharpen、Vibrance、Curves、Tonemap、CRT 等） | Copyright (c) CeeJay.dk（MIT）；其中 SMAA 由 Jimenez 等人提供（宽松许可），FXAA 为 NVIDIA FXAA 3.11 by Timothy Lottes（版权声明保留在 `FXAA.fxh` 头部） |
| `Shaders\ReShade_HDR_shaders\` | lilium 的 HDR 相关效果与 `lilium__include\` 头文件 | 版权与许可声明在各文件内 |
| `Textures\` | FontAtlas.png、lut.png、SweetFX 的 AreaTex/Layer/SearchTex、iMMERSE 噪点图、lilium 噪声图与字体图集 | 随对应 shader 的许可：crosire / CeeJay.dk / Pascal Gilcher / lilium |
