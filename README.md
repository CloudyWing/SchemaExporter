# SchemaExporter

之前撰寫了好幾個版本的工具，用於從資料庫結構中生成 Schema，但因為認為可能用不到而沒有保留，結果後來又需要使用，所以現在重新撰寫了一個版本來做備份。

目前這個版本整理成共用 Core、WPF 桌面程式和 CLI。平常手動匯出可以用桌面版，要做排程、自動化或 snapshot / diff 時就用 CLI。

## 專案組成

- `src\SchemaExporter`：WPF 桌面程式，以命令列引數觸發時進入 CLI 模式
- `src\SchemaExporter.Core`：共用核心邏輯，負責 provider、匯出流程、snapshot / diff、診斷資訊
- `tests\SchemaExporter.Core.Tests`：NUnit 測試

## 目前支援的內容

- SQL Server / Oracle 兩種資料庫
- 透過 provider abstraction 切換資料來源
- 主要輸出為 Excel
- 可選擇額外產生 manifest、JSON sidecar、Markdown sidecar
- 可產生 schema snapshot，並和既有 snapshot 做 diff
- 可用 export profile 控制 schema / object 篩選與是否包含 view
- 匯出時會收集 diagnostics，方便看警告和支援狀態

## 文件

- [快速入門](docs/getting-started.md)
- [設定檔說明](docs/configuration.md)
- [WPF 版操作說明](docs/wpf.md)
- [CLI 版操作說明](docs/cli.md)
- [Manifest 格式](docs/manifest.md)
- [Snapshot 格式](docs/snapshot.md)
- [Diff 格式](docs/diff.md)
- [診斷資訊說明](docs/diagnostics.md)

## 建置與測試

```powershell
dotnet build .\SchemaExporter.slnx -v minimal
dotnet test .\SchemaExporter.slnx -v minimal --no-build
```

## 授權

- [LICENSE.md](LICENSE.md)
