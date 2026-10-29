# 架构说明

## 1. 进程模型

单进程宿主：

```
WPF 宿主（FluentWindow + WebView2）
        ↓ WebMessage / PostWebMessageAsJson
AppWebBridge（App 层，命令路由 + 状态投影）
        ↓ DI
FrameGenManagerNext.Core（用例 + 端口）
        ↓
FrameGenManagerNext.Infrastructure（Windows / 文件 / 网络 / NVAPI / ReShade）
```

没有 Electron、没有 Node 运行时、没有本地 HTTP 服务、没有 sidecar。Web UI 是本地静态资源，通过 `https://fgm.local` 虚拟主机加载；所有系统操作仍由 C# 执行。长操作通过 `async` + `IProgress` + `CancellationToken` 执行，写操作由桥接层串行闸门和部署引擎锁保护。

## 2. 项目与依赖

```
FrameGenManagerNext.Domain          纯领域，零外部依赖
FrameGenManagerNext.Core            用例服务 + 端口接口，只依赖 Domain
FrameGenManagerNext.Infrastructure  端口实现，依赖 Core/Domain
FrameGenManagerNext.App             WPF/WebView2 宿主 + AppWebBridge，组合根引用 Infrastructure
FrameGenManagerNext.Cli             无 UI 验收入口
FrameGenManagerNext.Elevated        显卡名称伪装提权助手，只接受白名单参数
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
- `FGM_DATA_DIR` 可覆盖根目录，用于测试。
- 便携版在 exe 同目录放 `portable.flag`，数据写到 `data\`；不写注册表。

## 8. UI

- WPF 只保留 `FluentWindow` 外壳和 WebView2 宿主，业务界面全部在 `WebAssets/`。
- 页面：帧生成 / Smooth Motion / DLSS5 / 日志 / 设置 / 关于；GPU 状态嵌在帧生成页左侧。
- 前端结构：
  - `js/app.js`：启动、导航、语言和全局进度。
  - `js/api.js`：请求/响应与事件桥。
  - `js/i18n.js`：从 C# 获取本地化字符串。
  - `js/ui.js`：Toast、弹层、确认框和 DOM 辅助。
  - `js/pages/*.js`：每个页面一个模块。
- C# 桥接按功能拆分：`AppWebBridge.*.cs`，只暴露白名单命令。
- 耗时操作带进度事件、错误码和日志关联；封面由宿主转成 data URL，前端不直接读文件系统。

## 9. 原生互操作

- Win32：CsWin32 生成绑定（Restart Manager、Shell 图标、版本资源、UAC）。
- NVAPI：显式结构布局 + `LibraryImport`，隔离在 `Infrastructure/NvApi`，通过 `INvApi` 端口暴露。
- PE：`System.Reflection.PortableExecutable` + `FileVersionInfo`；必要时用 PeNet。
- 提权操作走独立白名单小工具，不整体提权主程序。

## 10. 更新与分发

- 便携版优先：`build/publish-portable.ps1` 生成自包含 win-x64 纯文件夹；先验收，后做安装包。WebView2 Runtime 使用系统 Evergreen 运行时。
- Velopack：每用户安装、增量更新、GitHub Releases（Phase 5）。
- 程序版本与资产包版本分离。
- 发布前校验 SHA-256；代码签名后续补上。
- 旧版仓库和旧更新源保持不变。

## 11. 非目标

- 不做跨平台。
- 不做帧生成渲染。
- 不读取/迁移旧版数据。
- 不在 v1 做 GPU 顶号（作为后续高级功能）。
- 不做通用插件系统；Asset Pack 是唯一扩展点。
