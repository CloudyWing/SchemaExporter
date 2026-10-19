# CHANGELOG

## v0.4.0 (2026-10-20)

### New Features

- 新增 `comment-sql` 命令，依 snapshot 與註解清單（JSON，欄位 `schema`、`object`、`column`、`comment`）產生資料表與欄位的註解 SQL，不連線資料庫。
- SQL Server 的註解 SQL 使用 `sp_addextendedproperty` 與 `sp_updateextendedproperty`（`MS_Description`），Oracle 使用 `COMMENT ON`。
- 註解 SQL 依 snapshot 的現有註解分為新增段與差異段，差異段每筆附資料庫既有值，內容固定排序，相同輸入產生逐位元組相同的檔案。
- 註解超出 SQL Server 擴充屬性值的 7,500 位元組或 Oracle 的 4,000 位元組上限時回報錯誤，不截斷。
- Schema Summary 新增 Relationships 區塊，列出外鍵關聯。
- Schema Summary 新增分檔輸出，依物件拆出明細檔，入口檔以連結索引。
- Snapshot 與 diff 新增獨立的外鍵關聯模型（`schemaVersion` 3），diff 新增 `relationshipChanges`。

### Changes

- Snapshot 索引欄位 `otherColumns` 更名為 `includedColumns`，並移除 `isForeignKey`。讀取 v2 Snapshot 時會自動對應，v2 與 v3 比對時關聯差異保持空白。
- Schema Summary 與明細檔不再輸出 routine 定義本文，只保留 signature 與 description。
- `diff`、`comment-sql`、顯示說明與引數錯誤不再讀取或建立使用者設定檔，設定檔缺漏或損毀時仍可執行。
- 直接相依 `System.Security.Cryptography.Pkcs` 10.0.13，取代 `Microsoft.Data.SqlClient` 與 `Oracle.ManagedDataAccess.Core` 傳遞帶入的 9.0.13，排除 GHSA-846f-f764-76mx 與 GHSA-2fmv-rv2c-59x7 兩項已知弱點。

### Bug Fixes

- 修正命令列的第一個引數無法辨識時（例如打錯命令名稱），會開出沒有人操作的桌面視窗而不結束的問題，改為輸出 `Unknown command: <name>` 與 usage，並以結束代碼 `1` 結束。
- 修正使用者設定檔內容無法解析時，匯出命令因未處理的例外而當掉，改為輸出 `Startup failed` 並以結束代碼 `1` 結束。
- 修正分檔輸出清理時可能刪除使用者自行放入明細目錄的檔案。
- 修正外鍵目錄查詢的權限不足判定，改依錯誤碼判斷，其餘資料庫錯誤照常拋出。
- 修正明細連結對含空白或井字號的目錄與檔名未做 URL 編碼。
- 分檔發布改為先寫入暫存再替換入口檔，中途失敗時還原既有內容。
- 匯出失敗訊息在寫入記錄與命令列輸出前遮罩連線字串，並遞迴處理內部例外。

### Testing

- 新增註解 SQL 的單元測試，涵蓋單引號、中文與 emoji、CRLF 與 LF 多行、空字串、只含空白與前後空白、超長註解、特殊物件名稱、SQL Server 的 `GO` 與 `$(`，以及 Oracle 的 `&`、SQL*Plus 特殊行與長度上限。
- 新增註解 SQL 的整合測試，在 Testcontainers 啟動的 SQL Server 與 Oracle 容器內以 `sqlcmd` 與 `sqlplus` 實際執行產出的 SQL，讀回註解與輸入逐字比對，並確認重新匯出 snapshot 後再產生一次沒有差異。
- 新增測試確認命令列拒絕 `--connection-string-env`、`--connection-string-stdin` 與 `--database-type`，且錯誤訊息不回顯傳入的值。
- 整合測試專案直接相依 `SSH.NET` 2026.0.0，排除 Testcontainers 傳遞帶入的 2025.1.0 已知弱點。
- 修正 Oracle 外鍵目錄權限不足的整合測試：改用管理者連線建立受限使用者，並以 synonym 模擬目錄檢視無權限，移除與主鍵重複的索引建立。

### Documentation

- 新增 `docs/comment-sql.md`，說明註解清單格式、輸出規則、各資料庫的跳脫與長度上限。
- `docs/cli.md` 新增 `comment-sql` 命令與結束代碼，`docs/provider-fixtures.md` 補充註解 SQL 整合測試的執行方式。

## v0.3.0 (2026-05-21)

### New Features

- 新增共用匯出請求解析流程，讓 WPF 與 CLI 共用 snapshot、diff 與附加產物產生邏輯。
- 新增 AI context 匯出，產生精簡 Schema Summary Markdown，供 Agent、CI 與審查流程使用。
- 新增 provider capability matrix，標示 SQL Server 與 Oracle 的 schema metadata 支援範圍。
- 新增 redaction 規則，可在輸出前遮罩描述、預設值與 routine 定義等敏感 metadata。

### Bug Fixes

- 修正 artifact JSON 欄位命名與 enum 序列化契約，並支援 `diff` 命令使用相對 snapshot 路徑。

### Testing

- 新增 CLI export 成品輸出端對端測試，驗證 workbook、manifest、sidecar、snapshot 與 summary 輸出。
- 新增 SQL Server provider fixture 與 metadata 驗證，涵蓋 table、view、column、index、foreign key 與 routine。
- 將 provider integration tests 抽離為 `SchemaExporter.Core.IntegrationTests`，改由 Testcontainers 自動啟動 SQL Server 與 Oracle fixture，不再需要手動 Docker Compose 與連線字串環境變數。

### Documentation

- 補齊 Schema Summary CLI 範例與輸出產物定位說明，區分 Markdown sidecar 與 AI context Markdown 的用途。
- 新增 CI schema drift 使用範例，示範 snapshot、diff 與 artifact 上傳流程。
- 整理 README 與 provider fixture 文件，說明 integration tests 的 Testcontainers 執行方式。

## v0.2.1 (2026-04-14)

### Bug Fixes

- 修正設定檔讀寫路徑，改存放於 `%LocalAppData%\SchemaExporter\`，並在更新前自動遷移現有設定，避免 Velopack 更新時覆蓋使用者設定。

## v0.2.0 (2026-04-11)

### New Features

- 新增「儲存設定」按鈕，可持久化匯出路徑、輸出選項及上次選取的連線與設定檔，重啟應用程式後自動還原。
