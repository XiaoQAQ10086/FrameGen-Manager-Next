# DLSS5 / ReShade 行为规格

来源：旧版 `dlss5.rs`、`ui/dlss5.js` 与真机安装器验证。
新实现：`Core/Features/Dlss5`、`Infrastructure/Dlss5`、`Infrastructure/Processes`、`dlss5.reshade-gitc` pack。

## 前置条件

- 从游戏渲染 EXE 探测图形 API；映射到 ReShade 参数：
  - DX9 → `d3d9`；DX10 → `d3d10`；DX11 → `d3d11`；DX12 → `d3d12`；Vulkan → `vulkan`；OpenGL → `opengl`。
- API 无法识别时拒绝安装。
- GPU 不是 SM75/SM86 时由 pack 的 `gpuRoutes` 在后续 UI 层提示；当前 CLI 不做 GPU 闸门。

## 安装流程

目标与 API 的约定：

- 部署目标来自界面的 EXE 选择：默认用扫描结果，也可以“手动选择游戏实际启动 EXE”。手动选择会同时更新该游戏的部署目标目录（与帧生成页一致），安装文件与 ReShade 都写到该 EXE 所在目录；即使扫描阶段没有找到渲染 EXE，手动选择后也允许安装。
- 图形 API 由扫描阶段自动判定，详情页把它作为默认选项并在后面标注“（自动）”；不再提供单独的“自动”行。用户没有改过该选项时按自动模式安装：DX10/11/12 统一使用 `dxgi.dll`（与上游分析器和旧项目一致）。用户手动改成其他 API 时按所选 API 安装，例如显式 DX12 使用 `d3d12.dll`。
- 2.0.7 之前自动模式会误写 `d3d12.dll`。升级后旧部署需要先“卸载”再重新部署，才能切到 `dxgi.dll`；只重新部署不会自动删除旧代理。
- Unity 游戏按 Unity 的 Windows 默认值判定为 D3D11；只有目录里带 `D3D12Core.dll`（D3D12 构建）或 `vulkan-1.dll`（Vulkan 构建）时才判为对应 API。ReShade 自己的代理 DLL（版本信息 ProductName=ReShade）不参与判定，否则装过 ReShade 后重新扫描会把 API 判错。

1. 校验目标目录、pack schema、安装器 SHA-256。
2. 通过统一部署引擎部署 pack 中的 114 个文件（当前 pack `6.8.1`，GITC Uplift `1.8.0`）：
   - `gitc-uplift.addon64`、`gitc-uplift.addon32`、`gitc-uplift-helper64.exe`
   - `GITC-Uplift/nvngx_dlssnr.dll`（534 MB 模型，1.8.0 未更换）
   - `reshade-shaders/Shaders/...`、`reshade-shaders/Textures/...`
   - `Uplift.fx`、`UpliftMask.fx`（Uplift.fx 随 1.8.0 更新，新增 motion 权限复选框；UpliftMask.fx 未变）
   - GITC 1.8.0 新增 Before Framegen（DX12/Vulkan Streamline 路由），已有设置会保留；pack 版本升到 6.8.1，已部署用户可以检测到资产更新。
3. 非 Vulkan 优先离线安装 ReShade（不联网、不运行 setup）：
   - 从 `ReShade_Setup_6.8.0_Addon.exe` 的 512 字节步进内嵌 ZIP 取出 `ReShade64.dll`（32 位游戏取 `ReShade32.dll`）。该 DLL 与官方 setup 写出的文件哈希一致。
   - 按最终代理写成代理文件名：自动模式下 DX10/11/12 使用 `dxgi.dll`；显式选择时使用该 API 自己的代理（`d3d9/d3d10/d3d11/d3d12/opengl32.dll`）。目标位置已有非 ReShade 文件时拒绝覆盖并回滚（`Conflict`）。
   - 目标没有 `ReShade.ini` 时写入有效默认配置；随后由配置服务把搜索路径合并为 `.\reshade-shaders\Shaders\**` / `Textures\**`。
   - 只有离线解包失败时才自动回退到官方 setup（`--headless --api <api>`，超时 240 秒）；界面不提供手动选择官方安装器的入口。
   - Vulkan 需要全局层，无法离线落地，只能使用官方 setup。
   - 标准 ReShade shader/纹理和 GITC 指定的 fx 都在 pack 的 114 个文件里，随部署一起写入。
   - 离线路径实测完整部署约 1–2 秒；官方 setup 的联网阶段约 60–100 秒，正常安装不经过它。
4. 失败时回滚已部署文件。
5. 界面在已部署（`Installed`）时锁定安装按钮；`Outdated`/`Partial`/`Conflict` 显示“更新”。`InstalledWithoutManifest`（文件存在但清单丢失）显示“重新部署”，不永久锁死。详情页提供“打开文件夹”。
6. 安装/卸载期间详情页显示实时进度条：文件写入按数量推进，ReShade 模块阶段显示不确定进度。操作成功或失败后都关闭全局进度卡片；ReShade 阶段使用不确定进度时不会再残留“正在安装 ReShade”。

安装前会检查帧生成是否已占用同一个代理文件（例如帧生成使用 `dxgi.dll`、DLSS5 选择 DXGI 代理）：占用时返回 `Conflict`，避免覆盖帧生成的代理入口。

## 状态检查

- pack 文件状态来自部署引擎（未部署/已部署/需更新/不完整/冲突）。
- 清单缺失但目录里仍有与当前 pack 哈希一致的文件时，状态为 `InstalledWithoutManifest`；界面允许“重新部署”或“卸载”，不会永久锁死。
- 通过文件版本信息识别 ReShade 模块（`d3d9/d3d10/d3d11/d3d12/dxgi/opengl32.dll`，ProductName/FileDescription 含 `ReShade`），避免误删游戏自带 DLL。
- 同时报告旧版 DLSS5/RenoDX 残留：根目录 `renodx-dlss*.addon*`（含第三方整合包常见的 `renodx-dlss-SF-*.addon64`）或旧布局根目录 `nvngx_dlssnr.dll`；命中项写入 `legacyFiles`，详情页逐项列出，避免用户误以为是本软件部署过 ReShade。
- 检测到旧版 DLSS5/RenoDX 时拒绝安装 GITC pack，返回 `LegacyInstallPresent`，必须先执行“清理旧版文件”。这不是按本软件的部署记录判断，而是按目录里的实际文件判断；文件可能来自其他 DLSS5 整合包或手动安装。

## 还原

- 通过部署引擎还原 114 个 pack 文件，恢复部署前备份。
- **不自动运行 ReShade 卸载器**；ReShade 模块、`ReShade.ini`、`ReShade.log`、`ReShadePreset.ini` 会保留，并明确提示。
- 需要彻底清理时使用“卸载”。

## 彻底卸载

界面只有在检测到已部署（含旧版 DLSS5/RenoDX/残留）时才允许点“卸载”；只有旧版文件、没有本软件部署时，按钮文字显示“清理旧版文件”。

1. 先调用部署引擎还原并清理清单/备份：删除仍匹配部署哈希的文件，恢复部署前已有的原文件；用户改过的文件保留并报告冲突。
2. 读取卸载前的清单，记录 `ExistedBefore=true` 的路径。这些是游戏原有文件，后续按 pack 清理时必须跳过。
3. 先尝试 ReShade 官方卸载器（`--state uninstall`），最多等 30 秒；超时或失败不报错，继续直接删除。直接删除是最终保证，不依赖任何网络。
4. 删除 ReShade 模块、`ReShade.ini`、`ReShade.log`、`ReShadePreset.ini`。
5. 删除 GITC 组件（`gitc-uplift.addon32/64`、`gitc-uplift-helper64.exe`、`GITC-Uplift/`）与已知 shader。
6. 按当前 pack 的文件表扫描 `reshade-shaders/Shaders/**` 与 `reshade-shaders/Textures/**`：哈希匹配的残留直接删除；用户改过的文件保留并计入剩余项。此步骤覆盖“官方 setup 写入或清单已丢失”的纹理/着色器残留。
7. 清理旧版 DLSS5/RenoDX：`renodx-dlss*.addon*`、旧布局根目录 `nvngx_dlssnr.dll`。
8. 清理插件运行时残渣（`renodx-dlss-report*`、`renodx-dlss-*`、`ReShade.ini.renodx-dlss*`、崩溃日志、trace）。
9. 仅当 `D3D12\` 只有 `D3D12Core.dll` 且版本信息含 Microsoft 时删除该 Agility SDK 文件。
10. 删除空目录后重新扫描；剩余项包含仍存在的 pack 文件。只有剩余项为 0 才丢弃清单和备份。
11. 返回 `removed` / `remaining` / `clean`；界面在 `clean=false` 时显示“卸载未完成”错误，而不是提示成功。删除是永久的，不进回收站。

## 命令

```powershell
fgm-next dlss5 inspect <target-dir> <game-exe>
fgm-next dlss5 install <target-dir> <game-exe>
fgm-next dlss5 restore <target-dir> [game-exe]
fgm-next dlss5 uninstall <target-dir> [game-exe]
```

## 真机验证（2026-10-29）

用 VTube Studio 的临时副本（UnityPlayer 提供 DX12 证据）：

- `inspect`：`NotDeployed`，`Direct3D12`，`reShadePresent=false`。
- `install`：114 个文件写入，ReShade 安装器输出 `Successfully installed ReShade.`，`reShadePresent=true`。
- `restore`：移除 114 个 pack 文件，`conflicts=0`；ReShade 模块/INI/日志按预期保留。
- `uninstall` 夹具：识别旧版 RenoDX 并阻止安装；卸载删除 ReShade 模块/INI/日志、GITC 组件、shader、旧版 addon、运行时残渣，剩余 0。

## 已知限制

- 官方 ReShade setup 每次都会联网下载兼容性数据，耗时约 60–100 秒；常规安装不经过它，只有 Vulkan 或离线解包失败时使用。
- Vulkan 依赖全局 ReShade 层，只能联网运行官方 setup。
- 官方 setup 的界面与退出码受 ReShade 上游控制；超时按失败处理并回滚。
