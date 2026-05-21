# 輸出產物定位

## 說明

SchemaExporter 的主要輸出是 Excel 活頁簿，其餘檔案稱為 artifacts。Artifacts 用於追溯匯出設定、保存可比對的 schema 狀態、提供人工審查資料，或提供 Agent 與 CI 使用的精簡上下文。

所有匯出時自動產生的 artifacts 會沿用主要 Excel 檔案的基本檔名，再加上固定後綴。

若啟用 `Schema.Redaction`，敏感 metadata 會在 artifacts 產生前被遮罩，因此 Excel、snapshot、JSON sidecar、Markdown sidecar 與 Schema Summary 會使用一致的遮罩結果。

## Artifact 類型

| 名稱 | 檔名後綴 | 主要讀者 | 用途 |
| --- | --- | --- | --- |
| Excel 活頁簿 | `.xlsx` | 人 | 完整 schema 匯出結果，適合人工檢視與保存。 |
| Manifest | `.manifest.json` | 人、自動化流程 | 記錄本次匯出的連線、設定、統計數量與 diagnostics。 |
| JSON sidecar | `.schema.json` | 自動化流程 | 保存本次 snapshot 與選用 diff 的 JSON 包裝。 |
| Markdown sidecar | `.schema.md` | 人 | 以可讀 Markdown 呈現完整 schema 結構與選用 diff 摘要。 |
| Schema Summary | `.schema-summary.md` | Agent、CI、人 | 精簡 AI context Markdown，聚焦 schema metadata、限制資訊與變更摘要。 |
| Schema Snapshot | `.snapshot.json` | 自動化流程 | 保存可重複比對的 schema 狀態，是 diff 命令的輸入來源。 |
| Schema Diff | `.diff.json` | 自動化流程 | 保存兩份 snapshot 的機器可讀差異結果。 |

## 命名準則

- `sidecar` 表示附屬於同一份 Excel 匯出的可讀或結構化補充檔案。
- `snapshot` 與 `diff` 是可重複使用的資料契約，適合納入版本治理與 CI 流程。
- `Schema Summary` 是目前的 AI context Markdown 輸出；既有設定檔中的 `GenerateAiContext` 會在讀取時轉為 `GenerateSchemaSummary`。
- `Markdown sidecar` 保留給完整人工閱讀輸出，不承擔 AI context 的精簡責任。

## 產生方式

匯出命令會依 `ExportResultOptions` 或 CLI 參數產生 artifacts。`schemaexporter diff` 命令也可以不連線資料庫，直接用兩份 snapshot 單獨產生 JSON 或 Markdown diff 報告。
