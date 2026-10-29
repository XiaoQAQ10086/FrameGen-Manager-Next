# ADR-0004：xUnit v3 + Microsoft.Testing.Platform

- 状态：已接受
- 日期：2026-10-29

## 背景

.NET 10 SDK 不再支持 Microsoft.Testing.Platform 测试项目走 VSTest。xUnit v3 默认是 MTP 应用（测试项目本身是可执行文件）。

## 决策

- 使用最新稳定版 `xunit.v3` + `Shouldly`。
- `global.json` 中显式启用新 `dotnet test` 体验：

```json
{
  "test": {
    "runner": "Microsoft.Testing.Platform"
  }
}
```

- 测试项目设置 `<OutputType>Exe</OutputType>` 和 `<IsTestProject>true</IsTestProject>`。
- CI 使用 `dotnet test FrameGenManagerNext.slnx -c Release --no-build`。

## 后果

- 删掉 `"test.runner"` 会导致 `dotnet test` 直接报错。
- 测试项目不能再依赖纯 VSTest 的扩展（如部分 coverlet 收集器场景）。
- 后续可以按项目启用 MTP 的 `--report-trx` 等能力。
