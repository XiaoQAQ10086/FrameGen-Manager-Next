# ADR-0003：新建独立 GitHub 仓库

- 状态：已接受
- 日期：2026-10-29

## 背景

旧项目位于 `XiaoQAQ10086/FrameGen-Manager`，已有用户、Releases 和自动更新源。旧版更新检查会读取：

```
raw.githubusercontent.com/XiaoQAQ10086/FrameGen-Manager/main/Cargo.toml
```

如果直接覆盖旧仓库 `main`，旧用户的更新检查会立即失效或出现异常。

## 决策

- 新建独立仓库 `FrameGen-Next`，独立历史、Issues、Releases 和 Velopack 更新源。
- 旧仓库保持原样，不覆盖 `main`，不改旧 Releases。
- 新版本从 `2.0.0` 开始，`packId = FrameGenNext`，与旧版并存安装。
- 旧仓库在 Next 正式发布后加迁移公告，再 Archive；旧 Releases 永久保留。

## 后果

- 新仓库不继承旧仓库的 star 和 issue 历史（可后续手动迁移需要的 issue）。
- 两个产品可以并行存在，风险隔离。
- 旧版自动更新不被破坏，用户可以先还原旧部署再切换。
