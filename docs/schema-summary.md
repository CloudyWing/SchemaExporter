# Schema Summary 格式

## 說明

Schema Summary 是精簡的 AI context Markdown，內容聚焦 schema metadata、物件清單、欄位、索引、外鍵關聯、routine signature、診斷資訊與差異摘要。

entry 與分檔明細檔案都不包含資料列內容、範例資料值或 routine 定義本文。Routine 只保留 signature 與 description。

## 與 Markdown sidecar 的差異

Markdown sidecar 是完整的可讀匯出，適合人工查閱完整 schema 結構。Schema Summary 是精簡摘要，適合放進 Agent、CI 或審查流程作為上下文。

完整 artifact 命名與定位參閱 [artifacts.md](artifacts.md)。

## 啟用方式

在 `appsettings.json` 設定：

```json
"ExportResultOptions": {
  "GenerateSchemaSummary": true
}
```

或 CLI 傳入 `--schema-summary` 參數。

## 檔案命名

與主要 Excel 輸出檔案同名，後綴為 `.schema-summary.md`。例如：

```text
TableSchema_prod_20241201_120000.xlsx
TableSchema_prod_20241201_120000.schema-summary.md
```

## 內容區塊

| 區塊 | 說明 |
| --- | --- |
| `Scope` | 匯出來源、資料庫類型、匯出設定檔、匯出時間與內容限制。 |
| `Counts` | 物件、欄位、索引、外鍵關聯與 routines 數量。 |
| `Diagnostics` | 匯出時收集到的資訊、警告與錯誤。 |
| `Provider Capabilities` | 目前資料庫 provider 的 schema metadata 支援範圍。 |
| `Diff Summary` | 指定基準 snapshot 時產生的變更摘要。 |
| `Object Inventory` | 物件與 routine 層級總覽，包含類型、欄位數、索引數、說明與明細連結（分檔模式）。 |
| `Relationships` | 外鍵約束、來源資料表與欄位、參考資料表與欄位。沒有關聯時輸出固定空集合文字；FK catalog 權限不足時輸出 unavailable 文字。 |
| `Objects` | 每個資料表或檢視表的欄位與索引 metadata。 |
| `Routines` | 預存程序、函數與 package routine 的 signature 摘要。 |

## 明細分檔

Summary 以 `Objects` 與 `Routines` 的總筆數作為判斷基準。

- 總筆數不超過 50 筆時，entry 內含物件明細與 routine 的 signature、description。
- 總筆數超過 50 筆時，entry 保留完整 inventory、完整 `Relationships` 與明細連結。每個物件或 routine 另寫入 `<entry-stem>.objects/` 下的獨立 Markdown 檔案。
- 明細檔案名稱包含穩定序號、類型、schema 與名稱，名稱字元限制為英數字、底線、連字號與句點。連結使用 `/` 作為分隔符號，並以 URI 編碼各 path segment。
- 產生分檔時會先完成所有明細檔，再寫入 entry。切回單檔模式時只清理該 entry 參照的明細檔案，保留其他檔案。
- 完整外鍵關聯清單固定保留在 entry，物件明細檔另列出該物件作為來源或參考端的關聯。

## 適用情境

- 將資料庫結構提供給 Agent 作為需求分析或程式碼審查背景。
- 在 CI 中保存可讀的 schema 摘要，供後續流程比對與查閱。
- 與 Schema Snapshot 搭配使用，保留機器可讀 JSON 與人類可讀 Markdown 兩種輸出。
