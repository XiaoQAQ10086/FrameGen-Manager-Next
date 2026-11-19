# 行为规格（Behavior Specs）

本目录是“旧版行为的唯一文字来源”。实现任何模块之前，先把规则写进对应的 `.md`，再写实现和测试。当前表格中的“新位置”仍指向冻结的 2.x C# 树；Rust/Flutter 对等过程中按切片迁移，迁移后的规格在文件内标注。

## 为什么需要

旧项目经过多次迭代，规则散落在 Rust、JS、文档和真实游戏测试里。直接看旧代码实现新代码，会把旧 bug 和隐含假设一起搬过来。正确顺序是：

1. 从旧代码、旧测试、CHANGELOG、README 和真实反馈中提取规则。
2. 写成可验收的表格/清单。
3. 为每条规则写 Rust 单元测试或夹具测试。
4. 实现。
5. 在真实硬件/游戏上跑对照清单。

## 模块清单

| 模块 | 旧版来源 | 新位置 | 状态 |
|---|---|---|---|
| 游戏库扫描 | `crates/core/src/scan.rs`、`ui/library.js` | `Core/Features/Library` + `Infrastructure/Library`；Rust 散装扫描 + 缓存 | [已实现](library.md) |
| 渲染 EXE 识别 | `scan.rs::find_render_exe` 及评分逻辑 | `Domain/Library`；Rust `fgm-infra/src/library` | [已实现](library.md) |
| 图形 API 检测 | `scan.rs::pe_imports`、`api_from_facts` | `Domain/Library` + `Infrastructure/Library`；Rust `fgm-infra/src/library` | [已实现](library.md) |
| 帧生成部署 | `deploy.rs`、`ui/deploy.js` | `Core/Features/Deployment` + `Infrastructure/Deployment` | [已实现](deployment.md) |
| 备份/还原 | `deploy.rs`、`backup_restore.rs`、`sm.rs`、`dlss5.rs` | `Infrastructure/Deployment` | [已实现](deployment.md) |
| INI 写入 | `update.rs::prepare_deploy_ini` | `Domain/Ini`；Rust `fgm-domain/src/ini` | [已实现](ini.md) |
| Smooth Motion | `sm.rs`、`ui/sm.js` | `Core/Features/SmoothMotion` + `Infrastructure/NvApi` | [已实现](smooth-motion.md) |
| ANR 帧生成 | apex-compute/ANR | `fgm-infra/src/anr` | [已实现（导入/部署/还原）](anr.md) |
| DLSS5 / ReShade | `dlss5.rs`、`ui/dlss5.js` | `Core/Features/Dlss5` + `Infrastructure/Processes`；Rust 离线 ReShade 安装器 | [已实现](dlss5.md) |
| GPU / 驱动闸门 | `gpu.rs`、`scan.rs::classify_gpu`、`ui/settings.js` | `Domain/Gpu` + `Infrastructure/Gpu`；Rust `fgm-infra/src/gpu` | [已实现](gpu.md) |
| 显卡名称伪装 | `gpu.rs::apply/restore/run_elevated` | `Infrastructure/Gpu`；Rust `fgm-infra/src/gpu_spoof` | [已实现并真机验证](gpu-spoof.md) |
| 设置 / 封面 | `ui/settings.js`、`ui/app.js` | `Infrastructure/Settings` + `Infrastructure/Library`；Rust `fgm-infra/src/settings` | [已实现（设置 + Steam 封面）](settings.md) |
| 更新 | `update.rs`、`apps/electron/main.js` | Velopack + `Core/Features/Packs`；Rust `fgm-infra/src/updates` | [已实现（检查/下载/安装）](updates.md) |
| 网络下载 | reqwest（旧版） | `fgm-infra/src/net`（WinHTTP） | [已实现（基础下载）](network.md) |
| 便携版 / 安装包 | Inno 6.7.4 | `build/publish-portable-next.ps1`、`installer-next.iss` | [已实现（本地构建）](packaging.md) |
| 诊断包 | `sidecar::diagnostics_export`、`ui/settings.js` | `Core/Features/Diagnostics` | [已实现](diagnostics.md) |
| 操作日志 | Serilog 文件 sink | `fgm-infra/src/logging` | [已实现](logging.md) |
| 路径安全 | `util.rs::safe_join`、sidecar `checked_dir` | `Domain/Safety`；Rust `fgm-domain` + `fgm-infra` | [已实现](path-safety.md) |
| 应用数据目录 | `Infrastructure/Paths/AppPaths.cs` | `fgm-infra/src/paths` | [已实现](app-paths.md) |
| 文件系统原语 | `Deployment/TargetLock.cs`、`DeploymentKey.cs`、原子写 | `fgm-infra/src/fs` | [已实现](filesystem-primitives.md) |
| 备份清单与备份存储 | `Deployment/BackupStore.cs` | `fgm-domain/src/deployment`、`fgm-infra/src/deployment` | [已实现](deployment.md) |
| 部署事务引擎 | `Infrastructure/Deployment/DeploymentEngine*.cs` | `fgm-infra/src/deployment` | [已实现（pack 规划 + 文件级）](deployment.md) |
| PE 导入读取 | `Infrastructure/Library/PeImportReader.cs` | `fgm-infra/src/pe` | [已实现](library.md) |
| 游戏进程检测 | `Infrastructure/Processes/GameProcessProbe.cs` | `fgm-infra/src/process` | [已实现](deployment.md) |
| 子进程运行 | `Infrastructure/Processes/ProcessRunner.cs` | `fgm-infra/src/process` | [已实现](process-runner.md) |
| NVAPI 驱动会话 | `Infrastructure/NvApi/*` | `fgm-infra/src/nvapi` | [已实现（会话/查找/读写）](smooth-motion.md) |
| 错误码 | `err.rs` 的关键词规则 | `Domain/Errors` | 已实现（枚举 + 上下文） |
| 资产包 schema/payload | 旧版 `deploy.rs` / `sm.rs` / `dlss5.rs` 的文件表 | `Domain/Packs`、`Infrastructure/Packs`；Rust Phase 1 | [已实现](asset-pack.md) |
| 内置资产升级 | 旧版升级时自动清理部署 | `Infrastructure/Deployment` + `Infrastructure/Packs` | [已实现](pack-upgrades.md) |
| 资产更新清除 | 无（新增） | `Core/Features/Maintenance` | [已实现](asset-maintenance.md) |
| Flutter 外壳 | 无（新增） | `ui/lib/app`、`ui/lib/bridge`、`ui/lib/features` | [已实现（全部页面）](flutter-shell.md) |

## 模板

```md
# <模块> 行为规格

## 输入
- ...

## 规则
| 编号 | 条件 | 行为 | 来源 | 测试 |
|---|---|---|---|---|
| LIB-001 | ... | ... | 旧文件:行 | `TestName` |

## 错误
| 场景 | ErrorCode | 用户可见结果 |
|---|---|---|

## 不做 / 已知限制
- ...

## 待确认
- ...
```

## 约束

- 规则必须可测试，避免“尽量”“自行判断”这类描述。
- 旧版的中文错误串只作为线索，不作为新接口的一部分。
- 旧 bug 单独列在“已知限制”或“修复”里，不要默认照搬。
- 每条规则都要能对应到一个 C# 测试，测试名写进表格。
- 旧项目保持只读；只引用路径，不复制代码。
