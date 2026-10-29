# assets

本目录只用于本地开发和打包，**二进制资产不进 git**。

预期布局：

```
assets/
  framegen/          帧生成代理与运行时（来自旧项目 bundled-assets）
  smooth-motion/     SM 解锁组件（来自 bundled-sm）
  dlss5/             ReShade / GITC Uplift / 模型（来自 bundled-dlss5）
  offline/           可选的离线资产包
```

Phase 1 会提供 `build/fetch-assets.ps1`：

- 默认从旧项目只读复制：
  - `D:\AI Work\FrameGen Manager\dlssg-manager\apps\electron\bundled-assets`
  - `D:\AI Work\FrameGen Manager\dlssg-manager\bundled-sm`
  - `D:\AI Work\FrameGen Manager\dlssg-manager\apps\electron\bundled-dlss5`
- 生成资产清单与 SHA-256。
- 生成 `pack.json` 与 `packs.json`。

规则：

- 不把 EXE/DLL/模型提交到 git。
- 任何资产替换都必须更新哈希清单并通过 `pack validate`。
- 远程安装器（ReShade）必须固定哈希，且只允许受信任 pack 执行。
