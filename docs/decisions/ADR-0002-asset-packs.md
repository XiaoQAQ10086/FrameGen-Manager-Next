# ADR-0002：资产包（Asset Pack）驱动部署

- 状态：已接受
- 日期：2026-10-29

## 背景

旧版把上游文件表、代理 DLL 列表、运行时目录、INI 键和哈希写死在 Rust 代码里。上游布局一变，就要改代码、改哈希表、重新发版；这是维护成本最高的部分之一。

## 决策

把“部署什么”变成数据，把“怎么部署”留在代码里。

每个资产包是文件夹 + `pack.json`：

- `id` / `version` / `schemaVersion` / `minAppVersion`
- `gpuRoutes`
- `files[]`：源路径、目标相对路径、角色（proxy/runtime/addon/model/shader）、是否必需、代理槽
- `ini`：目标、模板、合并模式
- `steps[]`：`copyFiles`、`writeIni`、`runInstaller`、`removeStaleFiles`
- `trust`：`bundled` / `verified`

代码只实现通用步骤执行器和校验器。

## 约束

- Pack 版本独立于程序版本；上游变化只发新 pack。
- Pack 必须通过校验：路径穿越、哈希、大小、步骤类型、目标目录白名单、`minAppVersion`。
- `runInstaller` 只允许受信任 pack；ReShade 安装器哈希固定在代码中，远程 pack 不能随意执行 EXE。
- 资产不进 git，作为 Release 资产发布；提供 `packs.json` 索引和离线包。
- 目标路径安全统一由引擎的 `SafePath`/白名单校验，不信任 pack 中的路径。

## 后果

- 上游布局变化不再触发程序发版。
- 需要维护 pack schema、校验器和发布流水线。
- 部署引擎必须足够通用，不能为单个资产族写特例。
