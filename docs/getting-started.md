# 快速入門

## 前置需求

- .NET 10.0 Runtime 或以上
- 支援的資料庫：Microsoft SQL Server、Oracle Database
- （WPF 版）Windows 10 或以上

## 安裝

從原始碼建置：

```bash
dotnet build
```

## 設定

所有行為由 `appsettings.json` 控制。建置完成後，執行前請先確認以下設定：

1. `Schema.ExportPath`：Excel 輸出的目標資料夾路徑。
2. `Schema.Connections`：至少一筆資料庫連線設定。

詳細設定說明參閱 [configuration.md](configuration.md)。

## 使用 WPF 版

啟動 `SchemaExporter.exe`，介面會自動載入 `appsettings.json` 中的連線與設定檔清單。

選擇連線與設定檔後，按下「匯出」即可開始。詳細欄位說明參閱 [wpf.md](wpf.md)。

## 使用 CLI 版

```bash
# 匯出
schemaexporter export --connection Key1

# 比對兩份 snapshot
schemaexporter diff --left old.snapshot.json --right new.snapshot.json
```

詳細參數說明參閱 [cli.md](cli.md)。
