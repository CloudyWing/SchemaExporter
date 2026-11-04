# 設定檔說明

使用者設定存放於 `%LocalAppData%\SchemaExporter\appsettings.json`，Desktop 與 CLI 共用同一份設定檔。首次啟動 Desktop 或執行 CLI `export` 時，應用程式會從執行檔目錄的 `appsettings.json` 範本複製一份到使用者設定目錄。CLI 的 `diff`、`comment-sql`、`--help` 與引數錯誤不會讀取或建立使用者設定檔。

設定結構如下：

```json
{
  "Schema": {
    "ExportPath": "...",
    "Connections": [ ... ],
    "LastSelectedConnectionName": "...",
    "ExportProfiles": [ ... ],
    "LastSelectedProfileName": "...",
    "ExportResultOptions": { ... },
    "Redaction": { ... }
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
| `Name` | string | 是 | 連線的顯示名稱，用於 Desktop 下拉選單與 CLI `--connection` 參數。 |
| `DatabaseType` | string | 否 | 資料庫類型，可填 `SqlServer` 或 `Oracle`，預設為 `SqlServer`。 |
| `ConnectionString` | string | 是 | ADO.NET 連線字串。可用明文、環境變數參照或 DPAPI 加密三種格式保存，詳見下方「連線字串保存格式」。 |
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

### 連線字串保存格式

每筆連線可各自選擇保存方式，沒有前綴的值一律視為明文，既有設定檔不需修改。

| 格式 | 寫法 | 適合情境 |
| --- | --- | --- |
| 明文 | `Server=prod-db;Database=MyApp;User Id=app;Password=...` | 本機、測試資料庫 |
| 環境變數參照 | `${env:SCHEMAEXPORTER_PROD}` | CI、不想讓密碼寫入設定檔的環境 |
| DPAPI 加密 | `dpapi:AQAAANCMnd8B...` | 本機的正式環境連線 |

- 環境變數參照必須是整個值，例如 `${env:SCHEMAEXPORTER_PROD}`；變數名稱只能使用英文字母、數字與底線，且不可以數字開頭。不支援在連線字串中間內插。
- DPAPI 加密值由 Desktop 設定頁的「加密儲存」產生，不需手動編寫。加密範圍是目前的 Windows 使用者，只有同一台電腦、同一個使用者能解開；複製設定檔到其他電腦或使用者後，必須重新輸入連線字串。
- 解析在匯出開始時進行，設定檔中保存的仍是原始值，儲存設定時不會把解析後的連線字串寫回檔案。
- 環境變數未設定、加密值無法解開，或在非 Windows 平台遇到加密值時，匯出會以驗證錯誤停止（CLI 結束代碼 `2`），錯誤訊息只包含連線名稱與變數名稱，不包含連線字串。

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

範例：`Tmp*` 比對所有以 `Tmp` 開頭的名稱、`User?` 比對 `Users`、`UserA` 等剛好五個字元的名稱。

### 篩選順序

1. 套用 `IncludeSchemas`（空白 = 全部通過）。
2. 套用 `ExcludeSchemas`，從已通過的結果中移除。
3. 對剩餘的結構描述套用 `IncludeObjects`（空白 = 全部通過）。
4. 套用 `ExcludeObjects`，從已通過的結果中移除。
5. 若 `IncludeViews` 為 `false`，移除所有檢視表。

---

## Schema.LastSelectedConnectionName

Desktop 匯出頁上次儲存的連線名稱。此欄位由匯出頁的「將目前選項存為預設值」寫入，用於下次啟動時還原選取狀態。

若欄位為空白、缺少或找不到對應連線，Desktop 會改用第一筆連線。

---

## Schema.LastSelectedProfileName

Desktop 匯出頁上次儲存的匯出設定檔名稱。此欄位由匯出頁的「將目前選項存為預設值」寫入，用於下次啟動時還原選取狀態。

若欄位為空白、缺少或找不到對應設定檔，Desktop 會依連線的 `ExportProfileName` 或第一筆設定檔決定預設值。

---

## Schema.ExportResultOptions

控制輸出檔案命名與附加產物的預設行為。CLI 可在執行時透過參數覆蓋；Desktop 匯出頁的進階選項（含 `TimestampFormat` 與 `OverwriteStrategy`）只套用於當次匯出，按下「將目前選項存為預設值」才會寫回設定檔。

| 欄位 | 型別 | 預設值 | 說明 |
| --- | --- | --- | --- |
| `UseTimestamp` | bool | `false` | 是否在檔名中附加時間戳記，格式由 `TimestampFormat` 決定。 |
| `TimestampFormat` | string | `"yyyyMMdd_HHmmss"` | 時間戳記格式，遵循 .NET 日期格式字串。 |
| `OverwriteStrategy` | string | `"Overwrite"` | 檔案已存在時的處理策略，詳見下方說明。 |
| `OpenOutputFolder` | bool | `false` | 匯出完成後是否自動以檔案總管開啟輸出資料夾。僅 Desktop 使用；CLI 忽略此設定，只有指定 `--open-output-folder` 時才開啟。 |
| `GenerateManifest` | bool | `false` | 是否產生 manifest 檔案，紀錄此次匯出的後設資料。 |
| `GenerateJsonSidecar` | bool | `false` | 是否產生 JSON sidecar 檔案，包含 snapshot 與選用 diff 的結構化資料。 |
| `GenerateMarkdownSidecar` | bool | `false` | 是否產生 Markdown sidecar 檔案，以可讀格式呈現完整 schema 結構與差異摘要。 |
| `GenerateSchemaSummary` | bool | `false` | 是否產生精簡 Schema Summary，也就是 AI context Markdown 檔案。 |
| `GenerateSchemaSnapshot` | bool | `false` | 是否產生可重複使用的 schema snapshot JSON 檔案，供後續差異比對使用。 |
| `DiffSourceSnapshotPath` | string | 無 | 差異比對的基準 snapshot 絕對路徑。CLI 會使用此設定；Desktop 匯出頁載入設定時會清空畫面上的對應欄位，不沿用此值。「將目前選項存為預設值」保留設定檔原值，不會持久化畫面上選擇的路徑。 |

各 artifacts 的定位與檔名後綴參閱 [artifacts.md](artifacts.md)。

### Desktop 儲存範圍

Desktop 的兩個儲存入口各自只寫入負責的欄位，儲存前會重新讀取最新的設定檔再合併，不會覆蓋另一個入口的設定：

| 儲存入口 | 寫入欄位 |
| --- | --- |
| 匯出頁「將目前選項存為預設值」 | `ExportPath`、`ExportResultOptions`、`LastSelectedConnectionName`、`LastSelectedProfileName` |
| 設定頁「儲存」 | `Connections`、`ExportProfiles`、`Redaction` |

### 向下相容

既有設定檔若仍包含 `GenerateAiContext`，讀取時會自動轉為 `GenerateSchemaSummary`；下次儲存後會輸出新欄位名稱。

### OverwriteStrategy 可用值

| 值 | 說明 |
| --- | --- |
| `Overwrite` | 直接覆寫已存在的檔案。 |
| `AppendSuffix` | 若檔案已存在，自動在檔名後附加序號（如 `_1`、`_2`）。 |
| `Fail` | 若檔案已存在，中止匯出並回報錯誤。 |

---

## Schema.Redaction

控制敏感 metadata 遮罩規則，可在 Desktop 設定頁的 Redaction 分頁編輯。Redaction 預設停用；啟用後會在 Excel、snapshot、JSON sidecar、Markdown sidecar 與 Schema Summary 產生前套用。

Redaction 只處理描述、預設值、routine 簽章與 routine 定義等 metadata 內容，不改寫 schema、object、column、index 或 routine 名稱。

| 欄位 | 型別 | 預設值 | 說明 |
| --- | --- | --- | --- |
| `Enabled` | bool | `false` | 是否啟用 redaction。 |
| `ReplacementText` | string | `"[REDACTED]"` | 被遮罩內容的替代文字。 |
| `SensitiveNamePatterns` | string[] | 見下方 | 用來比對物件、欄位與 routine 名稱的 .NET regex。命中後會遮罩該項目的描述、預設值或 routine 定義等 metadata。 |
| `SensitiveTextPatterns` | string[] | `[]` | 用來比對 metadata 文字內容的 .NET regex。命中時只替換符合的文字片段。 |

`SensitiveNamePatterns` 的內建預設值為：

```json
[
  "password", "passwd", "pwd", "secret", "token",
  "api[_-]?key", "credential", "private[_-]?key",
  "connection[_-]?string", "salt"
]
```

可完整覆寫此清單。覆寫後，內建規則不會保留，需自行補回需要的項目。

範例（在預設規則外額外加入自訂 pattern）：

```json
"Redaction": {
  "Enabled": true,
  "ReplacementText": "[MASKED]",
  "SensitiveNamePatterns": [
    "password",
    "token",
    "api[_-]?key"
  ],
  "SensitiveTextPatterns": [
    "AKIA[0-9A-Z]+"
  ]
}
```
