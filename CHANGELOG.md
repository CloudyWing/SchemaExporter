# CHANGELOG

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
