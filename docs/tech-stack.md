# 技術棧說明

本文件說明 SchemaExporter 所使用的特殊技術，包含各技術的概念介紹、在本專案中的用法，以及需要特別注意的細節。

---

## 目錄

- [Velopack — 自動更新框架](#velopack)
- [CommunityToolkit.Mvvm — MVVM Source Generator](#communitytoolkitmvvm)
- [CloudyWing.SpreadsheetExporter — Excel 匯出](#cloudywingspreadsheetexporter)
- [MinVer — 語意版號控制](#minver)
- [LibraryImport — 現代 P/Invoke](#libraryimport)
- [LoggerMessage — 高效能日誌](#loggermessage)
- [Dapper — 輕量 ORM](#dapper)
- [Testcontainers — 整合測試容器](#testcontainers)
- [CLI 模式進入點](#cli-模式進入點)

---

## Velopack

### 概念

Velopack 是一個 Windows 應用程式打包與自動更新框架，用來取代 Squirrel.Windows。它負責：

- 建立可安裝的 `.exe` 安裝檔。
- 在應用程式啟動時檢查並下載新版本。
- 在不中斷使用者操作的情況下更新並重啟。

### 初始化（必要步驟）

`App.xaml.cs` 建構式的**第一行**必須呼叫 `VelopackApp.Build().Run()`。這行程式碼處理安裝器、解除安裝器與更新鉤子事件，若未放在進入點最前端，更新流程會無法正確執行。

```csharp
// App.xaml.cs
public App() {
    VelopackApp.Build().Run();
}
```

### 更新來源設定

本專案使用 GitHub Releases 作為更新來源。`VelopackUpdateService` 在建構時建立 `UpdateManager`：

```csharp
updateManager = new UpdateManager(
    new GithubSource(GitHubRepository, accessToken: null, prerelease: false)
);
```

- `GitHubRepository`：格式為 `https://github.com/Owner/Repo`。
- `prerelease: false`：僅提供正式版更新，忽略預發布標籤。
- `accessToken`：公開儲存庫不需要，私有儲存庫需要填入 PAT。

### 更新生命週期

更新分三個步驟，分別對應 `IUpdateService` 的三個方法：

#### 1. 檢查是否有新版本

```csharp
UpdateInfo? update = await updateService.CheckForUpdatesAsync();
if (update is null) return; // null 表示目前已是最新版，或不是安裝版執行
```

`updateManager.IsInstalled` 為 `false` 時（開發階段直接從 IDE 執行），`CheckForUpdatesAsync` 直接回傳 `null` 並記錄日誌，不會拋出例外。

#### 2. 下載更新包

```csharp
Progress<int> progress = new(percent => {
    viewModel.StatusMessage = $"正在下載更新... {percent}%";
});

await updateService.DownloadUpdateAsync(update, progress);
```

下載在背景非同步進行，`IProgress<int>` 傳入下載百分比讓 UI 能即時顯示進度。

#### 3. 套用並重啟

```csharp
updateService.ApplyUpdateAndRestart(update);
```

這個呼叫不會 `return`，應用程式會直接關閉並由新版重啟，因此後續程式碼不會被執行。

### 完整更新流程（MainWindow.xaml.cs）

```csharp
public async Task CheckForUpdatesAsync() {
    UpdateInfo? update = await updateService.CheckForUpdatesAsync();
    if (update is null) return;

    MessageBoxResult result = MessageBox.Show(
        $"偵測到新版本 {update.TargetFullRelease.Version}，是否立即下載？",
        "有可用更新", MessageBoxButton.YesNo, MessageBoxImage.Information
    );
    if (result != MessageBoxResult.Yes) return;

    Progress<int> progress = new(percent => {
        viewModel.StatusMessage = $"正在下載更新... {percent}%";
    });
    await updateService.DownloadUpdateAsync(update, progress);
    updateService.ApplyUpdateAndRestart(update);
}
```

### 打包與發布

使用 Velopack CLI 打包：

```bash
# 安裝 Velopack CLI
dotnet tool install -g vpk

# 打包（從 publish 輸出目錄建立安裝檔）
vpk pack \
  --packId CloudyWing.SchemaExporter \
  --packVersion 1.0.0 \
  --packDir publish/ \
  --mainExe CloudyWing.SchemaExporter.exe

# 上傳至 GitHub Releases（搭配 MinVer 版號）
vpk upload github \
  --repoUrl https://github.com/Owner/SchemaExporter \
  --token $GITHUB_TOKEN
```

---

## CommunityToolkit.Mvvm

### 概念

CommunityToolkit.Mvvm 提供 WPF/MAUI MVVM 開發的 Source Generator，在編譯期自動產生 `INotifyPropertyChanged` 屬性、`ICommand` 實作等，消除樣板程式碼。

本專案使用 8.x 版本，搭配 .NET 10 的 `partial` 屬性語法。

### ObservableProperty

在 `partial class` 裡以 `[ObservableProperty]` 標記 `partial` 屬性，Source Generator 自動產生有 `SetProperty` 呼叫的完整屬性。

```csharp
// ViewModel.cs（輸入）
public partial class ViewModel : ObservableObject {
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
    public partial SchemaConnection? Connection { get; set; }

    [ObservableProperty]
    public partial bool IsExporting { get; set; }
}

// 相當於 Source Generator 產生：
// public SchemaConnection? Connection {
//     get => _connection;
//     set => SetProperty(ref _connection, value);
// }
```

`[NotifyCanExecuteChangedFor]` 讓屬性變更時自動觸發指定命令的 `CanExecuteChanged`，這樣按鈕啟用狀態會即時更新。

### RelayCommand

```csharp
[RelayCommand(CanExecute = nameof(CanSubmit))]
private async Task SubmitAsync() {
    IsExporting = true;
    // ...
}

private bool CanSubmit() =>
    !IsExporting && Connection is not null && SelectedProfile is not null;
```

Source Generator 產生 `SubmitCommand` 屬性，繫結至 XAML 的 `Command="{Binding SubmitCommand}"`。

### OnPropertyChanged 鉤子

屬性變更後可用 `partial` 方法接收通知：

```csharp
// ViewModel.cs
partial void OnConnectionChanged(SchemaConnection? value) {
    SelectedProfile = ResolveProfile(SelectedProfile?.Name, value);
}
```

此方法由 Source Generator 自動呼叫，不需手動觸發。

### SetProperty（SettingsViewModel 模式）

`SettingsViewModel` 使用 `field` 關鍵字（C# 13 / .NET 9+）搭配 `SetProperty`：

```csharp
public EditableConnection? SelectedConnection {
    get;
    set => SetProperty(ref field, value);
}
```

`field` 關鍵字讓屬性內部直接存取編譯器隱含產生的支援欄位，不需要手動宣告 `_selectedConnection`。

### ObservableCollection

所有 UI 需要繫結的集合使用 `ObservableCollection<T>`，元素增刪時 UI 自動更新：

```csharp
public ObservableCollection<SchemaConnection> Connections { get; } = [];
public ObservableCollection<ExportProfile> ExportProfiles { get; } = [];
public ObservableCollection<ExportDiagnostic> Diagnostics { get; } = [];
```

---

## CloudyWing.SpreadsheetExporter

### 概念

CloudyWing.SpreadsheetExporter 是一個建構式 Excel 匯出函式庫，以宣告式 API 定義工作表結構，透過 Renderer（本專案使用 ClosedXML）輸出實際 Excel 檔案。

核心概念：

- **SpreadsheetManager**：全域設定中心（Renderer、預設樣式）。
- **SheetDefinition**：代表一個工作表，可附加多個 Template。
- **GridTemplate**：自由格式方格，逐列逐儲存格定義。
- **RecordSetTemplate**：資料列表，自動根據集合渲染多列。

### 初始化（SpreadsheetExporterBootstrapper）

`SpreadsheetExporterBootstrapper.Configure()` 在 `App.xaml.cs` 啟動時呼叫一次，使用 `lock` 確保執行緒安全的一次性初始化：

```csharp
SpreadsheetManager.SetRenderer(static () => new ExcelRenderer());

CellStyle baseCellStyle = new(
    HorizontalAlignment.Center,
    VerticalAlignment.Middle,
    hasBorder: false,
    wrapText: true,
    backgroundColor: Color.Empty,
    font: new CellFont("微軟正黑體", 10, Color.Empty, FontStyles.None),
    format: null,
    shrinkToFit: false
);

SpreadsheetManager.DefaultCellStyles = new CellStyleConfiguration {
    CellStyle    = baseCellStyle,
    GridCellStyle = baseCellStyle with { HorizontalAlignment = HorizontalAlignment.Left },
    HeaderStyle  = baseCellStyle with {
        Font = baseCellStyle.Font with { Style = FontStyles.Bold },
        HasBorder = true
    },
    FieldStyle   = baseCellStyle with {
        HorizontalAlignment = HorizontalAlignment.Left,
        HasBorder = true
    }
};
```

`CellStyle` 是 `record struct`，使用 `with` 語法派生新樣式，不修改原始值。

### 建立工作表與設定欄寬

```csharp
SheetDefinition sheet = document.CreateSheet("TableName");

sheet.SetColumnWidth(0, 40D)  // 欄位名稱
     .SetColumnWidth(1, 15D)  // 欄位型別
     .SetColumnWidth(2, 15D)  // 預設值
     .SetColumnWidth(3, 10D); // 是否允許 Null
```

`SetColumnWidth` 回傳 `this`，支援鏈式串接。

### GridTemplate — 自由格式標頭

```csharp
GridTemplate headerTemplate = new();

headerTemplate
    .CreateRow()
        .CreateCell("Schema：",             cellStyle: headerLabelStyle)
        .CreateCell(databaseObject.SchemaName, colSpan: 2)
        .CreateCell("物件名稱：",           cellStyle: headerLabelStyle)
        .CreateCell(databaseObject.ObjectName, colSpan: 3)
    .CreateRow(Constants.AutoFitRowHeight)
        .CreateCell("類型：",               cellStyle: headerLabelStyle)
        .CreateCell(databaseObject.ObjectType, colSpan: 2)
        .CreateCell("資料表描述：",         cellStyle: headerLabelStyle)
        .CreateCell(databaseObject.ObjectDescription ?? "", colSpan: 3);

sheet.AddTemplate(headerTemplate);
```

`CreateRow()` 與 `CreateCell()` 均回傳 `this`（分別是 `RowDefinition` 與 `GridTemplate`），可無限鏈式。`colSpan` 表示合併儲存格數量。

### RecordSetTemplate — 資料列表

```csharp
CellStyle centerStyle = SpreadsheetManager.DefaultCellStyles.FieldStyle with {
    HorizontalAlignment = HorizontalAlignment.Center
};

RecordSetTemplate<DatabaseColumnSchema> columnsTemplate = new(columns);
columnsTemplate.Columns
    .Add("欄位名稱",         x => x.ColumnName)
    .Add("欄位型別",         x => x.ColumnType)
    .Add("預設值",           x => x.ColumnDefault)
    .Add("是否允許 Null",    x => x.IsNullable,    fieldStyleGenerator: _ => centerStyle)
    .Add("是否為 PK",        x => x.IsPrimaryKey,  fieldStyleGenerator: _ => centerStyle)
    .Add("是否為 Identity",  x => x.IsIdentity,    fieldStyleGenerator: _ => centerStyle)
    .Add("描述",             x => x.ColumnDescription);

sheet.AddTemplate(columnsTemplate);
```

`fieldStyleGenerator` 接受 `Func<T, CellStyle>`，可依資料列動態決定儲存格樣式。

### 輸出 Excel 檔案

```csharp
using SpreadsheetDocument document = SpreadsheetManager.CreateDocument();
// ... 加入工作表與 Template ...
document.Save(outputFilePath);
```

`SpreadsheetDocument` 實作 `IDisposable`，務必在 `using` 區塊內使用。

---

## MinVer

### 概念

MinVer 是一個 .NET Source Generator 工具，從 Git 標籤自動推算組件版號，完全不需要手動維護版本號碼。每次建置時讀取最近的 Git 標籤並填入 `AssemblyVersion`、`AssemblyFileVersion`、`AssemblyInformationalVersion`。

### 設定

本專案的 `TagPrefix` 設定為 `v`，表示 MinVer 辨識 `v1.2.3` 格式的標籤：

```xml
<!-- SchemaExporter.Core.csproj / SchemaExporter.csproj -->
<PropertyGroup>
  <MinVerTagPrefix>v</MinVerTagPrefix>
</PropertyGroup>
```

### 版號推算規則

| Git 狀態 | 產生的版號 |
| --- | --- |
| 目前 commit 上有 `v1.2.3` 標籤 | `1.2.3` |
| 最近標籤 `v1.2.3`，往後 5 個 commit | `1.2.4-alpha.0.5` |
| 完全沒有標籤 | `0.0.0-alpha.0.N` |

預發布版號尾碼 `alpha.0.N` 中的 `N` 是距離最近標籤的 commit 數量。

### 發布流程

```bash
# 打上語意版號標籤並推送
git tag v1.0.0
git push origin v1.0.0

# 下次建置即自動使用 1.0.0 作為版號
dotnet build
```

### 取得目前版號

```csharp
string version = typeof(App).Assembly
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
    ?.InformationalVersion ?? "unknown";
```

---

## LibraryImport

### 概念

`[LibraryImport]` 是 .NET 7 引入的現代 P/Invoke 方式，用來在 .NET 呼叫 Windows 原生 API（或其他原生函式庫）。與舊的 `[DllImport]` 相比，它透過 **Source Generator** 在編譯期產生 marshaling 程式碼，而不是執行期反射，效能更好且完全支援 AOT 發布與 Trimming。

| 特性 | `[DllImport]`（舊） | `[LibraryImport]`（新） |
| --- | --- | --- |
| .NET 版本 | .NET Framework+ | .NET 7+ |
| 方法宣告 | `extern` | `partial` |
| 程式碼生成 | 執行期 IL | 編譯期 Source Generator |
| AOT / Trimming | 不支援 | 完整支援 |
| AllowUnsafeBlocks | 不需要 | **必須設定** |

### 本專案用法（CliConsoleSession.cs）

CLI 模式需要將主控台附加至父處理序（即呼叫 `SchemaExporter.exe` 的 PowerShell / cmd），使用兩個 Windows API：

```csharp
internal sealed partial class CliConsoleSession : IDisposable {
    // 為父處理序的主控台配置新主控台視窗
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AllocConsole();

    // 附加至指定處理序（-1 = 父處理序）的主控台
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AttachConsole(int processId);
}
```

- `partial` 關鍵字：讓 Source Generator 在同一個 `partial class` 產生實作程式碼。
- `SetLastError = true`：呼叫失敗時可用 `Marshal.GetLastWin32Error()` 取得錯誤碼。
- `[return: MarshalAs(UnmanagedType.Bool)]`：指定將 C 的 `BOOL`（32-bit int）正確轉換為 C# `bool`。

### 為何需要 AllowUnsafeBlocks

LibraryImport Source Generator 產生的程式碼包含 `unsafe` 區塊（用於高效 marshaling）。若專案未啟用 `AllowUnsafeBlocks`，`LibraryImportGenerator` 會在建置時產生 `SYSLIB1062` 錯誤。

```xml
<!-- SchemaExporter.csproj -->
<AllowUnsafeBlocks>true</AllowUnsafeBlocks>
```

> [!NOTE]
> `AllowUnsafeBlocks` 允許**整個專案**撰寫 `unsafe` 程式碼，但本專案只有 Source Generator 產生的部分使用，不應在業務邏輯中直接撰寫 `unsafe` 程式碼。

---

## LoggerMessage

### 概念

`[LoggerMessage]` 是 .NET 6 引入的 Source Generator，在編譯期產生高效能的日誌方法，避免執行期字串格式化、裝箱（boxing）和不必要的記憶體配置。

### 與傳統日誌的差異

```csharp
// ❌ 傳統寫法：每次都格式化字串，即使 Information 等級被過濾掉
logger.LogInformation("Export completed for {connectionName} in {elapsedMs} ms.", name, ms);

// ✅ [LoggerMessage]：等級不滿足時直接跳過，無字串格式化開銷
[LoggerMessage(EventId = 2001, Level = LogLevel.Information,
    Message = "Export completed for {connectionName} in {elapsedMs} ms.")]
private static partial void LogExportCompleted(ILogger logger, string connectionName, double elapsedMs);
```

### 本專案用法

`SchemaExportOrchestrator` 定義 4 個日誌方法涵蓋整個匯出生命週期：

```csharp
// 匯出開始
[LoggerMessage(EventId = 2000, Level = LogLevel.Information,
    Message = "Starting schema export for connection {connectionName} ({databaseType}) using profile {profileName}.")]
private static partial void LogExportStarted(
    ILogger logger, string connectionName, DatabaseType databaseType, string profileName);

// 匯出完成（含各階段耗時）
[LoggerMessage(EventId = 2001, Level = LogLevel.Information,
    Message = "Schema export completed for {connectionName} in {elapsedMilliseconds} ms. " +
              "Objects={objectCount}, Warnings={warningCount}, Output={outputFilePath}.")]
private static partial void LogExportCompleted(
    ILogger logger, string connectionName, double elapsedMilliseconds,
    int objectCount, /* ... */ string outputFilePath);

// 匯出被取消
[LoggerMessage(EventId = 2002, Level = LogLevel.Warning,
    Message = "Schema export for {connectionName} was cancelled during {stage} after {elapsedMilliseconds} ms.")]
private static partial void LogExportCancelled(
    ILogger logger, string connectionName, ExportStage stage, double elapsedMilliseconds);

// 匯出失敗
[LoggerMessage(EventId = 2003, Level = LogLevel.Error,
    Message = "Schema export failed for {connectionName} during {stage} after {elapsedMilliseconds} ms.")]
private static partial void LogExportFailed(
    ILogger logger, Exception exception, string connectionName,
    ExportStage stage, double elapsedMilliseconds, /* ... */);
```

`VelopackUpdateService` 也有一個日誌方法：

```csharp
[LoggerMessage(EventId = 3000, Level = LogLevel.Information,
    Message = "Velopack update check skipped because the app is not installed.")]
private static partial void LogUpdateCheckSkipped(ILogger logger);
```

### 使用規則

- 方法必須是 `static partial void`（或回傳 `bool`）。
- 類別必須是 `partial class`。
- `Exception` 參數（若有）必須放在 `ILogger` 之後的第一個位置。
- `EventId` 建議按模組分段管理（本專案：2000-2999 為 Core，3000-3999 為 App）。

---

## Dapper

### 概念

Dapper 是一個輕量級 Micro-ORM，直接在 `IDbConnection` 上提供擴充方法，讓 SQL 查詢結果自動對應至 C# 物件，不需要撰寫手動的 `DataReader` 迭代程式碼。

### 本專案用法

`OracleDatabaseSchemaProvider` 與 `SqlServerDatabaseSchemaProvider` 使用 Dapper 讀取資料庫 Schema：

```csharp
// 開啟連線並查詢
using IDbConnection connection = new SqlConnection(connectionString);

IReadOnlyList<DatabaseColumnSchema> columns = (await connection.QueryAsync<DatabaseColumnSchema>(
    ColumnQuery,
    new { SchemaName = schemaName, ObjectNames = objectNames }
).ConfigureAwait(false)).ToList().AsReadOnly();
```

- `QueryAsync<T>`：非同步查詢，結果直接對應至 `DatabaseColumnSchema` 屬性。
- 匿名物件作為參數（防 SQL Injection）。
- `ConfigureAwait(false)`：Library 層必須加，避免死結。

### Schema 對應規則

Dapper 預設以**大小寫不敏感的名稱**對應欄位與屬性，所以 SQL 查詢回傳的 `SCHEMA_NAME` 可對應至 C# 的 `SchemaName` 屬性。

---

## Testcontainers

### 概念

Testcontainers 是一個整合測試函式庫，在測試執行期間以程式碼啟動 Docker container，提供拋棄式的相依服務（如資料庫），測試結束後自動清理。相較於手動維護的共用測試資料庫，每次測試都從乾淨的 container 開始，不會殘留前次測試的狀態。

本專案在 `SchemaExporter.Core.IntegrationTests` 測試專案使用 `Testcontainers.MsSql` 與 `Testcontainers.Oracle` 兩個 provider 套件，分別驗證 SQL Server 與 Oracle provider 對實體資料庫的查詢行為。

### 本專案用法（SqlServerTestDatabase.cs）

每個 provider 的測試資料庫封裝為一個 `IAsyncDisposable` 類別，建立時啟動 container、套用 fixture schema，釋放時移除 container：

```csharp
internal sealed class SqlServerTestDatabase : IAsyncDisposable {
    private const string ImageName = "mcr.microsoft.com/mssql/server:2025-CU4-GDR1-ubuntu-24.04";
    private readonly MsSqlContainer container;

    public static async Task<SqlServerTestDatabase> CreateAsync() {
        MsSqlContainer container = new MsSqlBuilder(ImageName).Build();
        try {
            await container.StartAsync();
            // 建立 fixture 資料庫並套用 ProviderFixtures/sqlserver/schema.sql
            return new SqlServerTestDatabase(container, connectionString);
        } catch {
            await container.DisposeAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync() {
        await container.DisposeAsync();
    }
}
```

測試以 `await using` 取得資料庫，連線字串由 container 隨機分配的 host port 組成，避免與本機既有資料庫衝突：

```csharp
await using SqlServerTestDatabase database = await SqlServerTestDatabase.CreateAsync();
SqlServerDatabaseSchemaProvider sut = new();
IReadOnlyList<DatabaseObjectSchema> objects = await sut.LoadObjectsAsync(database.ConnectionString);
```

### Fixture schema 來源

Fixture schema scripts 位於 `tests/SchemaExporter.ProviderFixtures`，由測試專案以連結內容（linked content）複製到輸出目錄的 `ProviderFixtures` 子目錄：

```xml
<!-- SchemaExporter.Core.IntegrationTests.csproj -->
<Content Include="..\SchemaExporter.ProviderFixtures\sqlserver\schema.sql">
  <Link>ProviderFixtures\sqlserver\schema.sql</Link>
  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
</Content>
```

container 啟動後讀取此 script 並套用，建立可重現的測試物件。

### 與 Explicit 標記搭配

`ProviderIntegrationTests` 標記 `[Explicit]`，執行整個方案的一般測試時不會被觸發，必須以 `--filter` 明確選取：

```csharp
[TestFixture]
[Category(IntegrationTestCategories.Integration)]
[Explicit("Requires Docker to start provider fixture containers.")]
[NonParallelizable]
public sealed class ProviderIntegrationTests {
    // ...
}
```

這個設計讓沒有 Docker 環境（例如一般 CI 的單元測試階段）也能執行 `dotnet test`，integration tests 自動略過。執行方式詳見 [provider-fixtures.md](provider-fixtures.md)。

`[NonParallelizable]` 確保 SQL Server 與 Oracle 的 container 不會同時啟動，降低本機資源壓力。

---

## CLI 模式進入點

### 架構設計

本專案是一個**WPF 主程式**，透過啟動引數偵測決定進入 GUI 模式或 CLI 模式，**不是兩個獨立可執行檔**。CLI 程式碼位於 `src/SchemaExporter/Cli/`。

### 進入點判斷（App.xaml.cs）

```csharp
protected override async void OnStartup(StartupEventArgs e) {
    bool isCliMode = HasCliArguments(e.Args);

    if (isCliMode) {
        _ = CliConsoleSession.Attach(); // 附加主控台，初始化 UTF-8 編碼
    }

    // ... 初始化 DI ...

    if (isCliMode) {
        int exitCode = await serviceProvider
            .GetRequiredService<CliRunner>()
            .RunAsync(e.Args);
        Shutdown(exitCode);
        return;
    }

    // GUI 模式：顯示主視窗
}
```

### CLI 命令

| 命令 | 說明 |
| --- | --- |
| `export` | 連接資料庫並匯出 Schema 至 Excel 與附加產物 |
| `diff` | 比較兩個 Snapshot JSON，輸出差異報告 |

常用範例：

```powershell
# 匯出（使用 appsettings.json 中名為 "MyDb" 的連線）
schemaexporter export --connection MyDb

# 匯出並指定輸出目錄
schemaexporter export --connection MyDb --output C:\Output

# 匯出並同時產生 Snapshot、JSON sidecar 與 Schema Summary
schemaexporter export --connection MyDb --snapshot --json-sidecar --schema-summary

# 比較兩個 Snapshot
schemaexporter diff `
  --left C:\Snapshots\2024-01.json `
  --right C:\Snapshots\2024-02.json

# 輸出 diff 至 Markdown 檔案
schemaexporter diff `
  --left C:\Snapshots\2024-01.json `
  --right C:\Snapshots\2024-02.json `
  --output C:\Reports\diff.md --format markdown
```

### CliConsoleSession — 主控台附加

WPF 應用程式預設沒有主控台視窗。CLI 模式啟動時需要呼叫 Windows API 附加至父處理序的主控台（`AttachConsole(-1)`），讓 `Console.Write` 輸出能顯示在呼叫者的終端機。若附加失敗（例如父處理序沒有主控台），則呼叫 `AllocConsole()` 配置新的主控台視窗。

```csharp
public static CliConsoleSession Attach() {
    bool attached = AttachConsole(-1) || AllocConsole();
    if (attached) {
        // 重設輸入/輸出串流為 UTF-8，確保中文不亂碼
        Console.InputEncoding = Encoding.UTF8;
        Console.OutputEncoding = Encoding.UTF8;
        Console.SetOut(new StreamWriter(
            Console.OpenStandardOutput(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
        ) { AutoFlush = true });
    }
    return new CliConsoleSession(attached);
}
```

### 結束代碼

| 結束代碼 | 意義 |
| --- | --- |
| `0` | 成功 |
| `1` | 引數解析失敗（已印出使用說明） |
| `2` | 匯出流程錯誤（`ExportWorkflowException`） |
| `3` | 未預期例外 |
