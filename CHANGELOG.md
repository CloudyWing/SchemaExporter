# CHANGELOG

## v0.2.1 (2026-04-14)

### Bug Fixes

- 修正設定檔讀寫路徑，改存放於 `%LocalAppData%\SchemaExporter\`，並在更新前自動遷移現有設定，避免 Velopack 更新時覆蓋使用者設定。

## v0.2.0 (2026-04-11)

### New Features

- 新增「儲存設定」按鈕，可持久化匯出路徑、輸出選項及上次選取的連線與設定檔，重啟應用程式後自動還原。
