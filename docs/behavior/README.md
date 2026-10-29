# 行为规格（Behavior Specs）

本目录是“旧版行为的唯一文字来源”。实现任何模块之前，先把规则写进对应的 `.md`，再写 C# 和测试。

## 为什么需要

旧项目由多个 agent 接手，规则散落在 Rust、JS、文档和真实游戏测试里。直接看旧代码实现新代码，会把旧 bug 和隐含假设一起搬过来。正确顺序是：

1. 从旧代码、旧测试、CHANGELOG、README 和真实反馈中提取规则。
2. 写成可验收的表格/清单。
3. 为每条规则写 C# 单元测试或夹具测试。
4. 实现。
5. 在真实硬件/游戏上跑对照清单。

## 模块清单

| 模块 | 旧版来源 | 新位置 | 状态 |
|---|---|---|---|
| 游戏库扫描 | `crates/core/src/scan.rs`、`ui/library.js` | `Core/Features/Library` + `Infrastructure/Library` | [已实现](library.md) |
| 渲染 EXE 识别 | `scan.rs::find_render_exe` 及评分逻辑 | `Domain/Library` | [已实现](library.md) |
| 图形 API 检测 | `scan.rs::pe_imports`、`api_from_facts` | `Domain/Library` + `Infrastructure/Library` | [已实现](library.md) |
| 帧生成部署 | `deploy.rs`、`ui/deploy.js` | `Core/Features/Deployment` | 待编写 |
| 备份/还原 | `deploy.rs`、`backup_restore.rs`、`sm.rs`、`dlss5.rs` | `Core/Features/Deployment` | 待编写 |
| INI 写入 | `update.rs::prepare_deploy_ini` | `Domain/Ini` | [已实现](ini.md) |
| Smooth Motion | `sm.rs`、`ui/sm.js` | `Core/Features/SmoothMotion` | 待编写 |
| DLSS5 / ReShade | `dlss5.rs`、`ui/dlss5.js` | `Core/Features/Dlss5` | 待编写 |
| GPU / 驱动闸门 | `gpu.rs`、`scan.rs::classify_gpu`、`ui/settings.js` | `Domain/Gpu` + `Infrastructure/Gpu` | [已实现](gpu.md) |
| 更新 | `update.rs`、`apps/electron/main.js` | Velopack + `Core/Features/Packs` | 待编写 |
| 诊断包 | `sidecar::diagnostics_export`、`ui/settings.js` | `Core/Features/Diagnostics` | 待编写 |
| 路径安全 | `util.rs::safe_join`、sidecar `checked_dir` | `Domain/Safety` | [已实现](path-safety.md) |
| 错误码 | `err.rs` 的关键词规则 | `Domain/Errors` | 已实现（枚举 + 上下文） |
| 资产包 schema/payload | 旧版 `deploy.rs` / `sm.rs` / `dlss5.rs` 的文件表 | `Domain/Packs`、`Infrastructure/Packs` | [已实现](asset-pack.md) |

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

- 规则必须可测试，避免“尽量”“智能判断”这类描述。
- 旧版的中文错误串只作为线索，不作为新接口的一部分。
- 旧 bug 单独列在“已知限制”或“修复”里，不要默认照搬。
- 每条规则都要能对应到一个 C# 测试，测试名写进表格。
- 旧项目保持只读；只引用路径，不复制代码。
