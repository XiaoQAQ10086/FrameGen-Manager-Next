# 问题反馈与日志上传

入口：顶部“日志与反馈”页面。

## 表单

- 类型：问题反馈（`issue`）/ 建议反馈（`suggestion`），默认问题。
- 联系方式：QQ / 邮箱，可选。
- 详细说明：必填，最多 5000 字符。
- 隐私勾选项默认关闭；未勾选不能提交。提示日志可能包含文件路径、用户名等信息。
- 提交前打包最近日志（`logs/fgm.log` 尾部，最多 1.5 MB）+ `summary.json` 为 zip；超过 2 MB 时拒绝上传。

## 上传

- Electron 主进程读取 `feedback.local.json` 或 `FGM_FEEDBACK_KEY`；上传密钥不进入渲染进程，不写入日志。
- `POST https://laobiao-feedback.pages.dev/api/feedback`，multipart 字段：`description`、`type`、`app`、`version`、`contact`、`meta`、`log`。
- 超时 90 秒；5xx/网络错误最多重试 3 次（2 秒、5 秒间隔）；401 提示客户端版本过旧。
- 成功后显示反馈编号并支持复制；按钮禁用防止重复提交；失败保留重试提示。

## 我的反馈

- 本地 `state/feedback-history.json` 最多保留 30 条，字段含 `id`、查询 `key`、时间、摘要、类型、状态、回复；`key` 只留在主进程文件中，不展示。
- 打开“我的反馈”时用批量接口刷新状态与站长回复；离线时显示本地快照。
- 状态文案直接使用服务端 `statusText`；无回复显示“站长还没有回复，处理完成后会显示在这里。”
- “删除本地记录”只删本地，不影响服务器。
