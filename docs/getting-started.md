# 快速入門

## 前置需求

- .NET 10.0 SDK 或以上
- 支援的資料庫：Microsoft SQL Server、Oracle Database
- （Desktop 版）Windows 10 或以上，以及 Microsoft Edge WebView2 Runtime

## 安裝

從原始碼建置：

```powershell
dotnet build .\SchemaExporter.slnx -v minimal
```

## 設定

所有行為由 `appsettings.json` 控制。建置完成後，執行前請先確認以下設定：

1. `Schema.ExportPath`：Excel 輸出的目標資料夾路徑。
2. `Schema.Connections`：至少一筆資料庫連線設定。

詳細設定說明參閱 [configuration.md](configuration.md)。

## 使用 Desktop 版

啟動 `CloudyWing.SchemaExporter.exe`，介面會自動載入 `appsettings.json` 中的連線與設定檔清單。沒有連線時，先到「設定」頁新增。

選擇連線、Export Profile 與輸出資料夾後，按下「開始匯出」即可。詳細欄位說明參閱 [desktop.md](desktop.md)。

## 使用 CLI 版

CLI 執行檔為安裝目錄 `tools\` 下的 `CloudyWing.SchemaExporter.Cli.exe`，也可從 Release 下載 CLI ZIP。

```powershell
# 匯出
& "<install>\tools\CloudyWing.SchemaExporter.Cli.exe" export --connection Key1

# 比對兩份 snapshot
& "<install>\tools\CloudyWing.SchemaExporter.Cli.exe" diff --left old.snapshot.json --right new.snapshot.json
```

詳細參數說明參閱 [cli.md](cli.md)。
