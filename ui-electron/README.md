# FrameGen Manager-Next · Electron UI

Electron + 静态 HTML/CSS/JS 的桌面界面，Rust 核心通过 napi-rs 嵌入主进程。

## 开发

```powershell
cd ui-electron
npm install
npm run build:native   # 编译 rust/crates/fgm-node 并复制为 native/fgm_node.node
$env:FGM_DATA_DIR = "$env:LOCALAPPDATA\FrameGen-Manager-Next"
npm start
```

`FGM_DATA_DIR` 未设置时使用 `ui-electron/data`（便携式测试目录）；`FGM_PACKS_DIR` 未设置时使用仓库 `packs/`。

## 结构

- `main.js`：单窗口；`native.invoke(name, payload)` 异步调用 Rust；`cover` 结果转换为 `file://` URL。
- `preload.js`：`window.fgm.call(name, payload)` 与 `openUrl`。
- `renderer/`：`index.html`、`app.js`、`styles.css`（旧 WebAssets 基线）、`motion.css`（动效）。
- `native/fgm_node.node`：由 Rust `fgm-node` crate 构建，不提交。

## 命令

| 名称 | 说明 |
|---|---|
| `version` | 产品版本 |
| `library.list` / `library.scan` | 库缓存 / 扫描 |
| `gameModes` | 每局部署状态 |
| `cover` | 封面路径（返回对象含 `url`） |
| `gpu` / `gpuSpoof.*` | 显卡与顶号 |
| `deploy` / `restore` | 帧生成部署与还原 |
| `sm.*` / `dlss5.*` / `anr.*` | 其余资产族 |
| `openFolder` / `openPath` / `openHags` | 系统动作 |
| `logs.*` / `diagnostics.export` | 日志与诊断 |
| `update.*` | 更新 |
| `operation.progress` / `operation.cancel` | 长操作进度/取消 |

参数与返回值是 Rust FFI 的 JSON 信封（`{"ok":true,...}` / `{"ok":false,"code":...}`）。
