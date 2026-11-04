using System.IO;
using CloudyWing.SchemaExporter.Core;
using CloudyWing.SchemaExporter.Core.Exporting;
using CloudyWing.SchemaExporter.Desktop.Services;
using CloudyWing.SchemaExporter.Infrastructure.Configuration;

namespace CloudyWing.SchemaExporter.Desktop.State;

/// <summary>
/// 保存匯出頁的選取、輸出選項與執行狀態，並協調 Core 匯出流程。
/// </summary>
/// <remarks>
/// 本類別不依賴 WPF 或 Razor，畫面以 <see cref="Changed"/> 事件重新繪製；原生對話框與 Shell 動作透過注入的服務完成。
/// 公開方法應由 Blazor Renderer 執行緒呼叫，內部 await 不使用 <c>ConfigureAwait(false)</c>，讓狀態變更都回到 Renderer 執行緒。
/// </remarks>
internal sealed class ExportWorkspace {
    private readonly ISettingsService settingsService;
    private readonly SchemaExportOrchestrator exportOrchestrator;
    private readonly SchemaExportRequestResolver requestResolver;
    private readonly IDialogService dialogService;
    private readonly IFileDialogService fileDialogService;
    private readonly IOutputFolderLauncher outputFolderLauncher;
    private SchemaOptions schemaOptions = new() { ExportPath = "" };
    private CancellationTokenSource? currentExportCancellation;
    private volatile bool isAcceptingProgress;
    private bool isInitialized;

    /// <summary>
    /// 初始化 <see cref="ExportWorkspace"/> 類別的新執行個體。
    /// </summary>
    /// <param name="settingsService">設定檔存取服務。</param>
    /// <param name="exportOrchestrator">匯出流程協調器。</param>
    /// <param name="requestResolver">匯出請求解析器。</param>
    /// <param name="dialogService">原生訊息對話框服務。</param>
    /// <param name="fileDialogService">原生檔案選擇對話框服務。</param>
    /// <param name="outputFolderLauncher">開啟輸出資料夾的 Shell 服務。</param>
    public ExportWorkspace(
        ISettingsService settingsService,
        SchemaExportOrchestrator exportOrchestrator,
        SchemaExportRequestResolver requestResolver,
        IDialogService dialogService,
        IFileDialogService fileDialogService,
        IOutputFolderLauncher outputFolderLauncher
    ) {
        ArgumentNullException.ThrowIfNull(settingsService);
        ArgumentNullException.ThrowIfNull(exportOrchestrator);
        ArgumentNullException.ThrowIfNull(requestResolver);
        ArgumentNullException.ThrowIfNull(dialogService);
        ArgumentNullException.ThrowIfNull(fileDialogService);
        ArgumentNullException.ThrowIfNull(outputFolderLauncher);

        this.settingsService = settingsService;
        this.exportOrchestrator = exportOrchestrator;
        this.requestResolver = requestResolver;
        this.dialogService = dialogService;
        this.fileDialogService = fileDialogService;
        this.outputFolderLauncher = outputFolderLauncher;
    }

    /// <summary>
    /// 在狀態變更、需要重新繪製畫面時觸發。
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// 取得可用的連線設定。
    /// </summary>
    public IReadOnlyList<SchemaConnection> Connections { get; private set; } = [];

    /// <summary>
    /// 取得可用的匯出設定檔；設定檔沒有任何 Profile 時提供一個不篩選的 <c>Default</c>。
    /// </summary>
    public IReadOnlyList<ExportProfile> ExportProfiles { get; private set; } = [];

    /// <summary>
    /// 取得目前選取的連線設定。
    /// </summary>
    public SchemaConnection? SelectedConnection { get; private set; }

    /// <summary>
    /// 取得目前選取的匯出設定檔。
    /// </summary>
    public ExportProfile? SelectedProfile { get; private set; }

    /// <summary>
    /// 取得或設定匯出資料夾路徑。
    /// </summary>
    public string OutputPath { get; set; } = "";

    /// <summary>
    /// 取得或設定一個值，用以指出是否產生 manifest 檔案。
    /// </summary>
    public bool GenerateManifest { get; set; }

    /// <summary>
    /// 取得或設定一個值，用以指出是否產生 JSON sidecar 檔案。
    /// </summary>
    public bool GenerateJsonSidecar { get; set; }

    /// <summary>
    /// 取得或設定一個值，用以指出是否產生 Markdown sidecar 檔案。
    /// </summary>
    public bool GenerateMarkdownSidecar { get; set; }

    /// <summary>
    /// 取得或設定一個值，用以指出是否產生 Schema Summary 檔案。
    /// </summary>
    public bool GenerateSchemaSummary { get; set; }

    /// <summary>
    /// 取得或設定一個值，用以指出是否產生 schema snapshot 檔案。
    /// </summary>
    public bool GenerateSchemaSnapshot { get; set; }

    /// <summary>
    /// 取得或設定差異比對使用的基準 snapshot 路徑；空白表示不比對。只對本次匯出有效，不寫入設定檔。
    /// </summary>
    public string? DiffSourceSnapshotPath { get; set; }

    /// <summary>
    /// 取得或設定一個值，用以指出是否在檔名中附加時間戳記。
    /// </summary>
    public bool UseTimestamp { get; set; }

    /// <summary>
    /// 取得或設定時間戳記格式。
    /// </summary>
    public string TimestampFormat { get; set; } = "yyyyMMdd_HHmmss";

    /// <summary>
    /// 取得或設定輸出檔案已存在時的處理策略。
    /// </summary>
    public OverwriteStrategy OverwriteStrategy { get; set; }

    /// <summary>
    /// 取得或設定一個值，用以指出是否在匯出完成後開啟輸出資料夾。
    /// </summary>
    public bool OpenOutputFolderAfterExport { get; set; }

    /// <summary>
    /// 取得一個值，用以指出是否正在執行匯出作業。
    /// </summary>
    public bool IsExporting { get; private set; }

    /// <summary>
    /// 取得一個值，用以指出是否已要求取消目前的匯出作業。
    /// </summary>
    public bool IsCancellationRequested { get; private set; }

    /// <summary>
    /// 取得目前進度百分比。
    /// </summary>
    public int ProgressPercent { get; private set; }

    /// <summary>
    /// 取得目前狀態訊息。
    /// </summary>
    public string StatusMessage { get; private set; } = "正在載入設定...";

    /// <summary>
    /// 取得目前狀態訊息的類型。
    /// </summary>
    public StatusKind StatusKind { get; private set; } = StatusKind.Info;

    /// <summary>
    /// 取得設定載入失敗的原因；<see langword="null"/> 表示設定已成功載入。
    /// </summary>
    public string? LoadError { get; private set; }

    /// <summary>
    /// 取得最近一次成功匯出的結果。
    /// </summary>
    public ExportResult? LastResult { get; private set; }

    /// <summary>
    /// 取得一個值，用以指出目前是否可以開始匯出。
    /// </summary>
    public bool CanExport => !IsExporting
        && LoadError is null
        && SelectedConnection is not null
        && SelectedProfile is not null
        && !string.IsNullOrWhiteSpace(OutputPath);

    /// <summary>
    /// 第一次顯示畫面時載入設定；之後的呼叫不重新載入，避免切換頁面時丟失尚未儲存的選項。
    /// </summary>
    /// <returns>代表非同步作業的工作。</returns>
    public async Task InitializeAsync() {
        if (isInitialized) {
            return;
        }

        isInitialized = true;
        await ReloadSettingsAsync();
    }

    /// <summary>
    /// 從使用者設定檔重新載入連線、設定檔與輸出選項；匯出期間呼叫時不做任何事。
    /// </summary>
    /// <returns>代表非同步作業的工作。</returns>
    public async Task ReloadSettingsAsync() {
        // 匯出期間不重新載入，避免執行中的選取與輸出選項被替換。
        if (IsExporting) {
            return;
        }

        try {
            schemaOptions = await settingsService.LoadAsync();
            LoadError = null;
        } catch (Exception ex) when (SettingsErrors.IsRecoverable(ex)) {
            LoadError = $"無法載入設定：{ex.Message}";
            Connections = [];
            ExportProfiles = [];
            SelectedConnection = null;
            SelectedProfile = null;
            SetStatus(LoadError, StatusKind.Error);
            return;
        }

        Connections = [.. schemaOptions.Connections];
        ExportProfiles = SchemaExportRequestResolver.GetAvailableProfiles(schemaOptions);

        ExportResultOptions resultOptions = schemaOptions.ExportResultOptions;
        OutputPath = schemaOptions.ExportPath;
        GenerateManifest = resultOptions.GenerateManifest;
        GenerateJsonSidecar = resultOptions.GenerateJsonSidecar;
        GenerateMarkdownSidecar = resultOptions.GenerateMarkdownSidecar;
        GenerateSchemaSummary = resultOptions.GenerateSchemaSummary;
        GenerateSchemaSnapshot = resultOptions.GenerateSchemaSnapshot;
        DiffSourceSnapshotPath = null;
        UseTimestamp = resultOptions.UseTimestamp;
        TimestampFormat = resultOptions.TimestampFormat;
        OverwriteStrategy = resultOptions.OverwriteStrategy;
        OpenOutputFolderAfterExport = resultOptions.OpenOutputFolder;

        SelectedConnection = ResolveConnection(schemaOptions.LastSelectedConnectionName);
        string? profileMessage = null;
        SelectedProfile = ResolveProfile(schemaOptions.LastSelectedProfileName, SelectedConnection, ref profileMessage);
        if (profileMessage is not null) {
            SetStatus(profileMessage, StatusKind.Warning);
            return;
        }

        SetStatus(
            Connections.Count == 0 ? "請先在「設定」建立連線。" : "請選擇連線並確認匯出設定。",
            StatusKind.Info
        );
    }

    /// <summary>
    /// 選取連線，並依連線指定的設定檔更新設定檔選取。
    /// </summary>
    /// <param name="connectionName">連線名稱。</param>
    public void SelectConnection(string? connectionName) {
        if (IsExporting) {
            return;
        }

        SelectedConnection = ResolveConnection(connectionName);
        string? profileMessage = null;
        // 切換連線時以新連線的預設設定檔為準，與 CLI 未指定 --profile 時的解析一致。
        SelectedProfile = ResolveProfile(null, SelectedConnection, ref profileMessage);
        if (profileMessage is not null) {
            SetStatus(profileMessage, StatusKind.Warning);
            return;
        }

        NotifyChanged();
    }

    /// <summary>
    /// 選取匯出設定檔。
    /// </summary>
    /// <param name="profileName">設定檔名稱。</param>
    public void SelectProfile(string? profileName) {
        if (IsExporting) {
            return;
        }

        SelectedProfile = ExportProfiles.FirstOrDefault(x =>
            string.Equals(x.Name, profileName, StringComparison.OrdinalIgnoreCase)
        ) ?? SelectedProfile;
        NotifyChanged();
    }

    /// <summary>
    /// 開啟資料夾選擇對話框並更新匯出資料夾。
    /// </summary>
    public void BrowseOutputPath() {
        string? selectedPath = fileDialogService.PickFolder("選擇匯出資料夾", OutputPath);
        if (selectedPath is null) {
            return;
        }

        OutputPath = selectedPath;
        NotifyChanged();
    }

    /// <summary>
    /// 開啟檔案選擇對話框並更新基準 snapshot 路徑。
    /// </summary>
    public void BrowseDiffSourceSnapshot() {
        string? selectedPath = fileDialogService.PickFile(
            "選擇基準 Snapshot 檔案",
            "Schema Snapshot (*.snapshot.json)|*.snapshot.json|All Files (*.*)|*.*",
            string.IsNullOrWhiteSpace(DiffSourceSnapshotPath) ? OutputPath : DiffSourceSnapshotPath
        );
        if (selectedPath is null) {
            return;
        }

        DiffSourceSnapshotPath = selectedPath;
        NotifyChanged();
    }

    /// <summary>
    /// 執行匯出作業。驗證、連線與輸出錯誤以對話框與狀態訊息回報，不向呼叫端擲回。
    /// </summary>
    /// <returns>代表非同步作業的工作。</returns>
    public async Task ExportAsync() {
        if (IsExporting) {
            return;
        }

        if (SelectedConnection is null) {
            ShowValidationWarning("請先選擇連線設定。");
            return;
        }

        if (SelectedProfile is null) {
            ShowValidationWarning("請先選擇匯出設定檔。");
            return;
        }

        using CancellationTokenSource cancellation = new();
        currentExportCancellation = cancellation;
        IsExporting = true;
        IsCancellationRequested = false;
        ProgressPercent = 0;
        LastResult = null;
        SetStatus("正在準備匯出...", StatusKind.Info);
        string? connectionStringForRedaction = null;

        try {
            ExportOptionOverrides overrides = new() {
                OutputPath = OutputPath,
                OpenOutputFolder = OpenOutputFolderAfterExport,
                GenerateManifest = GenerateManifest,
                GenerateJsonSidecar = GenerateJsonSidecar,
                GenerateMarkdownSidecar = GenerateMarkdownSidecar,
                GenerateSchemaSummary = GenerateSchemaSummary,
                GenerateSchemaSnapshot = GenerateSchemaSnapshot,
                UseTimestamp = UseTimestamp,
                TimestampFormat = TimestampFormat,
                OverwriteStrategy = OverwriteStrategy,
                DiffSourceSnapshotPath = DiffSourceSnapshotPath,
                OverrideDiffSourceSnapshotPath = true
            };
            SchemaExportRequest request = requestResolver.Resolve(
                schemaOptions,
                SelectedConnection.Name,
                SelectedProfile.Name,
                overrides
            );
            connectionStringForRedaction = request.Connection.ConnectionString;

            ExportResult result;
            isAcceptingProgress = true;
            try {
                result = await exportOrchestrator.ExportAsync(
                    request,
                    new Progress<ExportProgress>(UpdateProgress),
                    cancellation.Token
                );
            } finally {
                isAcceptingProgress = false;
            }

            LastResult = result;
            ProgressPercent = 100;
            int warningCount = result.Diagnostics.Count(x => x.Severity == DiagnosticSeverity.Warning);
            int errorCount = result.Diagnostics.Count(x => x.Severity == DiagnosticSeverity.Error);
            if (errorCount > 0 || warningCount > 0) {
                List<string> problems = [];
                if (errorCount > 0) {
                    problems.Add($"{errorCount} 個錯誤");
                }

                if (warningCount > 0) {
                    problems.Add($"{warningCount} 個警告");
                }

                SetStatus($"匯出完成，但有 {string.Join("、", problems)}，請確認診斷資訊。", StatusKind.Warning);
            } else {
                SetStatus("匯出完成。", StatusKind.Success);
            }
        } catch (OperationCanceledException) when (cancellation.IsCancellationRequested) {
            SetStatus("匯出已取消，輸出資料夾中可能留有未完成的檔案。", StatusKind.Warning);
        } catch (ExportValidationException ex) {
            SetStatus(ex.Message, StatusKind.Warning);
            dialogService.ShowWarning("匯出驗證", ex.Message);
        } catch (ExportConnectionException ex) {
            string message = SchemaExportOrchestrator.RedactException(ex, connectionStringForRedaction).Message;
            SetStatus(message, StatusKind.Error);
            dialogService.ShowError("連線錯誤", message);
        } catch (ExportOutputException ex) {
            string message = SchemaExportOrchestrator.RedactException(ex, connectionStringForRedaction).Message;
            SetStatus(message, StatusKind.Error);
            dialogService.ShowError("輸出錯誤", message);
        } catch (Exception ex) {
            // 桌面程式的最外層互動入口：任何未預期例外都要回到可操作狀態，訊息先遮罩連線字串再顯示。
            string message = SchemaExportOrchestrator.RedactException(ex, connectionStringForRedaction).Message;
            SetStatus("匯出時發生未預期的錯誤。", StatusKind.Error);
            dialogService.ShowError("未預期錯誤", $"匯出時發生未預期的錯誤：{message}");
        } finally {
            currentExportCancellation = null;
            IsExporting = false;
            IsCancellationRequested = false;
            NotifyChanged();
        }
    }

    /// <summary>
    /// 要求取消目前的匯出作業。
    /// </summary>
    public void CancelExport() {
        CancellationTokenSource? cancellation = currentExportCancellation;
        if (!IsExporting || cancellation is null || IsCancellationRequested) {
            return;
        }

        IsCancellationRequested = true;
        // 先顯示「正在取消」再取消：在 Renderer 同步內容下，匯出的收尾可能在 Cancel() 內同步完成，
        // 若之後才設定狀態，會蓋掉「匯出已取消」。
        SetStatus("正在取消匯出作業...", StatusKind.Info);
        try {
            cancellation.Cancel();
        } catch (ObjectDisposedException) {
            // 匯出剛好在按下取消時結束，CancellationTokenSource 已釋放，不需再取消。
        }
    }

    /// <summary>
    /// 開啟目前輸入的匯出資料夾，資料夾不存在時先建立。
    /// </summary>
    public void OpenOutputFolder() {
        if (string.IsNullOrWhiteSpace(OutputPath)) {
            dialogService.ShowWarning("輸出資料夾", "請先輸入匯出資料夾路徑。");
            return;
        }

        try {
            string normalizedPath = Path.GetFullPath(OutputPath.Trim());
            Directory.CreateDirectory(normalizedPath);
            outputFolderLauncher.Open(normalizedPath);
        } catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException or System.ComponentModel.Win32Exception) {
            dialogService.ShowError("輸出資料夾", $"無法開啟輸出資料夾：{ex.Message}");
        }
    }

    /// <summary>
    /// 開啟最近一次匯出結果所在的資料夾。
    /// </summary>
    public void OpenLastResultFolder() {
        if (LastResult is null || string.IsNullOrWhiteSpace(LastResult.OutputDirectoryPath)) {
            return;
        }

        try {
            outputFolderLauncher.Open(LastResult.OutputDirectoryPath);
        } catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) {
            dialogService.ShowError("輸出資料夾", $"無法開啟輸出資料夾：{ex.Message}");
        }
    }

    /// <summary>
    /// 將目前的匯出資料夾、輸出選項與選取項目儲存為預設值。連線、設定檔與 Redaction 以設定檔最新內容為準，不會被覆寫。
    /// </summary>
    /// <returns>儲存成功時回傳 <see langword="true"/>；否則回傳 <see langword="false"/>。</returns>
    public async Task<bool> SaveDefaultsAsync() {
        if (IsExporting) {
            return false;
        }

        if (string.IsNullOrWhiteSpace(OutputPath)) {
            dialogService.ShowWarning("儲存預設值", "請先輸入匯出資料夾路徑。");
            return false;
        }

        try {
            // 重新讀取最新設定，避免覆蓋設定頁在本頁載入後才儲存的連線與設定檔。
            SchemaOptions latestOptions = await settingsService.ReadAsync();
            SchemaOptions optionsToSave = new() {
                ExportPath = OutputPath.Trim(),
                Connections = latestOptions.Connections,
                ExportProfiles = latestOptions.ExportProfiles,
                Redaction = latestOptions.Redaction,
                ExportResultOptions = new ExportResultOptions {
                    UseTimestamp = UseTimestamp,
                    TimestampFormat = string.IsNullOrWhiteSpace(TimestampFormat)
                        ? latestOptions.ExportResultOptions.TimestampFormat
                        : TimestampFormat.Trim(),
                    OverwriteStrategy = OverwriteStrategy,
                    OpenOutputFolder = OpenOutputFolderAfterExport,
                    GenerateManifest = GenerateManifest,
                    GenerateJsonSidecar = GenerateJsonSidecar,
                    GenerateMarkdownSidecar = GenerateMarkdownSidecar,
                    GenerateSchemaSummary = GenerateSchemaSummary,
                    GenerateSchemaSnapshot = GenerateSchemaSnapshot,
                    // 基準 snapshot 只對單次匯出有意義，保留設定檔原值而不寫入畫面暫存的路徑。
                    DiffSourceSnapshotPath = latestOptions.ExportResultOptions.DiffSourceSnapshotPath
                },
                LastSelectedConnectionName = SelectedConnection?.Name,
                LastSelectedProfileName = SelectedProfile?.Name
            };

            await settingsService.SaveAsync(optionsToSave);
            schemaOptions = optionsToSave;
            SetStatus("已將目前選項儲存為預設值。", StatusKind.Success);
            return true;
        } catch (ExportValidationException ex) {
            dialogService.ShowWarning("儲存預設值", ex.Message);
        } catch (Exception ex) when (SettingsErrors.IsRecoverable(ex)) {
            dialogService.ShowError("儲存預設值", $"無法儲存設定：{ex.Message}");
        }

        return false;
    }

    private void ShowValidationWarning(string message) {
        SetStatus(message, StatusKind.Warning);
        dialogService.ShowWarning("匯出驗證", message);
    }

    private SchemaConnection? ResolveConnection(string? connectionName) {
        return SchemaExportRequestResolver.FindConnection(schemaOptions, connectionName) ?? Connections.FirstOrDefault();
    }

    private ExportProfile? ResolveProfile(string? requestedProfileName, SchemaConnection? connection, ref string? warningMessage) {
        ExportProfile? fallbackProfile = ExportProfiles.FirstOrDefault();
        string? profileName = SchemaExportRequestResolver.SelectProfileName(requestedProfileName, connection);
        if (fallbackProfile is null || string.IsNullOrWhiteSpace(profileName)) {
            return fallbackProfile;
        }

        ExportProfile? matchedProfile = SchemaExportRequestResolver.FindProfile(ExportProfiles, profileName);
        if (matchedProfile is not null) {
            return matchedProfile;
        }

        // CLI 找不到設定檔時直接失敗；畫面改用第一筆並提示，讓使用者仍可操作。
        warningMessage = $"匯出設定檔「{profileName}」不存在，已改用「{fallbackProfile.Name}」。";
        return fallbackProfile;
    }

    private void UpdateProgress(ExportProgress progress) {
        // Progress<T> 以投遞方式回報，匯出結束後才送達的進度不可蓋掉完成或取消訊息。
        if (!isAcceptingProgress) {
            return;
        }

        StatusMessage = progress.Message;
        StatusKind = StatusKind.Info;
        ProgressPercent = progress.PercentComplete ?? ProgressPercent;
        NotifyChanged();
    }

    private void SetStatus(string message, StatusKind kind) {
        StatusMessage = message;
        StatusKind = kind;
        NotifyChanged();
    }

    private void NotifyChanged() {
        Changed?.Invoke();
    }
}
