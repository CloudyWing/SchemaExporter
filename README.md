# SchemaExporter

之前撰寫了好幾個版本的工具，用於從資料庫結構中生成 Schema，但因為認為可能用不到而沒有保留，結果後來又需要使用，所以現在重新撰寫了一個版本來做備份。

目前這個版本整理成共用 Core、WPF 桌面程式和 CLI。平常手動匯出可以用桌面版，要做排程、自動化或 snapshot / diff 時就用 CLI。

## 專案組成

- `src\SchemaExporter`：WPF 桌面程式
- `src\SchemaExporter.Core`：共用核心邏輯，負責 provider、匯出流程、snapshot / diff、診斷資訊
- `src\SchemaExporter.Cli`：命令列入口
- `src\SchemaExporter.Core.Tests`：NUnit 測試

## 目前支援的內容

- SQL Server / Oracle 兩種資料庫
- 透過 provider abstraction 切換資料來源
- 主要輸出為 Excel
- 可選擇額外產生 manifest、JSON sidecar、Markdown sidecar
- 可產生 schema snapshot，並和既有 snapshot 做 diff
- 可用 export profile 控制 schema / object 篩選與是否包含 view
- 匯出時會收集 diagnostics，方便看警告和支援狀態

## 設定

桌面版和 CLI 都讀同一種 `appsettings.json` 格式。開發時直接修改 `src\SchemaExporter\appsettings.json` 即可；CLI 專案會把這份檔案一起帶到輸出目錄。

每個連線設定只使用單一 `ConnectionString` 欄位，桌面版與 CLI 都直接以這個連線字串連線資料庫。

範例：

```json
{
  "Schema": {
    "ExportPath": "D:\\SchemaExports",
    "Connections": [
      {
        "Name": "SqlServerDev",
        "DatabaseType": "SqlServer",
        "ConnectionString": "Server=.;Database=SchemaExporter;Trusted_Connection=True;TrustServerCertificate=True;",
        "ExportProfileName": "Default"
      },
      {
        "Name": "OracleDev",
        "DatabaseType": "Oracle",
        "ConnectionString": "Data Source=OracleDev;User Id=schema_user;Password=<password>;",
        "ExportProfileName": "TablesOnly"
      }
    ],
    "ExportProfiles": [
      {
        "Name": "Default",
        "IncludeSchemas": [],
        "ExcludeSchemas": [
          "sys",
          "INFORMATION_SCHEMA"
        ],
        "IncludeObjects": [],
        "ExcludeObjects": [],
        "IncludeViews": true
      },
      {
        "Name": "TablesOnly",
        "IncludeSchemas": [
          "dbo"
        ],
        "ExcludeSchemas": [],
        "IncludeObjects": [],
        "ExcludeObjects": [
          "*_bak",
          "*_history"
        ],
        "IncludeViews": false
      }
    ],
    "ExportResultOptions": {
      "UseTimestamp": true,
      "TimestampFormat": "yyyyMMdd_HHmmss",
      "OverwriteStrategy": "AppendSuffix",
      "OpenOutputFolder": false,
      "GenerateManifest": true,
      "GenerateJsonSidecar": true,
      "GenerateMarkdownSidecar": true,
      "GenerateSchemaSnapshot": true,
      "DiffSourceSnapshotPath": null
    }
  }
}
```

補充幾點：

- `DatabaseType` 目前用 `SqlServer` 或 `Oracle`
- `ExportPath` 和 CLI `--output` 建議直接用絕對路徑
- `ConnectionString` 由應用程式直接使用，若需要保護敏感資訊可改由部署環境自行管理 `appsettings.json`
- `ExportProfileName` 沒指定時，會使用預設或第一個 profile

## 桌面版使用

1. 先調整 `src\SchemaExporter\appsettings.json`。
2. 執行桌面程式。
3. 在 UI 選連線、選 profile、確認輸出資料夾。
4. 開始匯出，完成後可直接看診斷資訊或開啟輸出資料夾。

```powershell
dotnet run --project .\src\SchemaExporter
```

## CLI 使用

先看說明：

```powershell
dotnet run --project .\src\SchemaExporter.Cli -- --help
```

匯出：

```powershell
dotnet run --project .\src\SchemaExporter.Cli -- export --connection SqlServerDev
```

指定 profile、輸出資料夾和 sidecar：

```powershell
dotnet run --project .\src\SchemaExporter.Cli -- export --connection OracleDev --profile TablesOnly --output D:\SchemaExports --manifest --json-sidecar --markdown-sidecar --snapshot
```

用既有 snapshot 做 diff：

```powershell
dotnet run --project .\src\SchemaExporter.Cli -- export --connection SqlServerDev --diff-from D:\SchemaExports\baseline_snapshot.json --snapshot
```

直接比對兩份 snapshot 或 schema JSON：

```powershell
dotnet run --project .\src\SchemaExporter.Cli -- diff --left D:\SchemaExports\before_snapshot.json --right D:\SchemaExports\after_snapshot.json --output D:\SchemaExports\schema-diff.md
```

CLI 完成後會列出輸出檔案和 diagnostics；如果 `diff` 沒有指定 `--output`，會直接把 Markdown 結果印到 console。

## 匯出結果

主要輸出是 Excel。其他檔案依設定決定是否產生：

- Excel 工作簿
- `*_manifest.json`
- `schema.json`
- `schema.md`
- `snapshot.json`
- `diff.json`

JSON / Markdown sidecar 會帶出目前匯出的 schema 資訊；如果有開啟 diff，也會把 diff 一起寫進去。匯出時收集到的 diagnostics 會在桌面版和 CLI 一起顯示。

## 建置與測試

目前 solution 已可直接建置並通過測試。從 repo root 執行：

```powershell
dotnet build .\SchemaExporter.slnx -v minimal
dotnet test .\SchemaExporter.slnx -v minimal --no-build
```

## 授權

- [LICENSE.md](LICENSE.md)
