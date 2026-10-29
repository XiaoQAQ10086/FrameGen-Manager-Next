# 架构说明

## 1. 进程模型

单进程：

```
WPF View / ViewModel
        ↓ DI
FrameGenManagerNext.Core（用例 + 端口）
        ↓
FrameGenManagerNext.Infrastructure（Windows / 文件 / 网络 / NVAPI / ReShade）
```

没有 sidecar，没有 stdin/stdout JSON 协议，没有第二语言运行时。长操作通过 `async` + `IProgress<OperationProgress>` + `CancellationToken` 执行，写操作由 `OperationRunner` 串行化。

## 2. 项目与依赖

```
FrameGenManagerNext.Domain          纯领域，零外部依赖
FrameGenManagerNext.Core            用例服务 + 端口接口，只依赖 Domain
FrameGenManagerNext.Infrastructure  端口实现，依赖 Core/Domain
FrameGenManagerNext.App             WPF，依赖 Core，组合根引用 Infrastructure
FrameGenManagerNext.Cli             无 UI 验收入口
```

依赖规则由项目引用和架构测试共同保证：

- Domain/Core 不能引用 `PresentationFramework`、`PresentationCore`、`WindowsBase`、`System.Windows.Forms`。
- P/Invoke 只能出现在 Infrastructure。
- Core/Domain 不能出现用户可见文案。

## 3. 功能切片

```
Core/Features/
  Library/          游戏库扫描、手动添加、封面缓存
  Deployment/       统一部署/还原/状态检测
  Framegen/         帧生成资产族与 INI 策略
  SmoothMotion/     NVIDIA 驱动配置与组件部署
  Dlss5/            ReShade / GITC Uplift 资产族
  Gpu/              GPU/驱动检测与路由
  Packs/            资产包获取、校验、版本
  Diagnostics/      日志、诊断包
  Settings/         配置读写与校验
```

每个功能自带服务接口、实现、模型和测试。功能之间通过接口或领域事件协作，禁止直接引用另一个功能的内部类型。

## 4. Asset Pack

上游布局是数据，不是代码：

- `pack.json` 描述文件、目标路径、角色（proxy/runtime/addon/model/shaper）、INI 模板、步骤和信任级别。
- 代码只实现通用步骤执行器；新上游版本 = 新 pack。
- Pack 独立版本、独立发布、按需下载，带 SHA-256、断点续传、镜像回退和离线包。
- 校验器在 CI 和新版加载时都运行：路径穿越、哈希、大小、步骤类型、目标白名单、`minAppVersion`。

详细决策见 [ADR-0002](decisions/ADR-0002-asset-packs.md)。

## 5. 部署引擎

所有资产族共用一个引擎：

```
PlanAsync
  → 校验目标目录 / 游戏进程 / 代理槽
  → 获取目标锁
  → 备份原件（BackupStore）
  → 临时文件写入
  → 原子替换
  → 校验哈希
  → 写 manifest（状态机）
```

Manifest 状态：

```
planned → backingUp → writing → verifying → installed
                                      ↘ rolledBack / partial
```

关键规则：

- 备份目录按目标路径 SHA-256 建键，统一存 `.bak` 和 `manifest.json`。
- 还原只删除“哈希仍等于部署时哈希”的文件；冲突文件保留并报告。
- 路径安全统一走 `SafePath`。
- 占用检测优先用 Restart Manager，不用 PID 猜测。
- 不在启动时自动清理旧部署。

## 6. 错误模型

- Domain：`ErrorCode` 枚举 + 参数对象。
- Core：返回 `Result<T>` / `AppError`，不返回用户文案。
- App：resx 按语言渲染，附错误码和诊断入口。
- 日志：英文结构化，带 operation id。

## 7. 数据与持久化

```
%LOCALAPPDATA%\FrameGenManagerNext\
  config\app.json
  library\library.json
  packs\<pack-id>\<version>\
  backups\<family>\<sha256(target)>\
  logs\
  cache\covers\
  state\operations\
```

- 所有 JSON 带 `schemaVersion`。
- 写入使用“临时文件 + 原子替换”；损坏文件隔离为 `.bad`。
- 不读取旧版数据；不做多跳迁移。
- `FGM_DATA_DIR` 可覆盖根目录，用于测试和便携场景。

## 8. UI

- Wpf.Ui `FluentWindow` + NavigationView 外壳。
- 页面：游戏库 / 帧生成 / Smooth Motion / DLSS5 / 日志 / 设置 / 关于。
- MVVM：CommunityToolkit.Mvvm；View 只绑定 ViewModel。
- 所有耗时操作走 `OperationRunner`，带进度、取消、错误展示和日志关联。
- 游戏列表虚拟化；封面异步加载并缓存。

## 9. 原生互操作

- Win32：CsWin32 生成绑定（Restart Manager、Shell 图标、版本资源、UAC）。
- NVAPI：显式结构布局 + `LibraryImport`，隔离在 `Infrastructure/NvApi`，通过 `INvApi` 端口暴露。
- PE：`System.Reflection.PortableExecutable` + `FileVersionInfo`；必要时用 PeNet。
- 提权操作走独立白名单小工具，不整体提权主程序。

## 10. 更新与分发

- Velopack：每用户安装、增量更新、GitHub Releases。
- 程序版本与资产包版本分离。
- 发布前校验 SHA-256；代码签名后续补上。
- 旧版仓库和旧更新源保持不变。

## 11. 非目标

- 不做跨平台。
- 不做帧生成渲染。
- 不读取/迁移旧版数据。
- 不在 v1 做 GPU 顶号（作为后续高级功能）。
- 不做通用插件系统；Asset Pack 是唯一扩展点。
