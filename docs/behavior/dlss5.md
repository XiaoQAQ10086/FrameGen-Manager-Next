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

- 部署目标来自界面的 EXE 选择：默认用扫描结果，也可以“手动选择游戏实际启动 EXE”。手动选择会同时更新该游戏的部署目标目录（与帧生成页一致），安装文件与 ReShade 都写到该 EXE 所在目录。
- 图形 API 由扫描阶段自动判定，详情页把它作为默认选项并在后面标注“（自动）”；不再提供单独的“自动”行。用户改成其他 API 时按所选 API 安装。
- Unity 游戏按 Unity 的 Windows 默认值判定为 D3D11；只有目录里带 `D3D12Core.dll`（D3D12 构建）或 `vulkan-1.dll`（Vulkan 构建）时才判为对应 API。ReShade 自己的代理 DLL（版本信息 ProductName=ReShade）不参与判定，否则装过 ReShade 后重新扫描会把 API 判错。

1. 校验目标目录、pack schema、安装器 SHA-256。
2. 通过统一部署引擎部署 pack 中的 114 个文件：
   - `gitc-uplift.addon64`、`gitc-uplift.addon32`、`gitc-uplift-helper64.exe`
   - `GITC-Uplift/nvngx_dlssnr.dll`（534 MB 模型）
   - `reshade-shaders/Shaders/...`、`reshade-shaders/Textures/...`
   - `Uplift.fx`、`UpliftMask.fx`
3. 离线安装 ReShade（不联网、不运行 setup）：
   - 官方 setup 会把同一份 `ReShade64.dll` 写到游戏目录，但运行它会先联网下载兼容性信息；直接解出内嵌 DLL 得到的结果相同，且没有等待。
   - 在 `ReShade_Setup_6.8.0_Addon.exe` 里按 512 字节步进查找内嵌 ZIP，取出 `ReShade64.dll`（32 位游戏取 `ReShade32.dll`）。
   - 按 API 写成代理文件名：`d3d9/d3d10/d3d11/d3d12/opengl32.dll`；目标位置已有非 ReShade 文件时拒绝覆盖并回滚（`Conflict`）。
   - 目标没有 `ReShade.ini` 时写入默认搜索路径；随后由配置服务合并 `.\reshade-shaders\Shaders\**` / `Textures\**`。
   - Vulkan 需要全局层，无法离线落地：选择 Vulkan 时改为运行官方 ReShade 安装器（在线，超时 240 秒），其余 API 仍走离线路径。
   - 标准 ReShade shader/纹理和 GITC 指定的 fx 都在 pack 的 114 个文件里，随部署一起写入。
4. 失败时回滚已部署文件。
5. 界面在已部署（`Installed`）时锁定安装按钮；只有 `Outdated`/`Partial`/`Conflict` 允许重新安装或修复。详情页提供“打开文件夹”。
6. 安装/卸载期间详情页显示实时进度条：文件写入按数量推进，ReShade 模块阶段显示不确定进度。

## 状态检查

- pack 文件状态来自部署引擎（未部署/已部署/需更新/不完整/冲突）。
- 通过文件版本信息识别 ReShade 模块（`d3d9/d3d10/d3d11/d3d12/dxgi/opengl32.dll`，ProductName/FileDescription 含 `ReShade`），避免误删游戏自带 DLL。
- 同时报告旧版 RenoDX 残留（根目录 `renodx-dlss5*.addon64` 或旧布局根目录 `nvngx_dlssnr.dll`）与插件运行时残渣。
- 检测到旧版 RenoDX 时拒绝安装 GITC pack，返回 `LegacyInstallPresent`，必须先执行卸载。

## 还原

- 通过部署引擎还原 114 个 pack 文件，恢复部署前备份。
- **不自动运行 ReShade 卸载器**；ReShade 模块、`ReShade.ini`、`ReShade.log`、`ReShadePreset.ini` 会保留，并明确提示。
- 需要彻底清理时使用“卸载”。

## 彻底卸载

界面只有在检测到已部署（含旧版 RenoDX/残留）时才允许点“卸载”；未部署时按钮锁定。

1. 先调用部署引擎还原并清理 manifest/备份：只删除仍匹配部署哈希的文件，用户改过的文件保留。
2. 先尝试 ReShade 官方卸载器（`--state uninstall`），最多等 30 秒；超时或失败不报错，继续直接删除。直接删除是最终保证，不依赖任何网络。
3. 删除 ReShade 模块、`ReShade.ini`、`ReShade.log`、`ReShadePreset.ini`。
4. 删除 GITC 组件（`gitc-uplift.addon32/64`、`gitc-uplift-helper64.exe`、`GITC-Uplift/`）与已知 shader。
5. 清理旧版 RenoDX：`renodx-dlss5*.addon*`、旧布局根目录 `nvngx_dlssnr.dll`。
6. 清理插件运行时残渣（`renodx-dlss5-report*`、`ReShade.ini.renodx-dlss5*`、崩溃日志、trace）。
7. 仅当 `D3D12\` 只有 `D3D12Core.dll` 且版本信息含 Microsoft 时删除该 Agility SDK 文件。
8. 删除后重新扫描，返回已删除项与仍被占用的项；删除是永久的，不进回收站。

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

- `ReShade.ini` 搜索路径目前是覆盖写入，不保留用户自定义路径；后续改成合并。
- 安装器下载“compatibility information”依赖网络；离线场景需要预先准备。
