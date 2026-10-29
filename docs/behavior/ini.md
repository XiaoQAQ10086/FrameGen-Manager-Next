# INI 编辑行为规格

来源：旧版 `crates/core/src/update.rs::prepare_deploy_ini` 以及实际生成的 `dlssg_sm86.ini`。

新实现：

- `Domain/Ini/IniDocument.cs`：保留行结构的解析/编辑。
- `Domain/Ini/IniMerger.cs`：按 stock 值差异应用覆盖。

## 解析规则

| 编号 | 条件 | 行为 | 测试 |
|---|---|---|---|
| INI-001 | 文件开头有 UTF-8 BOM | 去掉 BOM | `Parse...`（后续补充） |
| INI-002 | 行首（忽略空白）是 `;` 或 `#` | 整行按注释保留 | `ParsePreservesCommentsAndReadsValuesCaseInsensitively` |
| INI-003 | `[name]` | 解析为 section，名称去首尾空白 | 同上 |
| INI-004 | `key=value` | key/value 去首尾空白 | 同上 |
| INI-005 | 无法解析的行 | 原样保留，不报错 | `MalformedLinesAndInlineCommentsArePreserved` |
| INI-006 | section/key 大小写 | 查找和写入不区分大小写；保留原始 key 的大小写 | `SetValueUpdatesExistingKeyInPlaceAndKeepsKeyCasing` |
| INI-007 | 重复 key | `GetValue` 返回最后一个；`SetValue` 更新最后一个 | `DuplicateKeysReturnLastValue` |
| INI-008 | 值里有 `;`/`#` | 不当作行内注释，值原样保留 | `MalformedLinesAndInlineCommentsArePreserved` |
| INI-009 | `\r\n`、`\n`、`\r` | 统一解析；输出用调用方指定的换行 | `ToTextUsesRequestedLineEndingsAndTrailingNewline` |
| INI-010 | 文件末尾换行 | 解析时忽略多余末尾空行；输出保证有末尾换行 | 同上 |
| INI-011 | 没有 section 的 key | 归入空 section，可读写 | `SetValueSupportsSectionlessKeys` |

## 写入规则

| 编号 | 条件 | 行为 | 测试 |
|---|---|---|---|
| INI-020 | key 已存在 | 原地修改值，保留 key 大小写和位置 | `SetValueUpdatesExistingKeyInPlaceAndKeepsKeyCasing` |
| INI-021 | section 存在、key 不存在 | 插入到该 section 的最后一个 key 之后、下一个 section 之前 | `SetValueAddsMissingKeyBeforeNextSection` |
| INI-022 | section 不存在 | 在文件末尾追加空行 + section + key | `SetValueAddsMissingSection` |
| INI-023 | 删除 key | 删除该 section 下所有同名 key | `RemoveValueRemovesAllOccurrences` |

## 覆盖规则（与 stock 模板比较）

| 编号 | 条件 | 行为 | 测试 |
|---|---|---|---|
| INI-030 | 覆盖值等于 stock 值，且模板里没有该 key | 不写入 | `ApplyOverridesSkipsStockValueWhenKeyIsMissing` |
| INI-031 | 覆盖值等于 stock 值，但模板里已有不同值 | 写回 stock 值 | `ApplyOverridesRestoresStockValueWhenTemplateDiffers` |
| INI-032 | 覆盖值不同于 stock 值 | 写入覆盖值 | `ApplyOverridesAppliesDifferentValue` |
| INI-033 | stock 的 section/key 大小写不同 | 仍视为同一项 | `ApplyOverridesMatchesStockKeysCaseInsensitively` |

## 为什么这样设计

- 旧版在“只有用户选项不同于 stock 时才写 key”上依赖隐式行为；新实现把 stock 值显式写进 pack。
- 保留注释和未知行，避免上游模板升级时把用户/上游的注释冲掉。
- 值不解析行内注释，避免路径里的 `;` 被截断。

## 已知限制

- 不支持转义序列、引号语义、多行值。
- 重复 section 会保留为独立块；写入定位到最后一个同名 section。
- 编码由调用方统一为 UTF-8；INI 文件本身不声明编码。
