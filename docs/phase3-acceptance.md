# Phase 3 本地验收记录

日期：2026-10-29  
范围：统一部署引擎、备份/还原、FrameGen 部署、游戏库 UI、便携版内置资产包。

## 夹具验收

目标目录：

```
artifacts/deploy-test2/
  version.dll        ← 原有文件
  dlssg_sm86.ini     ← 原有文件
```

执行：

```powershell
fgm-next deploy framegen artifacts/deploy-test2
fgm-next deploy inspect artifacts/deploy-test2
fgm-next deploy restore artifacts/deploy-test2
```

结果：

| 步骤 | 结果 |
|---|---|
| 部署 | `Installed`，写入 8 个文件，备份 2 个原文件 |
| 检查 | `Installed`，pack 0.4.1 |
| 还原 | `restored=2 removed=6 conflicts=0` |
| 还原后 | `version.dll` / `dlssg_sm86.ini` 内容恢复；`dlssg_sm86\` 目录移除 |
| 再检查 | `NotDeployed` |

## UI 验收

通过 UI Automation 在临时“游戏”条目上点击“部署”和“还原”：

- 部署确认框 → 回车确认 → 约 10 秒内状态变为 **已部署**，顶部显示“部署完成：AAA UI Fixture”。
- 还原确认框 → 回车确认 → 状态回到 **未部署**，顶部显示“还原完成：AAA UI Fixture”。
- 测试条目随后从库中移除，未触碰真实游戏目录。

## 便携版验收

内置 `packs/framegen.sm86` 后在便携目录执行：

```powershell
fgm-next.exe deploy framegen <fixture>
fgm-next.exe deploy inspect <fixture>
fgm-next.exe deploy restore <fixture>
fgm-next.exe pack validate packs/framegen.sm86
```

结果：部署 `Installed`（8 files，2 backups）、还原 `restored=2 removed=6`、原文件恢复；pack 全量哈希校验通过。

## 门禁

- `build/verify.ps1 -Configuration Release`：通过。
- 测试：196/196。
- 格式检查、质量检查、四个 pack schema 校验：通过。
- 128 个 C# 文件通过结构检查。

## 已知限制

- UI 部署使用默认选项（`version.dll`、优化 1、4X、Preset A、DLSS）；完整代理/画质选项尚未接入。
- 尚未在真实游戏上执行部署对照；夹具与便携版已验证写入/备份/还原逻辑。
- 文件被游戏占用时直接拒绝，不自动结束游戏进程。
- 目标目录位于 Program Files 等受保护目录时会被拒绝。
- 窗口系统标题栏与自定义标题栏重复，属于外观问题。

## 下一步

- Smooth Motion 和 DLSS5 接入同一部署引擎。
- 完整代理/画质选项 UI。
- 真实游戏部署/还原对照，然后生成 Velopack 安装包。
