# 路线图

## Phase 0 · 基础脚手架（当前）

- [x] 安装 .NET 10 SDK，锁定 10.0.4xx
- [x] `FrameGenManagerNext.slnx` + 5 个源项目 + 4 个测试项目
- [x] 中央包版本、分析器、Nullable、warnings-as-errors
- [x] WPF + Wpf.Ui 空壳、DI、Serilog、数据目录
- [x] CLI `--version` / `--paths`
- [x] xUnit v3 + MTP 测试跑通
- [x] `dotnet format` 门禁
- [x] CI + 质量检查脚本
- [x] 本地 git 初始化与初始提交
- [x] Wpf.Ui / Velopack 兼容性结论写入 [ADR-0005](decisions/ADR-0005-phase0-compatibility.md)
- [x] GitHub 仓库 `FrameGen-Manager-Next` 创建与首次 push
- [x] GitHub Actions CI 通过

验收：`pwsh ./build/verify.ps1` 全绿；空壳启动；CI 通过。

仓库：<https://github.com/XiaoQAQ10086/FrameGen-Manager-Next>

## Phase 1 · 领域与资产包（进行中）

- [x] `ErrorCode`、`AppError`、`Result<T>`
- [x] `SafePath` 路径安全规则
- [x] INI 解析/编辑/stock 覆盖
- [x] `pack.json` schema + 校验器 + 严格 JSON 读取
- [x] payload 校验（存在性、大小、SHA-256、INI 模板、安装器）
- [x] 从旧项目生成 FrameGen / Smooth Motion RTX30/RTX20 / DLSS5 四个 pack
- [x] CLI `pack validate [--no-hash] [--schema-only]`
- [x] 行为规格：路径安全、INI、资产包
- [x] 便携版发布流程（自包含 win-x64 + `portable.flag`）
- [ ] GPU 路由行为规格（Phase 2 补）

验收：四个 pack 通过完整哈希校验；112 个测试通过；便携版可启动。

> 安装包（Velopack）不在此阶段生成；先交付便携版给用户验收，通过后再做安装器。

## Phase 2 · 只读闭环（已完成本地验收）

- [x] GPU / 驱动 / HAGS 读取（nvidia-smi + 注册表）
- [x] GPU 路由与 R580 / 616.64 闸门（Domain 纯函数）
- [x] Steam / Epic / WeGame / 散装 / 手动游戏扫描
- [x] 渲染 EXE 评分（旧版加权规则）
- [x] PE 导入 + 文件名 + wrapper + UnityPlayer 回退的 API 检测
- [x] `library.json` 缓存、忽略列表、手动条目
- [x] WPF 四页：游戏库、GPU 状态、日志与诊断、关于
- [x] CLI：`gpu`、`library list/scan`、`logs tail`
- [x] 行为规格：`library.md`、`gpu.md`

验收（2026-10-29）：真机扫描 7 个游戏（旧缓存 6 个 + 新增散装 1 个）；霍格沃茨/PUBG/VTube/Nomad DX12、Wallpaper DX11、CS2 Unknown，与旧版一致；GPU RTX 3070 / SM86 / 621.01。全程未写入游戏目录。详见 [Phase 2 验收记录](phase2-acceptance.md)。

## Phase 3 · 部署引擎 + 帧生成（本地夹具验收完成）

- [x] 统一部署引擎：plan → lock → backup → atomic write → verify → manifest
- [x] 备份存储、manifest 状态机、跨进程文件锁
- [x] 冲突/缺失/锁定/无 manifest 状态检测
- [x] FrameGen 选项校验与 INI 生成
- [x] FrameGen 部署/还原/检查（CLI + 游戏库 UI）
- [x] 便携版内置 FrameGen 资产包
- [ ] 完整代理/画质选项 UI
- [ ] 真实游戏部署/还原对照

验收（夹具 + 便携版 + UI）：部署 8 个文件、备份 2 个原文件；检查 `Installed`；还原后原文件内容恢复、运行时目录移除；196 个测试通过。详见 [Phase 3 验收记录](phase3-acceptance.md)。

## Phase 4 · Smooth Motion + DLSS5（进行中）

- [x] NVAPI DRS 适配器：查 profile / 建 profile / 读写设置 / 保存 / 新会话复核
- [x] Smooth Motion 组件部署（RTX30 / RTX20 pack）与还原
- [x] Smooth Motion 页面与 CLI（inspect / enable / disable / restore）
- [x] 真机验证：RTX 3070，写 1 读回 1，写 0 读回 0，还原后组件移除
- [x] DLSS5 核心：API 探测、ReShade 安装器、哈希校验、114 个文件部署、还原
- [ ] DLSS5 页面与 GPU 闸门、ReShade 卸载/旧版清理

验收（SM + DLSS5 核心）：NVAPI 写后读回一致；DLSS5 真机安装/还原通过；197 个测试通过。

## Phase 5 · 发布

- Velopack 安装/更新
- 中英文 resx
- 主题与页面完善
- 诊断包
- 离线资产包

验收：安装/更新/卸载完整走通；发布候选。

## Phase 6 · 验收与发布

- 真实游戏/硬件对照清单
- bug 修复
- 旧仓库迁移公告与归档

## 后续

- GPU 顶号（高级功能，独立提权工具 + 明确风险提示）
- 代码签名（Azure Trusted Signing 等）
- 可选便携版
