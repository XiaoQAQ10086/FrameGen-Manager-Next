# REFramework 行为规格

## 目标

只为 **RE Engine** 游戏提供可选的 REFramework 组件，避免《怪物猎人：世界》等 MT Framework 游戏被误装 `dinput8.dll`。REFramework 不是“所有卡普空游戏”的通用补丁。

## 引擎判断

`Infrastructure/Library/EngineDetector`：

1. 在安装根目录、部署目录及其最多 3 级父目录查找：
   - `re_chunk_000.pak`
   - `re_chunk_000.pak.patch_*.pak`
   - `re_dlc_000.pak`
2. 命中任一标记 → `GameEngine.ReEngine`。
3. 没有 RE Engine 标记、但 EXE 名属于 MT Framework 已知集合（`mhw`、`mhworld`、`re5`、`re6`、`dmc4`）→ `MtFramework`。
4. 否则 `Unknown`。
5. 不通过 Steam/Epic 的 publisher 字段判断，避免联网、限流和非 Steam 游戏失效。

## 安装

- 关于页“RE 引擎”卡片的“RE 引擎自动安装 REFramework”开关默认开启，保存在 `AppSettings.AutoInstallReframework`。
- 仅在 `game.engine == ReEngine` 且该开关开启时，部署**帧生成或 DLSS5**都会自动安装 REFramework，不在详情页额外询问；已经存在时跳过并弹窗说明。
- REFramework 使用独立 pack `reframework`，文件为 `dinput8.dll` + `reframework_revision.txt`，版本和 SHA-256 固定在 `pack.json`。
- 安装前由部署引擎备份同名 `dinput8.dll`；卸载时只有当哈希仍匹配部署值才恢复/删除，用户替换过的文件保留并报告冲突。
- REFramework 安装失败时中止本次 DLSS5 安装，避免出现“DLSS5 已装但依赖没装”的半成品状态。
- 卸载 DLSS5 时尝试卸载本工具安装的 REFramework；没有我们的 manifest 时不动其他工具安装的同名文件。
- REFramework 使用 `dinput8.dll`，帧生成的代理选择必须避开该入口。帧生成页面收到 `reframeworkProxy=dinput8.dll` 时禁用该选项并提示。

## 资产来源

- 官方：`praydog/REFramework-nightly`。
- 当前固定 nightly revision `f00d6bee503125180621a6666779499f9a30881d`，`REFramework.zip` SHA-256 `9b3f82dbad5599d3b67082bc89ca5a6b990fcf02416ea32490a630cea2f2d9d0`。
- 包内 `dinput8.dll` SHA-256 `748d6543b45a5ddd6b2d5170d1ba16ed56219c7f0d52141797a95c3bda189e5f`。
- 上游更新需要替换 pack、更新 SHA，并在 RE Engine 真机验证；不自动跟随 latest。

## 测试

- `Infrastructure.Tests/EngineDetectorTests.cs`：RE Engine 标记、补丁 pak、嵌套目录、MT Framework、干净目录。
- `docs/behavior/dlss5.md` 记录 UI 与卸载联动。
