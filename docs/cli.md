# CLI 版操作說明

CLI 是獨立的 Console 執行檔 `CloudyWing.SchemaExporter.Cli.exe`，不依賴 WPF、WebView2 或 Desktop 程式，支援三個命令：`export`、`diff` 與 `comment-sql`。

```text
CloudyWing.SchemaExporter.Cli export --connection <name> [options]
CloudyWing.SchemaExporter.Cli diff --left <path> --right <path> [options]
CloudyWing.SchemaExporter.Cli comment-sql --snapshot <path> --comments <path> --output <path>
CloudyWing.SchemaExporter.Cli --help
```

## 取得執行檔

| 來源 | 位置 |
| --- | --- |
| Desktop 安裝版 | 安裝目錄的 `tools\CloudyWing.SchemaExporter.Cli.exe`。Velopack 預設安裝在 `%LocalAppData%\CloudyWing.SchemaExporter\current\`（推測，以實際安裝路徑為準）。 |
| Release 的 CLI ZIP | `CloudyWing.SchemaExporter.Cli-<版本>-win-x64.zip`，解壓縮即可執行，內含 `appsettings.json` 範本。 |
| 原始碼 | `dotnet run --project .\src\SchemaExporter.Cli\SchemaExporter.Cli.csproj -- <命令>` |

Agent Skill、排程或 CI 請以絕對路徑呼叫，不要依賴目前工作目錄。

> v1.0.0 起，舊版 `CloudyWing.SchemaExporter.exe export ...` 的 CLI 模式已移除。Desktop 執行檔帶任何引數啟動時會直接以結束代碼 `1` 結束，不會開啟視窗；請改用上述 CLI 執行檔，命令與參數意義不變。

## 設定檔

`export` 從 `%LocalAppData%\SchemaExporter\appsettings.json` 讀取使用者設定，與 Desktop 共用。若使用者設定檔尚不存在，會先從 CLI 執行檔同目錄的 `appsettings.json` 範本複製一份。除非以參數覆蓋，否則所有預設值均來自使用者設定檔。

`diff`、`comment-sql`、`--help` 與參數錯誤都是離線作業，不讀取也不建立使用者設定檔，設定檔缺漏或損毀時仍可執行。

## 輸出串流與取消

- 進度與正常結果寫入 stdout，錯誤訊息與 Error 等級的記錄寫入 stderr。
- 錯誤訊息與記錄會遮罩連線字串中的密碼。
- 執行中按 Ctrl+C 會取消作業並以結束代碼 `4` 結束；已寫出的檔案可能不完整，不應視為成功結果。再按一次 Ctrl+C 則由系統直接終止。

---

## export 命令

連線至資料庫並匯出 schema 為 Excel 檔案。

```bash
CloudyWing.SchemaExporter.Cli export --connection <name> [options]
```

### 必要參數

| 參數 | 說明 |
| --- | --- |
| `--connection <name>` | 要使用的連線名稱，須對應 `appsettings.json` 中 `Schema.Connections` 的某筆 `Name`。 |

### 選用參數

| 參數 | 說明 |
| --- | --- |
| `--profile <name>` | 覆蓋此次使用的匯出設定檔名稱。省略時使用連線設定的 `ExportProfileName`，若仍找不到則套用第一筆設定檔。 |
| `--output <path>` | 覆蓋輸出資料夾的絕對路徑。省略時使用 `appsettings.json` 的 `Schema.ExportPath`。 |
| `--manifest` | 強制啟用 manifest 產生，無論設定檔預設值為何。 |
| `--no-manifest` | 強制停用 manifest 產生。 |
| `--json-sidecar` | 強制啟用 JSON sidecar 產生。 |
| `--markdown-sidecar` | 強制啟用完整可讀 Markdown sidecar 產生。 |
| `--schema-summary` | 強制啟用精簡 Schema Summary，也就是 AI context Markdown 產生。 |
| `--snapshot` | 強制啟用 schema snapshot 產生。 |
| `--diff-from <path>` | 指定基準 snapshot 的絕對路徑，執行完成後自動產生差異比對結果。 |
| `--open-output-folder` | 匯出完成後開啟輸出資料夾。CLI 只依此旗標決定，不沿用設定檔的 `OpenOutputFolder`，避免無人值守流程彈出視窗。 |
| `--no-open-output-folder` | 不開啟輸出資料夾（預設）。 |
| `--no-timestamp` | 停用檔名時間戳記後綴。 |
| `--help` / `-h` / `/?` | 顯示說明文字。 |

### 輸出

執行過程中會即時輸出進度訊息至 stdout。完成後顯示所有產生的檔案路徑及診斷摘要。

Artifacts 的命名與用途參閱 [artifacts.md](artifacts.md)。

```text
[Validating] 正在驗證匯出參數...
[LoadingSchema] 正在讀取資料庫結構描述...
...

Export completed successfully.
Workbook: D:\SchemaExports\TableSchema_Key1_20241201_120000.xlsx
Manifest: D:\SchemaExports\TableSchema_Key1_20241201_120000.manifest.json
Schema summary: D:\SchemaExports\TableSchema_Key1_20241201_120000.schema-summary.md
Diagnostics: 2 total, 1 warning(s), 0 error(s).
警告:
- [檢視表支援] 檢視表目前僅匯出物件與欄位中繼資料，不包含定義 SQL、相依性與索引/主鍵明細。
```

### 結束代碼

| 代碼 | 名稱 | 說明 |
| --- | --- | --- |
| `0` | `Success` | 成功，或使用者要求顯示 help。 |
| `1` | `ArgumentError` | 參數解析失敗（顯示 usage 後結束），包含無法辨識的命令（`Unknown command: <name>`）；使用者設定檔無法讀取時也回傳 `1`（`Startup failed: ...`），與舊版一致。 |
| `2` | `WorkflowError` | 匯出流程失敗（如驗證錯誤、連線失敗、輸出失敗）。 |
| `3` | `UnexpectedError` | 未預期的錯誤。 |
| `4` | `Canceled` | 使用者以 Ctrl+C 取消。 |

### 範例

```bash
# 基本匯出
CloudyWing.SchemaExporter.Cli export --connection prod

# 覆蓋設定檔與輸出路徑
CloudyWing.SchemaExporter.Cli export --connection prod --profile TablesOnly --output C:\Reports

# 匯出並產生 snapshot 與 manifest
CloudyWing.SchemaExporter.Cli export --connection prod --snapshot --manifest

# 匯出並與基準 snapshot 產生差異比對
CloudyWing.SchemaExporter.Cli export --connection prod --snapshot --diff-from C:\Snapshots\baseline.snapshot.json

# 匯出精簡 Schema Summary
CloudyWing.SchemaExporter.Cli export --connection prod --schema-summary

# 停用時間戳記
CloudyWing.SchemaExporter.Cli export --connection prod --no-timestamp
```

---

## diff 命令

比對兩份 schema snapshot 檔案並輸出差異報告，不需要連線資料庫。

Snapshot 路徑可使用絕對路徑或相對路徑；相對路徑會依目前工作目錄解析。

```bash
CloudyWing.SchemaExporter.Cli diff --left <path> --right <path> [options]
```

### 必要參數

| 參數 | 說明 |
| --- | --- |
| `--left <path>` | 基準（舊版）snapshot 檔案的路徑。 |
| `--right <path>` | 目前（新版）snapshot 檔案的路徑。 |

### 選用參數

| 參數 | 說明 |
| --- | --- |
| `--output <path>` | 將差異報告寫入指定路徑。省略時直接輸出 Markdown 至 stdout。 |
| `--format <json 或 markdown>` | 指定輸出格式。省略時依副檔名自動判斷（`.md` 為 Markdown，其餘為 JSON）。 |
| `--help` / `-h` / `/?` | 顯示說明文字。 |

### 結束代碼

| 代碼 | 名稱 | 說明 |
| --- | --- | --- |
| `0` | `Success` | 成功，或使用者要求顯示 help。 |
| `1` | `ArgumentError` | 參數解析失敗。 |
| `2` | `WorkflowError` | 比對流程失敗（如找不到檔案、格式錯誤）。 |
| `3` | `UnexpectedError` | 未預期的錯誤。 |
| `4` | `Canceled` | 使用者以 Ctrl+C 取消。 |

### 範例

```bash
# 輸出 Markdown 至 console
CloudyWing.SchemaExporter.Cli diff --left baseline.snapshot.json --right current.snapshot.json

# 寫入 Markdown 檔案
CloudyWing.SchemaExporter.Cli diff --left baseline.snapshot.json --right current.snapshot.json --output diff.md

# 寫入 JSON 檔案
CloudyWing.SchemaExporter.Cli diff --left baseline.snapshot.json --right current.snapshot.json --output diff.json

# 明確指定格式
CloudyWing.SchemaExporter.Cli diff --left baseline.snapshot.json --right current.snapshot.json --output report.txt --format markdown
```

---

## comment-sql 命令

依 schema snapshot 與註解清單產生資料表與欄位註解的 SQL 檔，不連線資料庫，也不執行 SQL。

```bash
CloudyWing.SchemaExporter.Cli comment-sql --snapshot <path> --comments <path> --output <path>
```

### 必要參數

| 參數 | 說明 |
| --- | --- |
| `--snapshot <path>` | schema snapshot 檔案的路徑，提供名稱、資料庫類型與現有註解。 |
| `--comments <path>` | 註解清單 JSON 檔案的路徑，欄位為 `schema`、`object`、`column`、`comment`。 |
| `--output <path>` | 要寫入的 `.sql` 檔案路徑，既有檔案會被覆寫。 |

### 結束代碼

| 代碼 | 名稱 | 說明 |
| --- | --- | --- |
| `0` | `Success` | 成功，或使用者要求顯示 help。 |
| `1` | `ArgumentError` | 參數解析失敗。 |
| `2` | `WorkflowError` | 輸入檔無法讀取或格式錯誤，或清單有無法產生 SQL 的項目，不會寫出輸出檔。 |
| `3` | `UnexpectedError` | 未預期的錯誤。 |
| `4` | `Canceled` | 使用者以 Ctrl+C 取消。 |

### 範例

```bash
CloudyWing.SchemaExporter.Cli comment-sql --snapshot db.snapshot.json --comments comments.json --output comments.sql
```

清單格式、輸出規則、各資料庫的跳脫與長度上限參閱 [comment-sql.md](comment-sql.md)。
