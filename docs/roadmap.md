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

验收：`pwsh ./build/verify.ps1` 全绿；空壳启动；CI 通过。

待完成：GitHub 仓库 `FrameGen-Manager-Next` 创建与首次 push（需要本机 `gh auth login` 或手动建仓）。

## Phase 1 · 领域与资产包

- `ErrorCode`、`Result<T>`、`SafePath`、INI 编辑器
- `pack.json` schema + 校验器
- 从旧项目生成 FrameGen / Smooth Motion / DLSS5 三个 pack
- CLI `pack validate`
- 行为规格：路径安全、INI、GPU 路由

验收：三个 pack 通过校验；规则单测通过。

## Phase 2 · 只读闭环

- GPU / 驱动信息
- 游戏库扫描（Steam / Epic / WeGame / 手动）
- 日志与诊断页

验收：同一台机器扫描结果与旧版一致；全程无写入。

## Phase 3 · 部署引擎 + 帧生成

- 统一部署/还原/状态检测
- 帧生成资产族
- 部署页面与进度/取消

验收：夹具覆盖回滚、文件占用、用户修改冲突；真实游戏部署/还原通过。

## Phase 4 · Smooth Motion + DLSS5

- NVAPI DRS 适配器
- ReShade 安装器与哈希校验
- 两个资产族接入同一引擎

验收：NVAPI 写后读回一致；ReShade 校验通过；硬件实测。

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
