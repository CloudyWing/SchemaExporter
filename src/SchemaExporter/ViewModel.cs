using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using CloudyWing.SchemaExporter.Core;
using CloudyWing.SchemaExporter.Core.Exporting;
using CloudyWing.SchemaExporter.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace CloudyWing.SchemaExporter;

/// <summary>
/// 提供桌面介面使用的 schema 匯出作業協調器。
/// </summary>
public partial class ViewModel : ObservableObject {
    private readonly ISettingsService settingsService;
    private readonly SchemaExportOrchestrator exportOrchestrator;
    private readonly SchemaExportRequestResolver requestResolver;
    private SchemaOptions schemaOptions = new() { ExportPath = "" };
    private CancellationTokenSource? currentExportCancellation;

    /// <summary>
    /// 取得或設定目前選取的連線設定。
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
    public partial SchemaConnection? Connection { get; set; }

    /// <summary>
    /// 取得或設定目前選取的匯出設定檔。
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
    public partial ExportProfile? SelectedProfile { get; set; }

    /// <summary>
    /// 取得或設定匯出資料夾路徑。
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenOutputFolderCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveSettingsCommand))]
    public partial string OutputPath { get; set; } = "";

    /// <summary>
    /// 取得或設定一個值，用以指出是否正在執行匯出作業。
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelExportCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenOutputFolderCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveSettingsCommand))]
    public partial bool IsExporting { get; set; }

    /// <summary>
    /// 取得或設定目前顯示的狀態訊息。
    /// </summary>
    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "請先選擇連線並確認匯出設定。";

    /// <summary>
    /// 取得或設定目前進度百分比。
    /// </summary>
    [ObservableProperty]
    public partial int ProgressPercent { get; set; }

    /// <summary>
    /// 取得或設定最近一次輸出的活頁簿檔案路徑。
    /// </summary>
    [ObservableProperty]
    public partial string? LastOutputFilePath { get; set; }

    /// <summary>
    /// 取得或設定最近一次產生的 manifest 檔案路徑。
    /// </summary>
    [ObservableProperty]
    public partial string? LastManifestFilePath { get; set; }

    /// <summary>
    /// 取得或設定是否產生 manifest 檔案。
    /// </summary>
    [ObservableProperty]
    public partial bool GenerateManifest { get; set; }

    /// <summary>
    /// 取得或設定是否產生 JSON sidecar 檔案。
    /// </summary>
    [ObservableProperty]
    public partial bool GenerateJsonSidecar { get; set; }

    /// <summary>
    /// 取得或設定是否產生 Markdown sidecar 檔案。
    /// </summary>
    [ObservableProperty]
    public partial bool GenerateMarkdownSidecar { get; set; }

    /// <summary>
    /// 取得或設定是否產生 AI context 檔案。
    /// </summary>
    [ObservableProperty]
    public partial bool GenerateAiContext { get; set; }

    /// <summary>
    /// 取得或設定是否產生 schema snapshot 檔案。
    /// </summary>
    [ObservableProperty]
    public partial bool GenerateSchemaSnapshot { get; set; }

    /// <summary>
    /// 取得或設定差異比對使用的基準 snapshot 路徑。
    /// </summary>
    [ObservableProperty]
    public partial string? DiffSourceSnapshotPath { get; set; }

    /// <summary>
    /// 取得或設定是否在檔名中附加時間戳記。
    /// </summary>
    [ObservableProperty]
    public partial bool UseTimestamp { get; set; }

    /// <summary>
    /// 取得或設定是否在匯出完成後開啟輸出資料夾。
    /// </summary>
    [ObservableProperty]
    public partial bool AutoOpenOutputFolder { get; set; }

    /// <summary>
    /// 取得或設定診斷區塊是否展開。
    /// </summary>
    [ObservableProperty]
    public partial bool IsDiagnosticsExpanded { get; set; }

    /// <summary>
    /// 取得可用的連線設定集合。
    /// </summary>
    public ObservableCollection<SchemaConnection> Connections { get; } = [];

    /// <summary>
    /// 取得可用的匯出設定檔集合。
    /// </summary>
    public ObservableCollection<ExportProfile> ExportProfiles { get; } = [];

    /// <summary>
    /// 取得最近一次匯出作業的診斷資訊集合。
    /// </summary>
    public ObservableCollection<ExportDiagnostic> Diagnostics { get; } = [];

    /// <summary>
    /// 初始化 <see cref="ViewModel"/> 類別的新執行個體。
    /// </summary>
    /// <param name="settingsService">設定檔存取服務。</param>
    /// <param name="exportOrchestrator">匯出流程協調器。</param>
    internal ViewModel(
        ISettingsService settingsService,
        SchemaExportOrchestrator exportOrchestrator,
        SchemaExportRequestResolver requestResolver
    ) {
        ArgumentNullException.ThrowIfNull(settingsService);
        ArgumentNullException.ThrowIfNull(exportOrchestrator);
        ArgumentNullException.ThrowIfNull(requestResolver);
        this.settingsService = settingsService;
        this.exportOrchestrator = exportOrchestrator;
        this.requestResolver = requestResolver;
    }

    /// <summary>
    /// 以目前設定初始化畫面狀態。
    /// </summary>
    public Task InitializeAsync() {
        return ReloadSettingsAsync();
    }

    /// <summary>
    /// 從 appsettings.json 重新載入設定並更新畫面。
    /// </summary>
    public async Task ReloadSettingsAsync() {
        schemaOptions = await settingsService.LoadAsync();

        ReplaceCollection(Connections, schemaOptions.Connections);
        ReplaceCollection(
            ExportProfiles,
            schemaOptions.ExportProfiles.Count > 0
                ? schemaOptions.ExportProfiles
                : [
                    new ExportProfile {
                        Name = "Default"
                    }
                ]
        );

        OutputPath = schemaOptions.ExportPath;
        GenerateManifest = schemaOptions.ExportResultOptions.GenerateManifest;
        GenerateJsonSidecar = schemaOptions.ExportResultOptions.GenerateJsonSidecar;
        GenerateMarkdownSidecar = schemaOptions.ExportResultOptions.GenerateMarkdownSidecar;
        GenerateAiContext = schemaOptions.ExportResultOptions.GenerateAiContext;
        GenerateSchemaSnapshot = schemaOptions.ExportResultOptions.GenerateSchemaSnapshot;
        DiffSourceSnapshotPath = null;
        UseTimestamp = schemaOptions.ExportResultOptions.UseTimestamp;
        AutoOpenOutputFolder = schemaOptions.ExportResultOptions.OpenOutputFolder;

        Connection = ResolveConnection(schemaOptions.LastSelectedConnectionName);
        SelectedProfile = ResolveProfile(schemaOptions.LastSelectedProfileName, Connection);
        StatusMessage = Connections.Count == 0
            ? "請先在設定中建立連線。"
            : "請選擇連線並確認匯出設定。";
    }

    [RelayCommand(CanExecute = nameof(CanSaveSettings))]
    private async Task SaveSettingsAsync() {
        SchemaOptions optionsToSave = new() {
            ExportPath = OutputPath.Trim(),
            Connections = schemaOptions.Connections,
            ExportProfiles = schemaOptions.ExportProfiles,
            ExportResultOptions = new ExportResultOptions {
                UseTimestamp = UseTimestamp,
                TimestampFormat = schemaOptions.ExportResultOptions.TimestampFormat,
                OverwriteStrategy = schemaOptions.ExportResultOptions.OverwriteStrategy,
                OpenOutputFolder = AutoOpenOutputFolder,
                GenerateManifest = GenerateManifest,
                GenerateJsonSidecar = GenerateJsonSidecar,
                GenerateMarkdownSidecar = GenerateMarkdownSidecar,
                GenerateAiContext = GenerateAiContext,
                GenerateSchemaSnapshot = GenerateSchemaSnapshot,
                DiffSourceSnapshotPath = null
            },
            LastSelectedConnectionName = Connection?.Name,
            LastSelectedProfileName = SelectedProfile?.Name
        };

        try {
            await settingsService.SaveAsync(optionsToSave);
            StatusMessage = "設定已儲存。";
        } catch (ExportValidationException ex) {
            MessageBox.Show(ex.Message, "儲存設定", MessageBoxButton.OK, MessageBoxImage.Warning);
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            MessageBox.Show(
                $"無法儲存設定：{ex.Message}",
                "儲存設定",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
    }

    private bool CanSaveSettings() {
        return !IsExporting && !string.IsNullOrWhiteSpace(OutputPath);
    }

    [RelayCommand(CanExecute = nameof(CanSubmit))]
    private async Task SubmitAsync() {
        if (Connection is null) {
            MessageBox.Show("請先選擇連線設定。", "匯出驗證", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (SelectedProfile is null) {
            MessageBox.Show("請先選擇匯出設定檔。", "匯出驗證", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsExporting = true;
        ProgressPercent = 0;
        StatusMessage = "正在準備匯出...";
        LastOutputFilePath = null;
        LastManifestFilePath = null;
        Diagnostics.Clear();
        currentExportCancellation = new CancellationTokenSource();

        try {
            Progress<ExportProgress> progress = new(UpdateProgress);

            ExportOptionOverrides overrides = new() {
                OutputPath = OutputPath,
                OpenOutputFolder = AutoOpenOutputFolder,
                GenerateManifest = GenerateManifest,
                GenerateJsonSidecar = GenerateJsonSidecar,
                GenerateMarkdownSidecar = GenerateMarkdownSidecar,
                GenerateAiContext = GenerateAiContext,
                GenerateSchemaSnapshot = GenerateSchemaSnapshot,
                UseTimestamp = UseTimestamp,
                DiffSourceSnapshotPath = DiffSourceSnapshotPath,
                OverrideDiffSourceSnapshotPath = true
            };
            SchemaExportRequest request = requestResolver.Resolve(
                schemaOptions,
                Connection.Name,
                SelectedProfile.Name,
                overrides
            );

            ExportResult result = await exportOrchestrator.ExportAsync(
                request,
                progress,
                currentExportCancellation.Token
            );

            LastOutputFilePath = result.OutputFilePath;
            LastManifestFilePath = result.ManifestFilePath;

            foreach (ExportDiagnostic diagnostic in result.Diagnostics) {
                Diagnostics.Add(diagnostic);
            }

            int warningCount = result.Diagnostics.Count(x => x.Severity == DiagnosticSeverity.Warning);
            StatusMessage = warningCount > 0
                ? $"匯出完成，但有 {warningCount} 個警告，請確認下方診斷資訊。"
                : "匯出完成。";
            ProgressPercent = 100;
            IsDiagnosticsExpanded = result.Diagnostics.Count > 0;

            ShowExportSuccessDialog(result);
        } catch (OperationCanceledException) {
            StatusMessage = "匯出已取消。";
            MessageBox.Show("匯出作業已取消。", "匯出取消", MessageBoxButton.OK, MessageBoxImage.Information);
        } catch (ExportValidationException ex) {
            StatusMessage = ex.Message;
            MessageBox.Show(ex.Message, "匯出驗證", MessageBoxButton.OK, MessageBoxImage.Warning);
        } catch (ExportConnectionException ex) {
            StatusMessage = ex.Message;
            MessageBox.Show(ex.Message, "連線錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
        } catch (ExportOutputException ex) {
            StatusMessage = ex.Message;
            MessageBox.Show(ex.Message, "輸出錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
        } catch (Exception ex) {
            StatusMessage = "匯出時發生未預期的錯誤。";
            MessageBox.Show(
                $"匯出時發生未預期的錯誤：{ex.Message}",
                "未預期錯誤",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        } finally {
            IsExporting = false;
            currentExportCancellation?.Dispose();
            currentExportCancellation = null;
        }
    }

    private bool CanSubmit() {
        return !IsExporting
            && Connection is not null
            && SelectedProfile is not null
            && !string.IsNullOrWhiteSpace(OutputPath);
    }

    [RelayCommand(CanExecute = nameof(CanCancelExport))]
    private void CancelExport() {
        currentExportCancellation?.Cancel();
        StatusMessage = "正在取消匯出作業...";
    }

    private bool CanCancelExport() {
        return IsExporting && currentExportCancellation is not null;
    }

    [RelayCommand(CanExecute = nameof(CanOpenOutputFolder))]
    private void OpenOutputFolder() {
        try {
            if (string.IsNullOrWhiteSpace(OutputPath)) {
                throw new ExportValidationException("請先輸入匯出資料夾路徑。");
            }

            string trimmedPath = OutputPath.Trim();
            string normalizedPath = Path.GetFullPath(trimmedPath);
            Directory.CreateDirectory(normalizedPath);

            Process.Start(new ProcessStartInfo {
                FileName = normalizedPath,
                UseShellExecute = true
            });
        } catch (ExportValidationException ex) {
            MessageBox.Show(ex.Message, "輸出資料夾", MessageBoxButton.OK, MessageBoxImage.Warning);
        } catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException) {
            MessageBox.Show(
                $"無法開啟輸出資料夾：{ex.Message}",
                "輸出資料夾",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
    }

    private bool CanOpenOutputFolder() {
        return !IsExporting && !string.IsNullOrWhiteSpace(OutputPath);
    }

    [RelayCommand]
    private void BrowseDiffSourceSnapshot() {
        OpenFileDialog dialog = new() {
            Title = "選擇基準 Snapshot 檔案",
            Filter = "Schema Snapshot (*.snapshot.json)|*.snapshot.json|All Files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() == true) {
            DiffSourceSnapshotPath = dialog.FileName;
        }
    }

    partial void OnConnectionChanged(SchemaConnection? value) {
        SelectedProfile = ResolveProfile(SelectedProfile?.Name, value);
    }

    private SchemaConnection? ResolveConnection(string? connectionName) {
        if (!string.IsNullOrWhiteSpace(connectionName)) {
            SchemaConnection? matchedConnection = Connections.FirstOrDefault(x =>
                string.Equals(x.Name, connectionName, StringComparison.OrdinalIgnoreCase)
            );
            if (matchedConnection is not null) {
                return matchedConnection;
            }
        }

        return Connections.FirstOrDefault();
    }

    private ExportProfile ResolveProfile(string? requestedProfileName, SchemaConnection? connection) {
        ExportProfile fallbackProfile = ExportProfiles.First();
        string? profileName = !string.IsNullOrWhiteSpace(requestedProfileName)
            ? requestedProfileName
            : connection?.ExportProfileName;

        if (string.IsNullOrWhiteSpace(profileName)) {
            return fallbackProfile;
        }

        ExportProfile? matchedProfile = ExportProfiles.FirstOrDefault(x =>
            string.Equals(x.Name, profileName, StringComparison.OrdinalIgnoreCase)
        );

        if (matchedProfile is not null) {
            return matchedProfile;
        }

        if (!IsExporting) {
            StatusMessage = $"連線「{connection?.Name}」指定的匯出設定檔不存在，已改用「{fallbackProfile.Name}」。";
        }

        return fallbackProfile;
    }

    private void UpdateProgress(ExportProgress progress) {
        StatusMessage = progress.Message;
        ProgressPercent = progress.PercentComplete ?? ProgressPercent;
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> target, IEnumerable<T> values) {
        target.Clear();
        foreach (T value in values) {
            target.Add(value);
        }
    }

    private static void ShowExportSuccessDialog(ExportResult result) {
        int warningCount = result.Diagnostics.Count(x => x.Severity == DiagnosticSeverity.Warning);
        int infoCount = result.Diagnostics.Count(x => x.Severity == DiagnosticSeverity.Info);

        StringBuilder messageBuilder = new();
        messageBuilder.AppendLine("檔案已成功匯出：");
        messageBuilder.AppendLine(result.OutputFilePath);

        if (!string.IsNullOrWhiteSpace(result.ManifestFilePath)) {
            messageBuilder.AppendLine();
            messageBuilder.AppendLine("Manifest：");
            messageBuilder.AppendLine(result.ManifestFilePath);
        }

        if (!string.IsNullOrWhiteSpace(result.JsonSidecarFilePath)) {
            messageBuilder.AppendLine();
            messageBuilder.AppendLine("JSON Sidecar：");
            messageBuilder.AppendLine(result.JsonSidecarFilePath);
        }

        if (!string.IsNullOrWhiteSpace(result.MarkdownSidecarFilePath)) {
            messageBuilder.AppendLine();
            messageBuilder.AppendLine("Markdown Sidecar：");
            messageBuilder.AppendLine(result.MarkdownSidecarFilePath);
        }

        if (!string.IsNullOrWhiteSpace(result.AiContextFilePath)) {
            messageBuilder.AppendLine();
            messageBuilder.AppendLine("AI Context：");
            messageBuilder.AppendLine(result.AiContextFilePath);
        }

        if (!string.IsNullOrWhiteSpace(result.SnapshotFilePath)) {
            messageBuilder.AppendLine();
            messageBuilder.AppendLine("Schema Snapshot：");
            messageBuilder.AppendLine(result.SnapshotFilePath);
        }

        if (!string.IsNullOrWhiteSpace(result.DiffFilePath)) {
            messageBuilder.AppendLine();
            messageBuilder.AppendLine("Schema Diff：");
            messageBuilder.AppendLine(result.DiffFilePath);
        }

        if (warningCount > 0 || infoCount > 0) {
            messageBuilder.AppendLine();
            messageBuilder.AppendLine($"診斷資訊：{warningCount} 個警告、{infoCount} 個資訊");
        }

        MessageBox.Show(messageBuilder.ToString(), "匯出成功", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
