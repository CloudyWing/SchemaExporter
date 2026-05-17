# Manifest 檔案格式

## 說明

Manifest 是每次匯出完成後產生的 JSON 紀錄檔，記錄本次匯出的後設資料：連線資訊、使用的設定、統計數量、以及任何診斷訊息。

主要用途是事後追溯：當你看到一份 Excel 檔案，對應的 manifest 可以告訴你它是什麼時候、用什麼設定、從哪個資料庫產出的。

## 啟用方式

在 `appsettings.json` 設定：

```json
"ExportResultOptions": {
  "GenerateManifest": true
}
```

或 CLI 傳入 `--manifest` 參數。

## 檔案命名

與主要 Excel 輸出檔案同名，後綴為 `.manifest.json`。例如：

```text
TableSchema_prod_20241201_120000.xlsx
TableSchema_prod_20241201_120000.manifest.json
```

## JSON 結構

```json
{
  "exportedAt": "2024-12-01T12:00:00+08:00",
  "connectionName": "prod",
  "databaseType": "SqlServer",
  "profileName": "Default",
  "outputFilePath": "D:\\SchemaExports\\TableSchema_prod_20241201_120000.xlsx",
  "resultOptions": { ... },
  "counts": { ... },
  "diagnostics": [ ... ]
}
```

## 欄位說明

### 根層級

| 欄位 | 說明 |
| --- | --- |
| `exportedAt` | 匯出完成的時間，ISO 8601 格式含時區偏移。 |
| `connectionName` | 本次使用的連線名稱。 |
| `databaseType` | 資料庫類型（`SqlServer` 或 `Oracle`）。 |
| `profileName` | 本次使用的匯出設定檔名稱。 |
| `outputFilePath` | 主要 Excel 輸出檔案的完整路徑。 |

### resultOptions

記錄本次匯出實際套用的結果選項：

| 欄位 | 說明 |
| --- | --- |
| `useTimestamp` | 是否使用了時間戳記。 |
| `timestampFormat` | 使用的時間戳記格式字串。 |
| `overwriteStrategy` | 檔案衝突處理策略。 |
| `openOutputFolder` | 是否在完成後開啟輸出資料夾。 |
| `generateManifest` | 是否產生了 manifest。 |
| `generateJsonSidecar` | 是否產生了 JSON sidecar。 |
| `generateMarkdownSidecar` | 是否產生了 Markdown sidecar。 |
| `generateSchemaSummary` | 是否產生了 Schema Summary。 |
| `generateSchemaSnapshot` | 是否產生了 schema snapshot。 |
| `diffSourceSnapshotPath` | 差異比對使用的基準 snapshot 路徑（若有）。 |

### counts

本次匯出的統計數量：

| 欄位 | 說明 |
| --- | --- |
| `objects` | 匯出的資料表與檢視表總數。 |
| `columns` | 匯出的欄位總數。 |
| `indexes` | 匯出的索引總數。 |
| `routines` | 匯出的預存程序與函數總數。 |

### diagnostics

本次匯出產生的診斷項目陣列，每筆包含：

| 欄位 | 說明 |
| --- | --- |
| `severity` | 嚴重性：`Info`、`Warning` 或 `Error`。 |
| `category` | 診斷類別。詳見 [diagnostics.md](diagnostics.md)。 |
| `supportLevel` | 支援層級（選用）：`Full`、`Partial` 或 `Unsupported`。 |
| `affectedObject` | 受影響的物件名稱（選用）。 |
| `message` | 診斷描述訊息。 |
