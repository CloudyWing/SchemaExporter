using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using CloudyWing.SchemaExporter.Core;
using CloudyWing.SchemaExporter.Core.Exporting;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Options;

namespace CloudyWing.SchemaExporter;

/// <summary>
/// 提供桌面介面使用的 schema 匯出作業協調器。
/// </summary>
public partial class ViewModel : ObservableObject {
    private readonly SchemaOptions schemaOptions;
    private readonly SchemaExportOrchestrator exportOrchestrator;
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
    public partial string OutputPath { get; set; } = "";

    /// <summary>
    /// 取得或設定一個值，用以指出是否正在執行匯出作業。
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelExportCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenOutputFolderCommand))]
    public partial bool IsExporting { get; set; }

    /// <summary>
    /// 取得或設定目前顯示的狀態訊息。
    /// </summary>
    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "請選擇連線並確認匯出設定。";

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
    /// 取得可用的連線設定集合。
    /// </summary>
    public ObservableCollection<SchemaConnection> Connections { get; }

    /// <summary>
    /// 取得可用的匯出設定檔集合。
    /// </summary>
    public ObservableCollection<ExportProfile> ExportProfiles { get; }

    /// <summary>
    /// 取得最近一次匯出作業的診斷資訊集合。
    /// </summary>
    public ObservableCollection<ExportDiagnostic> Diagnostics { get; } = [];

    /// <summary>
    /// 取得目前結果選項的摘要說明。
    /// </summary>
    public string ResultOptionsSummary {
        get {
            List<string> segments = [];
            if (schemaOptions.ExportResultOptions.UseTimestamp) {
                segments.Add($"檔名加上時間戳記（{schemaOptions.ExportResultOptions.TimestampFormat}）");
            }

            segments.Add($"檔案衝突處理：{GetOverwriteStrategyText(schemaOptions.ExportResultOptions.OverwriteStrategy)}");

            if (schemaOptions.ExportResultOptions.GenerateManifest) {
                segments.Add("產生 manifest");
            }

            if (schemaOptions.ExportResultOptions.GenerateJsonSidecar) {
                segments.Add("產生 JSON sidecar");
            }

            if (schemaOptions.ExportResultOptions.GenerateMarkdownSidecar) {
                segments.Add("產生 Markdown sidecar");
            }

            if (schemaOptions.ExportResultOptions.GenerateSchemaSnapshot) {
                segments.Add("產生 schema snapshot");
            }

            if (!string.IsNullOrWhiteSpace(schemaOptions.ExportResultOptions.DiffSourceSnapshotPath)) {
                segments.Add("比對既有 schema snapshot");
            }

            if (schemaOptions.ExportResultOptions.OpenOutputFolder) {
                segments.Add("完成後自動開啟輸出資料夾");
            }

            return segments.Count == 0
                ? "使用預設結果選項"
                : string.Join("、", segments);
        }
    }

    /// <summary>
    /// 初始化 <see cref="ViewModel"/> 類別的新執行個體。
    /// </summary>
    /// <param name="schemaAccessor">Schema 設定存取器。</param>
    /// <param name="exportOrchestrator">匯出流程協調器。</param>
    public ViewModel(
        IOptions<SchemaOptions> schemaAccessor,
        SchemaExportOrchestrator exportOrchestrator
    ) {
        ArgumentNullException.ThrowIfNull(schemaAccessor, nameof(schemaAccessor));
        ArgumentNullException.ThrowIfNull(exportOrchestrator, nameof(exportOrchestrator));

        schemaOptions = schemaAccessor.Value;
        this.exportOrchestrator = exportOrchestrator;

        ExportProfiles = new ObservableCollection<ExportProfile>(
            schemaOptions.ExportProfiles.Count > 0
                ? schemaOptions.ExportProfiles
                : [new ExportProfile { Name = "Default" }]
        );

        Connections = new ObservableCollection<SchemaConnection>(schemaOptions.Connections);
        OutputPath = schemaOptions.ExportPath;
        Connection = Connections.FirstOrDefault();
        SelectedProfile = ResolveConnectionProfile(Connection);
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

            ExportResult result = await exportOrchestrator.ExportAsync(
                Connection,
                OutputPath,
                SelectedProfile,
                schemaOptions.ExportResultOptions,
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
            if (!Path.IsPathFullyQualified(trimmedPath)) {
                throw new ExportValidationException($"匯出資料夾必須使用絕對路徑：{trimmedPath}");
            }

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

    partial void OnConnectionChanged(SchemaConnection? value) {
        SelectedProfile = ResolveConnectionProfile(value);
    }

    private ExportProfile ResolveConnectionProfile(SchemaConnection? connection) {
        ExportProfile fallbackProfile = ExportProfiles.First();

        if (connection is null || string.IsNullOrWhiteSpace(connection.ExportProfileName)) {
            return fallbackProfile;
        }

        ExportProfile? matchedProfile = ExportProfiles.FirstOrDefault(x =>
            string.Equals(x.Name, connection.ExportProfileName, StringComparison.OrdinalIgnoreCase)
        );

        if (matchedProfile is not null) {
            return matchedProfile;
        }

        if (!IsExporting) {
            StatusMessage = $"連線「{connection.Name}」指定的匯出設定檔不存在，已改用「{fallbackProfile.Name}」。";
        }

        return fallbackProfile;
    }

    private void UpdateProgress(ExportProgress progress) {
        StatusMessage = progress.Message;
        ProgressPercent = progress.PercentComplete ?? ProgressPercent;
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

    private static string GetOverwriteStrategyText(OverwriteStrategy overwriteStrategy) {
        return overwriteStrategy switch {
            OverwriteStrategy.Overwrite => "直接覆寫",
            OverwriteStrategy.AppendSuffix => "自動附加編號",
            OverwriteStrategy.Fail => "發現重複檔名時中止",
            _ => overwriteStrategy.ToString()
        };
    }
}

