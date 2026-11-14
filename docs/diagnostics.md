# 診斷資訊說明

診斷資訊在匯出過程中產生，記錄值得注意的狀況。診斷不代表匯出失敗，但警告層級的項目表示輸出結果可能不完整，建議確認。

診斷資訊會出現在以下位置：

- Desktop 版：匯出頁在匯出完成後顯示的診斷資訊表格。
- CLI 版：匯出完成後的 console 輸出。
- Manifest 檔案：`diagnostics` 陣列。
- Snapshot 檔案：`diagnostics` 陣列。

---

## 嚴重性（Severity）

| 值 | 顯示文字 | 說明 |
| --- | --- | --- |
| `Info` | 資訊 | 一般提示，不影響輸出結果。 |
| `Warning` | 警告 | 匯出結果可能不完整，建議確認。 |
| `Error` | 錯誤 | 已知問題或無法完成的項目。流程是否中止由對應例外與 CLI exit code 決定。 |

---

## 類別（Category）

| 值 | 顯示文字 | 說明 |
| --- | --- | --- |
| `General` | 一般 | 不屬於其他類別的一般性診斷，例如匯出完成但無法自動開啟輸出資料夾。 |
| `Filtering` | 篩選 | 與篩選條件相關，如某個物件被排除、篩選結果為空等。 |
| `Naming` | 命名 | 與物件或欄位命名相關，如命名衝突或無效字元。 |
| `ViewSupport` | 檢視表支援 | 關於特定資料庫對檢視表的支援狀況。 |
| `Configuration` | 設定 | 與 `appsettings.json` 設定相關，如設定值無效或缺少必要設定。 |
| `RoutineSupport` | 程序支援 | 關於特定資料庫對預存程序或函數的支援狀況。 |
| `Execution` | 執行 | 匯出執行過程中發生的狀況，如特定物件讀取異常。 |
| `Redaction` | Redaction | 敏感 metadata 遮罩已套用的資訊。 |

---

## 支援層級（SupportLevel）

部分診斷會附帶支援層級，說明目前連線資料庫對該功能的支援程度：

| 值 | 顯示文字 | 說明 |
| --- | --- | --- |
| `Full` | 完整支援 | 此功能在目前資料庫上完整可用。 |
| `Partial` | 部分支援 | 此功能在目前資料庫上有限制，輸出結果可能不完整。 |
| `Unsupported` | 不支援 | 此功能在目前資料庫上不支援，相關資料不會出現在輸出中。 |

各 provider 的完整支援範圍參閱 [provider-capabilities.md](provider-capabilities.md)。

---

## 診斷欄位對照

Desktop 匯出頁診斷資訊表格與 CLI / 檔案輸出的對照：

| Desktop 欄位 | JSON 欄位 | CLI 顯示 |
| --- | --- | --- |
| 嚴重性 | `severity` | `[嚴重性/類別]` 前綴 |
| 類別 | `category` | `[嚴重性/類別]` 前綴 |
| 支援層級 | `supportLevel` | 包含在訊息中 |
| 影響物件 | `affectedObject` | 包含在訊息中 |
| 訊息 | `message` | 訊息本文 |

---

## 開啟輸出資料夾的警告

要求匯出完成後開啟輸出資料夾（Desktop 勾選對應選項，或 CLI 指定 `--open-output-folder`）時，若無法開啟，匯出仍視為成功，只會加入一筆 `Warning` / `General` 診斷：

| 情況 | 訊息 |
| --- | --- |
| Host 未註冊 `IOutputFolderLauncher` | `匯出完成，但目前執行環境未提供開啟資料夾功能。` |
| 作業系統無法開啟資料夾 | `匯出完成，但無法自動開啟輸出資料夾：<原因>` |

Desktop 與 CLI 都透過 `AddSchemaExporterInfrastructure()` 註冊 `ShellOutputFolderLauncher`，第一種訊息只會出現在未註冊 launcher 的 Host。開啟資料夾發生在 Manifest 與 Snapshot 寫出之後，因此這類診斷只會出現在 Desktop 診斷資訊表格與 CLI console 輸出，不會寫入 Manifest 或 Snapshot 的 `diagnostics` 陣列。
