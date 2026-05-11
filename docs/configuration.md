# 設定檔說明

使用者設定存放於 `%LocalAppData%\SchemaExporter\appsettings.json`。首次啟動時，應用程式會從執行檔目錄的 `appsettings.json` 範本複製一份到使用者設定目錄。

設定結構如下：

```json
{
  "Schema": {
    "ExportPath": "...",
    "Connections": [ ... ],
    "LastSelectedConnectionName": "...",
    "ExportProfiles": [ ... ],
    "LastSelectedProfileName": "...",
    "ExportResultOptions": { ... }
  }
}
```

---

## Schema.ExportPath

匯出 Excel 的預設輸出資料夾。必須使用絕對路徑。若資料夾不存在，執行時會自動建立。

CLI 的 `--output` 參數可於執行時覆蓋此設定。

---

## Schema.Connections

資料庫連線清單。每筆連線設定包含以下欄位：

| 欄位 | 型別 | 必填 | 說明 |
| --- | --- | --- | --- |
| `Name` | string | 是 | 連線的顯示名稱，用於 WPF 下拉選單與 CLI `--connection` 參數。 |
| `DatabaseType` | string | 否 | 資料庫類型，可填 `SqlServer` 或 `Oracle`，預設為 `SqlServer`。 |
| `ConnectionString` | string | 是 | 標準 ADO.NET 連接字串。 |
| `ExportProfileName` | string | 否 | 此連線預設套用的匯出設定檔名稱。若未填或找不到對應設定檔，會套用清單中第一筆設定檔。 |

範例：

```json
{
  "Name": "正式環境",
  "DatabaseType": "SqlServer",
  "ConnectionString": "Server=prod-db;Database=MyApp;...",
  "ExportProfileName": "Default"
}
```

---

## Schema.ExportProfiles

篩選條件設定檔清單。每筆設定檔包含以下欄位：

| 欄位 | 型別 | 說明 |
| --- | --- | --- |
| `Name` | string | 設定檔名稱。 |
| `IncludeSchemas` | string[] | 要納入的結構描述名稱模式。空白表示全部納入。 |
| `ExcludeSchemas` | string[] | 要排除的結構描述名稱模式。排除規則在納入規則之後生效。 |
| `IncludeObjects` | string[] | 要納入的物件名稱模式。空白表示全部納入。 |
| `ExcludeObjects` | string[] | 要排除的物件名稱模式。 |
| `IncludeViews` | bool | 是否在資料表之外額外納入檢視表，預設為 `true`。 |

### 萬用字元

名稱模式支援萬用字元：

- `*`：代表任意數量的字元（含零個）。
- `?`：代表單一字元。

範例：`*_bak` 會排除所有以 `_bak` 結尾的物件。

### 篩選順序

1. 套用 `IncludeSchemas`（空白 = 全部通過）。
2. 套用 `ExcludeSchemas`，從已通過的結果中移除。
3. 對剩餘的結構描述套用 `IncludeObjects`（空白 = 全部通過）。
4. 套用 `ExcludeObjects`，從已通過的結果中移除。
5. 若 `IncludeViews` 為 `false`，移除所有檢視表。

---

## Schema.LastSelectedConnectionName

WPF 主畫面上次儲存的連線名稱。此欄位由「儲存設定」按鈕寫入，用於下次啟動時還原選取狀態。

若欄位為空白、缺少或找不到對應連線，WPF 會改用第一筆連線。

---

## Schema.LastSelectedProfileName

WPF 主畫面上次儲存的匯出設定檔名稱。此欄位由「儲存設定」按鈕寫入，用於下次啟動時還原選取狀態。

若欄位為空白、缺少或找不到對應設定檔，WPF 會依連線的 `ExportProfileName` 或第一筆設定檔決定預設值。

---

## Schema.ExportResultOptions

控制輸出檔案命名與附加產物的預設行為。所有選項均可在 CLI 執行時透過參數覆蓋。

| 欄位 | 型別 | 預設值 | 說明 |
| --- | --- | --- | --- |
| `UseTimestamp` | bool | `false` | 是否在檔名中附加時間戳記，格式由 `TimestampFormat` 決定。 |
| `TimestampFormat` | string | `"yyyyMMdd_HHmmss"` | 時間戳記格式，遵循 .NET 日期格式字串。 |
| `OverwriteStrategy` | string | `"Overwrite"` | 檔案已存在時的處理策略，詳見下方說明。 |
| `OpenOutputFolder` | bool | `false` | 匯出完成後是否自動以檔案總管開啟輸出資料夾。 |
| `GenerateManifest` | bool | `false` | 是否產生 manifest 檔案，紀錄此次匯出的後設資料。 |
| `GenerateJsonSidecar` | bool | `false` | 是否產生 JSON sidecar 檔案，包含完整 schema 結構與選用差異比對資料。 |
| `GenerateMarkdownSidecar` | bool | `false` | 是否產生 Markdown sidecar 檔案，以可讀格式呈現 schema 結構與差異摘要。 |
| `GenerateAiContext` | bool | `false` | 是否產生供 AI Agent 讀取的 schema context Markdown 檔案。 |
| `GenerateSchemaSnapshot` | bool | `false` | 是否產生可重複使用的 schema snapshot JSON 檔案，供後續差異比對使用。 |
| `DiffSourceSnapshotPath` | string | 無 | 差異比對的基準 snapshot 絕對路徑。CLI 會使用此設定；WPF 主畫面每次啟動會清空此欄位，不會透過「儲存設定」持久化。 |

### OverwriteStrategy 可用值

| 值 | 說明 |
| --- | --- |
| `Overwrite` | 直接覆寫已存在的檔案。 |
| `AppendSuffix` | 若檔案已存在，自動在檔名後附加序號（如 `_1`、`_2`）。 |
| `Fail` | 若檔案已存在，中止匯出並回報錯誤。 |
