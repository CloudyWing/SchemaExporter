# 輸出產物定位

## 說明

SchemaExporter 的主要輸出是 Excel 活頁簿，其餘檔案稱為 artifacts。Artifacts 用於追溯匯出設定、保存可比對的 schema 狀態、提供人工審查資料，或提供 Agent 與 CI 使用的精簡上下文。

所有匯出時自動產生的 artifacts 會沿用主要 Excel 檔案的基本檔名，再加上固定後綴。

若啟用 `Schema.Redaction`，敏感 metadata 會在 artifacts 產生前被遮罩，因此 Excel、snapshot、JSON sidecar、Markdown sidecar 與 Schema Summary 會使用一致的遮罩結果。外鍵關聯會以獨立的 Relationships 集合傳遞至各項適用成品。

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

Schema Summary 會固定輸出 Relationships 區塊。外鍵 catalog 權限不足時，Summary 仍可產生，並同時保留診斷訊息與固定的 unavailable 文字。

## 命名準則

- `sidecar` 表示附屬於同一份 Excel 匯出的可讀或結構化補充檔案。
- `snapshot` 與 `diff` 是可重複使用的資料契約，適合納入版本治理與 CI 流程。
- `Schema Summary` 是目前的 AI context Markdown 輸出；既有設定檔中的 `GenerateAiContext` 會在讀取時轉為 `GenerateSchemaSummary`。
- `Markdown sidecar` 保留給完整人工閱讀輸出，不承擔 AI context 的精簡責任。

## Schema Summary 分檔

Schema Summary 以物件與 routine 總筆數判斷是否分檔。50 筆以內使用單一 entry 檔案並內含明細；超過 50 筆時，entry 保留完整 inventory、Relationships 與相對連結，明細寫入 `<entry-stem>.objects/`。

分檔檔名包含穩定序號與安全化的 schema、名稱。entry 會在明細檔案完成後寫入。重新產生單檔模式時只清理該 entry 推導出的 `.objects` 目錄。

## 產生方式

匯出命令會依 `ExportResultOptions` 或 CLI 參數產生 artifacts。`schemaexporter diff` 命令也可以不連線資料庫，直接用兩份 snapshot 單獨產生 JSON 或 Markdown diff 報告。
