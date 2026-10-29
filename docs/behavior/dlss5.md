# DLSS5 / ReShade 行为规格

来源：旧版 `dlss5.rs`、`ui/dlss5.js` 与真机安装器验证。
新实现：`Core/Features/Dlss5`、`Infrastructure/Dlss5`、`Infrastructure/Processes`、`dlss5.reshade-gitc` pack。

## 前置条件

- 从游戏渲染 EXE 探测图形 API；映射到 ReShade 参数：
  - DX9 → `d3d9`；DX10 → `d3d10`；DX11 → `d3d11`；DX12 → `d3d12`；Vulkan → `vulkan`；OpenGL → `opengl`。
- API 无法识别时拒绝安装。
- GPU 不是 SM75/SM86 时由 pack 的 `gpuRoutes` 在后续 UI 层提示；当前 CLI 不做 GPU 闸门。

## 安装流程

1. 校验目标目录、pack schema、安装器 SHA-256。
2. 通过统一部署引擎部署 pack 中的 114 个文件：
   - `gitc-uplift.addon64`、`gitc-uplift.addon32`、`gitc-uplift-helper64.exe`
   - `GITC-Uplift/nvngx_dlssnr.dll`（534 MB 模型）
   - `reshade-shaders/Shaders/...`、`reshade-shaders/Textures/...`
   - `Uplift.fx`、`UpliftMask.fx`
3. 运行 ReShade 安装器：
   ```
   ReShade_Setup_6.8.0_Addon.exe --headless --api <api> "<game-exe>"
   ```
   - 超时 240 秒；`CreateNoWindow`；捕获 stdout/stderr。
   - 非 0 退出或超时 → 回滚已部署文件，返回 `InstallerFailed`。
4. 如果生成 `ReShade.ini`，写入：
   - `EffectSearchPaths=.\reshade-shaders\Shaders\**`
   - `TextureSearchPaths=.\reshade-shaders\Textures\**`
5. 返回安装器输出和部署报告。

## 状态检查

- pack 文件状态来自部署引擎（未部署/已部署/需更新/不完整/冲突）。
- `ReShade.ini` 存在 → `reShadePresent=true`。
- 同时显示探测到的 API。

## 还原

- 通过部署引擎还原 114 个 pack 文件，恢复部署前备份。
- **不自动运行 ReShade 卸载器**；ReShade 模块、`ReShade.ini`、`ReShade.log`、`ReShadePreset.ini` 会保留，并明确提示。
- 真实游戏卸载/清理留到后续“卸载 DLSS5”功能。

## 命令

```powershell
fgm-next dlss5 inspect <target-dir> <game-exe>
fgm-next dlss5 install <target-dir> <game-exe>
fgm-next dlss5 restore <target-dir> <game-exe>
```

## 真机验证（2026-10-29）

用 VTube Studio 的临时副本（UnityPlayer 提供 DX12 证据）：

- `inspect`：`NotDeployed`，`Direct3D12`，`reShadePresent=false`。
- `install`：114 个文件写入，ReShade 安装器输出 `Successfully installed ReShade.`，`reShadePresent=true`。
- `restore`：移除 114 个 pack 文件，`conflicts=0`；ReShade 模块/INI/日志按预期保留。

## 已知限制

- 未接入 GPU 路由闸门（UI 层后续加）。
- 不支持旧版 RenoDX 清理/官方 ReShade 卸载。
- `ReShade.ini` 搜索路径目前是覆盖写入，不保留用户自定义路径；后续改成合并。
- 安装器下载“compatibility information”依赖网络；离线场景需要预先准备。
