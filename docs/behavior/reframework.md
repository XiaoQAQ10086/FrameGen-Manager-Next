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


## Rust 迁移状态（Phase 4）

- `fgm-infra/src/library/engine_detector.rs`：从目标目录、游戏库安装目录和 EXE 父目录分别向上最多 4 级查找 `re_chunk_000.pak`、`re_chunk_000.pak.patch_*.pak`、`re_dlc_000.pak`；命中判为 RE Engine；否则按 EXE 名（`mhw`/`mhworld`/`re5`/`re6`/`dmc4`）判为 MT Framework，其余 Unknown。
- `fgm-infra/src/reframework.rs`：`install_before_deploy` 读取 `config/app.json` 的 `autoInstallReframework`，只在开关开启、引擎为 RE Engine、目标目录没有 `dinput8.dll` 且本地存在 `reframework` pack 时部署；部署走统一引擎，备份/回滚与哈希校验一致。`uninstall` 只处理本工具自己的 manifest，绝不按文件名删除其他工具的 `dinput8.dll`。
- 接入点：帧生成部署（`fgm_deploy_json`）与 DLSS5 安装在写入前调用；DLSS5 卸载在还原后调用 REFramework 卸载。
- 帧生成部署前会读取 `gameModes.engine`；RE Engine 且自动安装开关开启时，代理选择禁用 `dinput8.dll`，后端也会在 REFramework 安装前返回 `Conflict`，避免先装 REFramework 再被帧生成覆盖。
- 已存在 `reframework_revision.txt` 或本工具 reframework manifest 时跳过安装，不覆盖用户已有 REFramework。
- 已知差异：pack 缺失时静默跳过而不是报错，避免没有资产的构建阻塞主功能。
- 测试：`engine_detector.rs` 单测（chunk/patch/安装根目录/MT/未知）、`tests/reframework.rs`（非 RE Engine 跳过、已有 revision 跳过、dinput8 代理冲突、无 manifest 卸载不动第三方文件）。完整 pack 需要 23 MB 资产，未纳入 CI 集成测试。

## 资产来源

- 官方：`praydog/REFramework-nightly`。
- 当前固定 nightly `nightly-01441`，revision `49c21b26d50b23a333e9025743c8d45c46bb460e`（2026-11-17 发布），`REFramework.zip` SHA-256 `49a897335cea7368885477426e90a524659329e2ac74dde302eb5769ba777c11`。
- 包内 `dinput8.dll` SHA-256 `28222866b3fb8182f522b6eb8d8bf9d04cec08a4e87bfc302c5aa0374b8e2b95`，大小 23,097,856 bytes。pack 版本从 `1.0.0` 升到 `1.1.0`；旧版本部署会被资产更新流程识别为可原位升级，用户点击“升级”后替换为新资产并保留原始备份。
- 上游更新需要替换 pack、更新 SHA，并在 RE Engine 真机验证；不自动跟随 latest。

## 测试

- `Infrastructure.Tests/EngineDetectorTests.cs`：RE Engine 标记、补丁 pak、嵌套目录、MT Framework、干净目录。
- `docs/behavior/dlss5.md` 记录 UI 与卸载联动。
