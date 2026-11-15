# packs

本目录存放**资产包清单**（`pack.json`），由 `build/generate-packs.ps1` 生成。payload 二进制不进入 git，生成在 `assets/packs/<pack-id>/`。

第三方许可文本不进 pack：统一放在仓库根目录的 `licenses/`，发布时随安装包与便携版落到程序的 `licenses\`（见 `licenses/README.md`）。

当前资产包：

| Pack ID | 版本 | 内容 | GPU 路由 |
|---|---|---|---|
| `framegen.sm86` | 0.4.2 | 六个代理 DLL（只把选中的那个部署到游戏根目录，其余五个留在 `alternatives/`）、`dlssg_sm86` 运行时、stock INI | Sm86 / Sm75 |
| `smooth-motion.rtx30` | 1.0.0 | RTX 30 SmoothMotion-next 组件 | Sm86 |
| `smooth-motion.rtx20` | 2.8.5 | RTX 20 ASI loader + normal.asi + runtime-manifest | Sm75 |
| `dlss5.reshade-gitc` | 6.8.2 | ReShade 安装器、GITC Uplift addon/helper/model、shaders、effects | Sm86 / Sm75 |
| `reframework` | 1.0.0 | REFramework 的 `dinput8.dll` 与 revision 记录 | Sm86 / Sm75 |

## 生成

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File build\generate-packs.ps1
```

默认从旧项目只读读取：

- `D:\AI Work\FrameGen Manager\dlssg-manager\apps\electron\bundled-assets`
- `D:\AI Work\FrameGen Manager\dlssg-manager\bundled-sm`
- `D:\AI Work\FrameGen Manager\dlssg-manager\apps\electron\bundled-dlss5`

生成后：

- 清单：`packs/<pack-id>/pack.json`（提交到 git）
- payload：`assets/packs/<pack-id>/`（git-ignored）
- 每个 pack 目录里都有一份 `pack.json` 副本，供 CLI 校验

## 校验

```powershell
fgm-next pack validate assets\packs\framegen.sm86
fgm-next pack validate packs\framegen.sm86 --schema-only
```

## 规则

- 不要手工改 `pack.json`；改生成脚本里的映射，然后重新生成。
- 资产文件变化必须重新生成，哈希会更新。
- `runInstaller` 只允许 `trust: bundled`；当前只有 DLSS5 的 ReShade 安装器使用。
- 版本是 **pack 版本**，不是程序版本；程序版本通过 `minAppVersion` 约束。
