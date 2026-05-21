# SchemaExporter

之前撰寫了好幾個版本的工具，用於從資料庫結構中生成 Schema，但因為認為可能用不到而沒有保留，結果後來又需要使用，所以現在重新撰寫了一個版本來做備份。

目前這個版本整理成共用 Core、WPF 桌面程式和 CLI。平常手動匯出可以用桌面版，要做排程、自動化或 snapshot / diff 時就用 CLI。使用者設定會存放在 `%LocalAppData%\SchemaExporter\appsettings.json`，避免更新時覆蓋連線與匯出設定。

## 專案組成

- `src\SchemaExporter`：WPF 桌面程式，帶命令列引數時切換為 CLI 模式
- `src\SchemaExporter.Core`：核心函式庫，包含 provider、匯出流程、snapshot / diff 與診斷邏輯
- `tests\SchemaExporter.Core.Tests`：Core 的 NUnit 測試
- `tests\SchemaExporter.Core.IntegrationTests`：Provider integration tests，透過 Testcontainers 啟動資料庫 fixture
- `tests\SchemaExporter.Tests`：WPF、CLI 與設定流程的 NUnit 測試
- `tests\SchemaExporter.ProviderFixtures`：Provider integration tests 使用的 schema fixture scripts

## 主要功能

### 資料庫支援

- 支援 Microsoft SQL Server 與 Oracle Database。
- 透過 provider abstraction 切換資料來源，各 provider 的 metadata 支援範圍詳見 [Provider Capability Matrix](docs/provider-capabilities.md)。

### 匯出產物

- 主要輸出為 Excel 活頁簿。
- 可額外產生 manifest、JSON sidecar、Markdown sidecar、Schema Summary 等 artifact。
- 可產生 schema snapshot，並與任一份既有 snapshot 進行 diff，輸出 JSON 或 Markdown 報告。

### 設定與安全

- 使用者設定存放於 `%LocalAppData%\SchemaExporter\appsettings.json`，避免應用程式更新時覆蓋連線與匯出設定。
- 透過 export profile 控制 schema / object 篩選與是否包含 view。
- 可啟用 redaction 規則，於輸出前遮罩描述、預設值與 routine 定義等敏感 metadata。
- 匯出過程會收集 diagnostics，提供資訊、警告、錯誤與 provider 支援層級。

## 工作流概觀

```text
資料庫 ──► export ──► Excel + artifacts (manifest / sidecar / snapshot)
                              │
                              ▼
              snapshot ──► diff ──► JSON / Markdown 差異報告
```

匯出時可同時產生 snapshot；後續搭配 baseline snapshot 與 `diff` 命令，即可在 CI 中偵測 schema drift。

## 文件

### 入門與操作

- [快速入門](docs/getting-started.md)
- [WPF 版操作說明](docs/wpf.md)
- [CLI 版操作說明](docs/cli.md)
- [設定檔說明](docs/configuration.md)

### 輸出格式契約

- [輸出產物定位](docs/artifacts.md)
- [Manifest 格式](docs/manifest.md)
- [Snapshot 格式](docs/snapshot.md)
- [Diff 格式](docs/diff.md)
- [Schema Summary 格式](docs/schema-summary.md)

### 進階主題

- [CI 使用範例](docs/ci.md)
- [Provider Fixture 資料庫](docs/provider-fixtures.md)
- [Provider Capability Matrix](docs/provider-capabilities.md)
- [診斷資訊說明](docs/diagnostics.md)
- [技術棧說明](docs/tech-stack.md)

## 建置與測試

建置整個解決方案：

```powershell
dotnet build .\SchemaExporter.slnx -v minimal
```

執行測試（一般測試不需要資料庫 fixture）：

```powershell
dotnet test .\SchemaExporter.slnx -v minimal --no-build
```

從原始碼直接執行 CLI：

```powershell
dotnet run --project .\src\SchemaExporter\SchemaExporter.csproj -- export --connection <name>
```

若要驗證 provider 對實體資料庫的查詢行為，請執行 [Provider Fixture 資料庫](docs/provider-fixtures.md) 說明的 integration test 指令。Integration tests 會透過 Testcontainers 啟動資料庫 fixture。

## 授權

本專案採用 MIT 授權，詳見 [LICENSE.md](LICENSE.md)。
