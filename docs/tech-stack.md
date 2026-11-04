# 技術棧說明

本文件說明 SchemaExporter 所使用的特殊技術，包含各技術的概念介紹、在本專案中的用法，以及需要特別注意的細節。

---

## 目錄

- [Velopack — 自動更新框架](#velopack)
- [Blazor Hybrid — Desktop 介面](#blazor-hybrid)
- [CloudyWing.SpreadsheetExporter — Excel 匯出](#cloudywingspreadsheetexporter)
- [MinVer — 語意版號控制](#minver)
- [LoggerMessage — 高效能日誌](#loggermessage)
- [Dapper — 輕量 ORM](#dapper)
- [Testcontainers — 整合測試容器](#testcontainers)
- [CLI Console 主程式](#cli-console-主程式)

---

## Velopack

### 概念

Velopack 是一個 Windows 應用程式打包與自動更新框架，用來取代 Squirrel.Windows。它負責：

- 建立可安裝的 `.exe` 安裝檔。
- 在應用程式啟動時檢查並下載新版本。
- 在不中斷使用者操作的情況下更新並重啟。

### 初始化（必要步驟）

Desktop 主程式 `src/SchemaExporter.Desktop/App.xaml.cs` 建構式的**第一行**必須呼叫 `VelopackApp.Build().Run()`。這行程式碼處理安裝器、解除安裝器與更新鉤子事件，若未放在進入點最前端，更新流程會無法正確執行。CLI 主程式不參照 Velopack，也不呼叫此方法。

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
await updateService.DownloadUpdateAsync(
    update,
    new Progress<int>(percent => SetStatus($"正在下載更新... {percent}%"))
);
```

下載在背景非同步進行，`IProgress<int>` 傳入下載百分比讓 UI 能即時顯示進度。`Progress<T>` 在建立時擷取 Blazor Renderer 的同步內容，回報會投遞回 Renderer 執行緒；`SetStatus` 更新狀態後觸發 `Changed` 事件，由 Razor Component 以 `InvokeAsync(StateHasChanged)` 重新繪製。

#### 3. 套用並重啟

```csharp
updateService.ApplyUpdateAndRestart(update);
```

這個呼叫不會 `return`，應用程式會直接關閉並由新版重啟，因此後續程式碼不會被執行。

### 完整更新流程（UpdateWorkspace.cs）

`MainLayout.razor` 首次繪製後在背景呼叫 `UpdateWorkspace.CheckForUpdatesAsync()`，不阻擋畫面顯示。每次執行只檢查一次，確認對話框透過 `IDialogService` 顯示：

```csharp
public async Task CheckForUpdatesAsync() {
    if (Interlocked.Exchange(ref hasChecked, 1) == 1) {
        return;
    }

    try {
        UpdateInfo? update = await updateService.CheckForUpdatesAsync();
        if (update is null) {
            return;
        }

        if (!dialogService.Confirm(
            "有可用更新",
            $"偵測到新版本 {update.TargetFullRelease.Version}，是否立即下載並重新啟動套用更新？"
        )) {
            SetStatus($"有可用更新：{update.TargetFullRelease.Version}，下次啟動時會再次提示。");
            return;
        }

        // 確保尚未啟動過新版的使用者，在更新前先將安裝目錄設定遷移至使用者目錄。
        AppPaths.EnsureUserConfigExistsIfInstallConfigExists();

        await updateService.DownloadUpdateAsync(
            update,
            new Progress<int>(percent => SetStatus($"正在下載更新... {percent}%"))
        );

        dialogService.ShowInformation("準備更新", "更新已下載完成，應用程式將重新啟動以套用新版本。");
        updateService.ApplyUpdateAndRestart(update);
    } catch (Exception ex) {
        // 更新檢查是背景輔助功能，網路或安裝狀態異常都不應影響匯出操作。
        SetStatus($"更新檢查未完成：{ex.Message}");
    }
}
```

### 打包與發布

Velopack 套件同時包含 Desktop 與 CLI。Desktop 與 CLI 先分別 Publish 到不同資料夾，避免相依檔與 `appsettings.json` 互相覆寫，再組成以下 staging 目錄：

```text
artifacts/stage/
├── CloudyWing.SchemaExporter.exe      # Desktop（Velopack main exe）
├── appsettings.json
├── ...
└── tools/
    ├── CloudyWing.SchemaExporter.Cli.exe
    └── appsettings.json
```

`--packDir` 指向這個 staging 目錄，安裝後 CLI 位於安裝目錄的 `tools\` 子目錄。使用 Velopack CLI 打包：

```bash
# 安裝 Velopack CLI
dotnet tool install -g vpk

# 打包（從 staging 目錄建立安裝檔，tools/ 內的 CLI 一併打包）
vpk pack \
  --packId CloudyWing.SchemaExporter \
  --packVersion 1.0.0 \
  --packDir artifacts/stage/ \
  --mainExe CloudyWing.SchemaExporter.exe

# 上傳至 GitHub Releases（搭配 MinVer 版號）
vpk upload github \
  --repoUrl https://github.com/Owner/SchemaExporter \
  --token $GITHUB_TOKEN
```

實際的 Release workflow（`.github/workflows/release.yml`）以 `velopack-pack` action 完成相同步驟，另外將 CLI 發布結果壓縮為 `CloudyWing.SchemaExporter.Cli-<tag>-win-x64.zip` 一併上傳至 GitHub Release。

---

## Blazor Hybrid

### 概念

Blazor Hybrid 讓 Razor Component 在原生桌面應用程式內執行，畫面由內嵌的 WebView 呈現。元件直接在 .NET 處理序中執行，不經過 WebAssembly，也不需要本機 HTTP 伺服器，元件可以直接注入並呼叫一般的 .NET 服務。

本專案的 Desktop 主程式（`src/SchemaExporter.Desktop/`）以 WPF 視窗承載 `BlazorWebView`，套件為 `Microsoft.AspNetCore.Components.WebView.Wpf`，底層使用 WebView2 呈現畫面。

### 專案設定

Desktop 專案使用 `Microsoft.NET.Sdk.Razor` SDK，並同時啟用 WPF：

```xml
<!-- SchemaExporter.Desktop.csproj -->
<Project Sdk="Microsoft.NET.Sdk.Razor">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <!-- 沿用舊版主程式名稱，維持 Velopack 的 main-exe 與既有安裝的升級路徑。 -->
    <AssemblyName>CloudyWing.SchemaExporter</AssemblyName>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Components.WebView.Wpf" Version="10.0.120" />
  </ItemGroup>
</Project>
```

`AssemblyName` 維持 `CloudyWing.SchemaExporter`，Velopack 的 main exe 與 PackId 都不變，既有安裝可以直接升級。

### WPF 主視窗承載 BlazorWebView

`MainWindow.xaml` 只放一個 `BlazorWebView`，以 `wwwroot\index.html` 為 host page，並把 `Routes` 元件掛到 `#app`：

```xml
<blazor:BlazorWebView x:Name="BlazorWebView"
                      HostPage="wwwroot\index.html">
    <blazor:BlazorWebView.RootComponents>
        <blazor:RootComponent Selector="#app"
                              ComponentType="{x:Type components:Routes}" />
    </blazor:BlazorWebView.RootComponents>
</blazor:BlazorWebView>
```

`App.xaml.cs` 建立 DI 容器時註冊 `AddWpfBlazorWebView()`，`MainWindow` 再把同一個 `IServiceProvider` 指定給 `BlazorWebView.Services`，Razor Component 才能以 `@inject` 取得服務。開發者工具只在 DEBUG 組態註冊：

```csharp
services.AddWpfBlazorWebView();
#if DEBUG
services.AddBlazorWebViewDeveloperTools();
#endif
```

### Razor Component 結構

| 檔案 | 說明 |
| --- | --- |
| `Components/Routes.razor` | Router，預設版面為 `MainLayout`。 |
| `Components/Layout/MainLayout.razor` | 共用版面；首次繪製後在背景觸發更新檢查。 |
| `Components/Pages/Export.razor` | 匯出頁（`/`），包含連線、Export Profile、進階選項與診斷資訊。 |
| `Components/Pages/Settings.razor` | 設定頁，分為 Connections、Export Profiles 與 Redaction 三個分頁。 |

### State 類別與 Changed 事件

畫面狀態與流程協調放在 `State/` 下的 `ExportWorkspace`、`SettingsWorkspace` 與 `UpdateWorkspace`。這些類別不依賴 WPF 型別，也不依賴 Razor，以 Singleton 註冊，可以直接在 `SchemaExporter.Desktop.Tests` 中測試。

State 類別的公開方法由 Razor 事件在 Renderer 執行緒呼叫，內部 `await` 不加 `ConfigureAwait(false)`，讓後續的狀態變更都回到 Renderer 執行緒，避免繪製途中讀到另一個執行緒寫到一半的狀態。這與 Core、Infrastructure 等 Library 層一律加 `ConfigureAwait(false)` 的規則不同。

匯出進度使用 `Progress<T>`，回報會投遞回 Renderer 執行緒。投遞可能晚於匯出完成才執行，因此 `ExportWorkspace` 在匯出結束後忽略遲到的進度，避免蓋掉完成或取消訊息。

狀態變更時，State 類別觸發 `Changed` 事件；元件訂閱事件後以 `InvokeAsync(StateHasChanged)` 重新繪製。`InvokeAsync` 確保繪製在元件的同步內容執行：

```razor
@implements IDisposable
@inject ExportWorkspace Workspace

@code {
    protected override async Task OnInitializedAsync() {
        Workspace.Changed += HandleChanged;
        await Workspace.InitializeAsync();
    }

    public void Dispose() {
        Workspace.Changed -= HandleChanged;
    }

    private void HandleChanged() {
        _ = InvokeAsync(StateHasChanged);
    }
}
```

元件在 `Dispose` 時務必取消訂閱，否則 Singleton 的 State 類別會一直持有已卸載的元件。

### 原生對話框

WebView 內的頁面不直接呼叫 WPF API。訊息框與檔案選擇透過介面注入，由 WPF 實作：

| 介面 | 實作 | 用途 |
| --- | --- | --- |
| `IDialogService` | `WpfDialogService` | 以 `MessageBox` 顯示資訊、警告、錯誤與確認對話框。 |
| `IFileDialogService` | `WpfFileDialogService` | 以 WPF `OpenFolderDialog` / `OpenFileDialog` 選擇資料夾與檔案。 |

Blazor 事件的延續可能不在 UI 執行緒，`WpfDialogService` 透過 `Application.Current.Dispatcher.Invoke` 在 WPF Dispatcher 上開啟對話框。

### 外部網址封鎖

介面只顯示本機打包的資源。`MainWindow` 處理 `BlazorWebView.UrlLoading`，凡是不在 WebView 內開啟的網址都改為 `CancelLoad`，不在 WebView 內開啟，也不交給外部瀏覽器：

```csharp
private static void HandleUrlLoading(object? sender, UrlLoadingEventArgs e) {
    if (e.UrlLoadingStrategy != UrlLoadingStrategy.OpenInWebView) {
        e.UrlLoadingStrategy = UrlLoadingStrategy.CancelLoad;
    }
}
```

### 為何不使用本機 Web 伺服器

Blazor Server 或 ASP.NET Core 自架的方式需要在本機開啟 HTTP 連接埠，會帶來連接埠衝突、防火牆提示，以及同機其他處理序可以連入的風險。Blazor Hybrid 的元件與 .NET 服務在同一個處理序執行，WebView2 只負責呈現，不需要監聽任何連接埠，也能直接使用 WPF 的原生對話框與 Velopack 更新流程。

### 啟動引數

Desktop EXE 只提供 GUI。若帶任何命令列引數啟動，`App.OnStartup` 會直接以結束代碼 `1` 結束，不開啟視窗，避免舊版以 Desktop EXE 執行 CLI 的排程或 Agent Skill 開出無人操作的視窗。CLI 請改用 `tools\CloudyWing.SchemaExporter.Cli.exe`，詳見 [CLI Console 主程式](#cli-console-主程式)。

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

`SpreadsheetExporterBootstrapper.Configure()` 在 Desktop 的 `App.OnStartup` 與 CLI 的 `Program.cs` 啟動時各呼叫一次，使用 `lock` 確保執行緒安全的一次性初始化：

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
<!-- SchemaExporter.Core / Infrastructure / Cli / Desktop 各 csproj -->
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
- `EventId` 建議按模組分段管理（本專案：2000-2999 為 Core，3000-3999 為 Desktop）。

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

## CLI Console 主程式

### 架構設計

CLI 與 Desktop 是**兩個獨立可執行檔**：

| 專案 | 輸出 | 說明 |
| --- | --- | --- |
| `src/SchemaExporter.Desktop/` | `CloudyWing.SchemaExporter.exe` | Blazor Hybrid GUI，帶引數啟動時直接以代碼 `1` 結束。 |
| `src/SchemaExporter.Cli/` | `CloudyWing.SchemaExporter.Cli.exe` | net10.0 Console 應用程式，不參照 WPF、WebView2 與 Velopack。 |

兩者共用 `SchemaExporter.Core`（匯出引擎）與 `SchemaExporter.Infrastructure`（設定檔路徑、`ISettingsService`、`SchemaExporterConfiguration` 與 `ShellOutputFolderLauncher`）。出廠設定範本 `appsettings.json` 放在 Infrastructure 專案，建置時會複製到 Desktop 與 CLI 各自的輸出目錄。

CLI 是一般 Console 應用程式，主控台由作業系統直接配置，不需要舊版 WPF 單一 EXE 的 `AttachConsole` / `AllocConsole` P/Invoke。

### 進入點（Program.cs）

`Program.cs` 使用 top-level statements，依序設定 UTF-8 輸出、註冊 Ctrl+C 處理、建立 DI 容器，再交給 `CliRunner`：

```csharp
Console.OutputEncoding = new UTF8Encoding(false);

using CancellationTokenSource cancellation = new();
Console.CancelKeyPress += (_, eventArgs) => { /* 見下方 */ };

ServiceProvider serviceProvider;
try {
    SpreadsheetExporterBootstrapper.Configure();
    IConfiguration configuration = SchemaExporterConfiguration.Build(CliArguments.RequiresUserConfig(args));

    ServiceCollection services = new();
    // ... AddLogging（SimpleConsole）...
    services.AddSchemaExporterCore(configuration);
    services.AddSchemaExporterInfrastructure();
    services.AddSingleton<CliRunner>();
    serviceProvider = services.BuildServiceProvider();
} catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or InvalidDataException or JsonException) {
    // 沿用舊版單一 EXE 的啟動失敗代碼 1，避免既有 Agent Skill 與排程的判斷失準。
    Console.Error.WriteLine($"Startup failed: {ex.Message}");
    return 1;
}

await using (serviceProvider) {
    return await serviceProvider.GetRequiredService<CliRunner>().RunAsync(args, cancellation.Token);
}
```

`CliArguments.RequiresUserConfig(args)` 只有在引數解析成功且命令為 `export` 時回傳 `true`。`diff`、`comment-sql`、`--help` 與引數錯誤都不會讀取或建立使用者設定檔，設定檔缺漏或損毀時仍可執行。

### Ctrl+C 處理

第一次按 Ctrl+C 時，處理常式設定 `eventArgs.Cancel = true` 阻止系統直接終止，並取消 `CancellationTokenSource`，讓命令自行收尾。再按一次才交給系統直接終止：

```csharp
Console.CancelKeyPress += (_, eventArgs) => {
    if (cancellation.IsCancellationRequested) {
        return;
    }

    eventArgs.Cancel = true;
    cancellation.Cancel();
};
```

`CliRunner.RunAsync` 只在 `cancellationToken.IsCancellationRequested` 為 `true` 時把 `OperationCanceledException` 轉為結束代碼 `4`（`Canceled`）；內部逾時造成的取消仍視為未預期錯誤。

### CLI 命令

| 命令 | 說明 |
| --- | --- |
| `export` | 連接資料庫並匯出 Schema 至 Excel 與附加產物 |
| `diff` | 比較兩個 Snapshot JSON，輸出差異報告 |
| `comment-sql` | 依 snapshot 與註解 JSON 離線產生 comment SQL，不連線資料庫 |

常用範例：

```powershell
# 匯出（使用 appsettings.json 中名為 "MyDb" 的連線）
CloudyWing.SchemaExporter.Cli export --connection MyDb

# 匯出並指定輸出目錄
CloudyWing.SchemaExporter.Cli export --connection MyDb --output C:\Output

# 匯出並同時產生 Snapshot、JSON sidecar 與 Schema Summary
CloudyWing.SchemaExporter.Cli export --connection MyDb --snapshot --json-sidecar --schema-summary

# 比較兩個 Snapshot
CloudyWing.SchemaExporter.Cli diff `
  --left C:\Snapshots\2024-01.json `
  --right C:\Snapshots\2024-02.json

# 輸出 diff 至 Markdown 檔案
CloudyWing.SchemaExporter.Cli diff `
  --left C:\Snapshots\2024-01.json `
  --right C:\Snapshots\2024-02.json `
  --output C:\Reports\diff.md --format markdown
```

CLI 只有在明確指定 `--open-output-folder` 時才開啟輸出資料夾，不沿用設定檔的 `OpenOutputFolder`，避免無人值守的排程跳出檔案總管視窗。

### 開啟輸出資料夾

`SchemaExportOrchestrator` 只決定匯出完成後是否開啟資料夾，實際的 Shell 呼叫由 Host 注入的 `IOutputFolderLauncher` 負責。`AddSchemaExporterInfrastructure()` 註冊 `ShellOutputFolderLauncher`，以 `Process.Start`（`UseShellExecute = true`）開啟資料夾。若 Host 沒有提供 launcher，Orchestrator 只加入一筆 Warning 診斷「匯出完成，但目前執行環境未提供開啟資料夾功能。」，不中斷匯出。

### 結束代碼

| 結束代碼 | 意義 |
| --- | --- |
| `0` | 成功，或已顯示使用說明 |
| `1` | 引數解析失敗（已印出使用說明）；啟動失敗（例如設定檔損毀）也沿用此代碼 |
| `2` | 匯出或 diff 流程錯誤（`ExportWorkflowException`） |
| `3` | 未預期例外 |
| `4` | 使用者以 Ctrl+C 取消；已寫出的檔案可能不完整 |

### 單一檔案發布

Release workflow 將 CLI 發布為 win-x64 self-contained single-file：

```powershell
dotnet publish .\src\SchemaExporter.Cli\SchemaExporter.Cli.csproj `
  -c Release --self-contained -r win-x64 `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -o .\artifacts\publish\cli
```

輸出目錄包含 `CloudyWing.SchemaExporter.Cli.exe` 與出廠範本 `appsettings.json`。發布結果有兩種取得方式：

- 安裝版的 `tools\` 子目錄，例如 `%LocalAppData%\CloudyWing.SchemaExporter\current\tools\CloudyWing.SchemaExporter.Cli.exe`（推測：依 Velopack 預設安裝位置）。
- GitHub Release 的 `CloudyWing.SchemaExporter.Cli-<tag>-win-x64.zip`，解壓縮即可執行，不需要安裝 Desktop。

### 測試

`tests/SchemaExporter.Cli.Tests` 涵蓋 `CliArguments` 與 `CliRunner`。`CliProcessTests` 會以子處理序啟動實際的 CLI 組件，驗證結束代碼與主控台輸出。
