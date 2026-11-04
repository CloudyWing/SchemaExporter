# Desktop 版操作說明

Desktop 版執行檔為 `CloudyWing.SchemaExporter.exe`，以 WPF 視窗承載 Blazor Hybrid 介面（底層為 WebView2），不啟動本機 Web Server。介面分為「匯出」與「設定」兩頁，設定與 CLI 共用 `%LocalAppData%\SchemaExporter\appsettings.json`。

介面採用 MudBlazor 元件。左側導覽可用 AppBar 的選單按鈕收合。

AppBar 右側的主題按鈕可選擇「跟隨系統」「淺色」「深色」，選擇後立即套用並儲存在 `%LocalAppData%\SchemaExporter\desktop.json`。這個檔案只供 Desktop 使用，不影響 CLI 與 `appsettings.json`；檔案缺漏或損毀時使用「跟隨系統」。

## 執行需求

- Windows 10 或以上。
- Microsoft Edge WebView2 Runtime。Windows 11 與已更新的 Windows 10 通常已內建；若畫面空白或無法啟動，請安裝 Evergreen 版 WebView2 Runtime。

---

## 匯出頁

### 資料庫連線

下拉選單，列出 `Schema.Connections` 中的連線，顯示名稱與資料庫類型，不顯示連線字串。

切換連線時，Export Profile 會改為該連線 `ExportProfileName` 指定的設定檔；設定檔不存在時改用第一筆，並在狀態區顯示警告。「管理設定」會切換到設定頁。

### Export Profile

下拉選單，列出 `Schema.ExportProfiles`。決定哪些 Schema 與物件會被納入匯出，以及是否包含檢視表。欄位說明參閱 [configuration.md](configuration.md)。

### 輸出資料夾

指定 Excel 輸出的目標資料夾，預設值來自 `Schema.ExportPath`。「瀏覽」開啟 Windows 資料夾選擇對話框，「開啟」以檔案總管開啟資料夾，資料夾不存在時會先建立。

### 進階選項

預設收合，標題右側會顯示目前會產生的產物，例如「Excel + Summary、Snapshot」。所有既有產物都可在此啟用，命名與用途參閱 [artifacts.md](artifacts.md)。

| 區塊 | 選項 |
| --- | --- |
| 額外輸出 | Schema Summary、Markdown Sidecar、JSON Sidecar、Schema Snapshot、Manifest。 |
| 差異比對 | 基準 Snapshot。填入前次匯出的 `.snapshot.json` 後，本次匯出會一併產生 `.diff.json`；留空不比對。 |
| 檔名與完成後動作 | 是否附加時間戳記與時間戳記格式、檔案已存在時的處理方式（覆寫、附加序號、停止匯出）、完成後是否開啟輸出資料夾。 |

「將目前選項存為預設值」會把輸出資料夾、上述選項（基準 Snapshot 除外）以及目前選取的連線與 Profile 寫入設定檔。連線、Profile 與 Redaction 規則以設定檔最新內容為準，不會被覆寫。基準 Snapshot 只對單次匯出有效，每次載入都會清空。

### 開始匯出與取消

「開始匯出」在連線、Profile 與輸出資料夾都已填寫時才可按下。匯出期間表單鎖定、設定頁停用，按鈕改為「取消匯出」。取消後狀態區會提示輸出資料夾可能留有未完成的檔案。

驗證、連線與輸出錯誤會以 Windows 對話框顯示，訊息中的連線字串密碼會先遮罩。

### 狀態、結果與診斷

- 狀態區顯示目前進度訊息與進度列；完成時依有無警告或錯誤顯示不同顏色。
- 匯出結果列出本次產生的每個檔案與所在資料夾，可直接開啟輸出資料夾。
- 診斷資訊顯示錯誤、警告、資訊數量；有警告或錯誤時自動展開明細表，欄位說明參閱 [diagnostics.md](diagnostics.md)。

---

## 設定頁

設定頁有三個分頁。按下「儲存」會驗證後寫入設定檔並回到匯出頁；驗證失敗時不會寫入任何內容，錯誤訊息顯示在頁面下方。「取消」會捨棄尚未儲存的修改。

設定頁只寫入連線、Export Profile 與 Redaction，輸出資料夾與輸出選項以設定檔最新內容為準。

### Connections

| 欄位 | 說明 |
| --- | --- |
| 連線名稱 | 顯示於匯出頁，也是 CLI `--connection` 使用的名稱。 |
| 資料庫類型 | `SqlServer` 或 `Oracle`。 |
| ConnectionString | 預設以遮罩顯示，按「顯示」才會顯示明文。也可輸入 `${env:變數名稱}`，改從環境變數讀取。 |
| 加密儲存 | 勾選後以 Windows DPAPI 加密寫入設定檔，只有這台電腦的目前使用者能解開。環境變數參照不需加密，此選項會停用。在其他電腦加密、無法解開的連線字串會顯示警告，重新輸入即可；未重新輸入時保留原本的加密值。 |
| 預設 Export Profile | 從現有 Profile 選擇；選「使用第一個設定檔」則不指定。 |

### Export Profiles

| 欄位 | 說明 |
| --- | --- |
| 設定檔名稱 | 修改名稱時，參照舊名稱的連線會一併更新。刪除 Profile 時，參照它的連線會改為不指定。 |
| View | 是否包含資料庫檢視表。 |
| Include / Exclude Schemas、Objects | 每行一筆 pattern，空白行與重複項目會自動忽略。 |

### Redaction

| 欄位 | 說明 |
| --- | --- |
| 啟用 | 是否在寫出任何檔案前遮罩敏感 metadata。 |
| 替代文字 | 遮罩後的文字，預設 `[REDACTED]`。 |
| 敏感名稱 Pattern | 每行一筆 Regex，比對欄位與物件名稱。 |
| 敏感內容 Pattern | 每行一筆 Regex，比對描述、預設值與程式定義。 |

---

## 更新

啟動後會在背景檢查 GitHub Release 是否有新版本。有新版本時會詢問是否下載並重新啟動；選擇稍後時，側邊欄會顯示提示，下次啟動再詢問。更新不會覆蓋 `%LocalAppData%\SchemaExporter\appsettings.json`。

## 與 CLI 的關係

Desktop 執行檔不再提供 CLI 模式。帶任何命令列引數啟動時會直接以結束代碼 `1` 結束，不開啟視窗。自動化與 AI Agent 請改用 `tools\CloudyWing.SchemaExporter.Cli.exe`，參閱 [cli.md](cli.md)。
