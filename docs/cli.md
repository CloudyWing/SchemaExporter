# CLI 版操作說明

CLI 版執行檔為 `schemaexporter`，支援兩個命令：`export` 與 `diff`。

```text
schemaexporter export --connection <name> [options]
schemaexporter diff --left <path> --right <path> [options]
schemaexporter --help
```

CLI 版從 `%LocalAppData%\SchemaExporter\appsettings.json` 讀取使用者設定。若使用者設定檔尚不存在，啟動時會先從執行檔目錄的 `appsettings.json` 範本複製一份。除非以參數覆蓋，否則所有預設值均來自使用者設定檔。

---

## export 命令

連線至資料庫並匯出 schema 為 Excel 檔案。

```bash
schemaexporter export --connection <name> [options]
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
| `--open-output-folder` | 匯出完成後開啟輸出資料夾（僅在 Windows 桌面環境有效）。 |
| `--no-open-output-folder` | 強制不開啟輸出資料夾。 |
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
| `1` | `ArgumentError` | 參數解析失敗（顯示 usage 後結束）。 |
| `2` | `WorkflowError` | 匯出流程失敗（如驗證錯誤、連線失敗、輸出失敗）。 |
| `3` | `UnexpectedError` | 未預期的錯誤。 |

### 範例

```bash
# 基本匯出
schemaexporter export --connection prod

# 覆蓋設定檔與輸出路徑
schemaexporter export --connection prod --profile TablesOnly --output C:\Reports

# 匯出並產生 snapshot 與 manifest
schemaexporter export --connection prod --snapshot --manifest

# 匯出並與基準 snapshot 產生差異比對
schemaexporter export --connection prod --snapshot --diff-from C:\Snapshots\baseline.snapshot.json

# 匯出精簡 Schema Summary
schemaexporter export --connection prod --schema-summary

# 停用時間戳記
schemaexporter export --connection prod --no-timestamp
```

---

## diff 命令

比對兩份 schema snapshot 檔案並輸出差異報告，不需要連線資料庫。

Snapshot 路徑可使用絕對路徑或相對路徑；相對路徑會依目前工作目錄解析。

```bash
schemaexporter diff --left <path> --right <path> [options]
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
| `--format <json\|markdown>` | 指定輸出格式。省略時依副檔名自動判斷（`.md` 為 Markdown，其餘為 JSON）。 |
| `--help` / `-h` / `/?` | 顯示說明文字。 |

### 結束代碼

| 代碼 | 名稱 | 說明 |
| --- | --- | --- |
| `0` | `Success` | 成功，或使用者要求顯示 help。 |
| `1` | `ArgumentError` | 參數解析失敗。 |
| `2` | `WorkflowError` | 比對流程失敗（如找不到檔案、格式錯誤）。 |
| `3` | `UnexpectedError` | 未預期的錯誤。 |

### 範例

```bash
# 輸出 Markdown 至 console
schemaexporter diff --left baseline.snapshot.json --right current.snapshot.json

# 寫入 Markdown 檔案
schemaexporter diff --left baseline.snapshot.json --right current.snapshot.json --output diff.md

# 寫入 JSON 檔案
schemaexporter diff --left baseline.snapshot.json --right current.snapshot.json --output diff.json

# 明確指定格式
schemaexporter diff --left baseline.snapshot.json --right current.snapshot.json --output report.txt --format markdown
```
