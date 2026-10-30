# 内置资产升级与迁移

目标：程序升级后，已经部署过旧版内置资产的游戏能够安全地过渡到新资产，同时保留原始文件备份和可还原能力。

## 术语

- **程序版本**：`FrameGenManagerNext` 的版本，来自 `AppInfo.Version`。
- **Pack ID**：资产族标识，例如 `framegen.sm86`。一个功能对应一个稳定 ID。
- **Pack 版本**：`pack.json` 里的 `version`，与程序版本独立。
- **Manifest**：某次部署写入的 `deployment.json`，记录 pack id/version、每个目标文件的 SHA-256 和原始备份位置。

## 当前实现（2.0.0）

### 1. Pack 定位

`PackRepository` 依次扫描：

1. `FGM_PACKS_DIR`
2. `<exe>/packs`
3. 数据目录 `packs`
4. 仓库开发目录 `packs` / `assets/packs`

每个根目录下同一个 pack id 只允许一个 `pack.json`。现在不再“先找到哪个用哪个”，而是选择**版本最高且通过 schema 校验、`minAppVersion <= 当前程序版本`** 的那一份。没有兼容版本时回退到第一个可读 pack，由业务层返回精确的 `PackInvalid` 错误。

这样后续把下载资产放进数据目录时，旧下载不会遮蔽程序自带的新版资产。

### 2. 升级检测

打开游戏列表时，`DeploymentEngine.InspectAsync` 用当前 pack 重新生成计划，并与 manifest 比较：

- manifest 的 `PackId` 或 `PackVersion` 不同；
- 同一个相对路径的期望哈希不同；
- 新 pack 增加了 manifest 中没有的文件。

任一成立 → `Outdated`。文件内容被用户或第三方修改 → `Conflict`；文件缺失 → `Partial`。检查只读，不写游戏目录。

检查使用 manifest 中记录的部署选项（代理槽、优化等级、帧数、Preset、引擎、超分、XeSS），而不是弹窗默认值。这样用户改过的选项不会被误判成资产过期，升级时也能沿用原选项。

### 3. 用户可见行为

- 游戏卡片显示“需更新”。
- 主按钮从“部署”变成“更新”。
- 列表上方出现提示：`有 N 个游戏的内置资产已更新。点击“更新”即可升级到当前版本，原有文件备份会保留。`
- 不会在启动时静默改游戏目录。必须由用户点击“更新”。

### 4. 原地升级

`DeployAsync` 对旧 manifest 执行事务式升级：

1. 获取目标锁，拒绝并发写。
2. 对每个新文件：
   - 旧 manifest 已有原始备份 → 复用该备份，**绝不覆盖成我们自己的文件**；
   - 文件不存在 → 记录 `ExistedBefore=false`；
   - 当前文件哈希等于新 pack 或旧 pack 记录 → 视为我们写入的文件，不做新备份；
   - 其他情况 → 备份为原始文件。
3. 写 `Installing` manifest，临时文件 + 原子替换 + 写入后哈希校验。
4. 清理旧 manifest 中已不在新 pack 的文件：
   - 内容仍是我们写入的 → 有原始备份则恢复原文件，否则删除；
   - 内容被改过 → 保留并给出警告。
5. 写 `Installed` manifest，pack id/version 和文件哈希全部更新。
6. 任一步失败 → 回滚已写文件，manifest 标记 `RolledBack`。

因为原始备份始终保留，升级后仍然可以“还原”到安装帧生成之前的原文件。还原只依赖最新 manifest，不需要旧 pack 本体。

### 5. 兼容性

- `minAppVersion` 高于当前程序版本的 pack 不会被选中。
- `schemaVersion` 只支持 1。未来若必须破坏兼容，提升 schemaVersion 并让新程序同时支持旧版读取；旧程序遇到新 schema 会明确报 `PACK-001`，不会误部署。
- 降级程序不做自动迁移保证；便携版可以换回旧目录，已部署文件按最新 manifest 还原或重新部署。

## 发布流程约束

1. **pack ID 稳定**。同一个功能只改 `version`，不改 ID；文件增删、改名、换目录都只发新 pack。
2. **版本只增不复用**。`version` 使用 `x.y.z`；已发布版本的内容不可原地修改，否则哈希和 manifest 的语义会被破坏。
3. **程序与 pack 分开发布**。上游资产更新不必发程序；只有出现新步骤类型、新部署语义时才提升 `minAppVersion` 并升级 schema。
4. **打包前跑 payload 校验**。`fgm-next pack validate` 必须检查存在性、大小、SHA-256、INI 模板和安装器哈希。
5. **同一版本内不改哈希**。修 bug 时递增 patch 版本，例如 `6.8.0` → `6.8.1`。

## 后续演进（下载资产 / 多版本并存）

首版资产随程序分发，升级路径已经闭环。引入在线资产后按以下策略扩展：

1. 数据目录使用 `packs/<pack-id>/<version>/`，同时保留当前版本和上一版本。
2. Release 发布 `packs.json` 索引：pack id、version、minAppVersion、schemaVersion、URL、SHA-256、大小、发布时间、是否强制。
3. 下载到 staging 目录，校验通过后再原子移动到版本目录；下载失败不影响现有部署。
4. `PackRepository` 继续按“最高兼容版本”选取，因此已下载的新版本会自然生效。
5. 部署仍由用户确认；强制更新只改变提示强度，不在启动时静默写游戏目录。
6. 只允许 `trust=bundled` 的 pack 执行安装器；远程 pack 永远不能运行 EXE。
7. 索引和 pack 都带哈希；后续可加签名，但首版先保证 HTTPS + SHA-256。

## 测试

- `DeploymentEngineTests.UpgradingPackKeepsOriginalBackupsAndRemovesStaleFiles`：
  部署 v1 → 检查 v2 为 `Outdated` → 升级到 v2（替换文件、清理旧文件、新增文件）→ 还原后原始内容恢复、新增文件移除。
- pack schema / payload 测试见 `AssetPackValidatorTests`、`PackJsonSerializerTests`、`PackPayloadValidatorTests`。

## 已知限制

- 升级只覆盖同一 `familyId` 的部署；换功能目录需要用户先还原旧部署。
- 文件被占用时整次升级在写入前失败，不会留下半套新文件。
- 用户改过的旧文件不会被删除，会在升级警告中列出。
- 不做跨大版本 manifest 链式迁移；manifest 是自包含的，只需要 pack 哈希和备份路径。
