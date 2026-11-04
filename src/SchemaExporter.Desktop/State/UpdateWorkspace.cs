using CloudyWing.SchemaExporter.Desktop.Services;
using CloudyWing.SchemaExporter.Infrastructure.Configuration;
using Velopack;

namespace CloudyWing.SchemaExporter.Desktop.State;

/// <summary>
/// 協調 Desktop 啟動後的更新檢查、下載與套用流程。
/// </summary>
internal sealed class UpdateWorkspace {
    private readonly IUpdateService updateService;
    private readonly IDialogService dialogService;
    private int hasChecked;

    /// <summary>
    /// 初始化 <see cref="UpdateWorkspace"/> 類別的新執行個體。
    /// </summary>
    /// <param name="updateService">應用程式更新服務。</param>
    /// <param name="dialogService">原生訊息對話框服務。</param>
    public UpdateWorkspace(IUpdateService updateService, IDialogService dialogService) {
        ArgumentNullException.ThrowIfNull(updateService);
        ArgumentNullException.ThrowIfNull(dialogService);

        this.updateService = updateService;
        this.dialogService = dialogService;
    }

    /// <summary>
    /// 在狀態變更、需要重新繪製畫面時觸發；可能由非 UI 執行緒觸發。
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// 取得目前的更新狀態訊息；<see langword="null"/> 表示沒有需要顯示的訊息。
    /// </summary>
    public string? StatusMessage { get; private set; }

    /// <summary>
    /// 檢查更新；有新版本時詢問使用者，同意後下載並重新啟動。每次執行只檢查一次，失敗只顯示狀態訊息，不中斷操作。
    /// </summary>
    /// <returns>代表非同步作業的工作。</returns>
    public async Task CheckForUpdatesAsync() {
        if (Interlocked.Exchange(ref hasChecked, 1) == 1) {
            return;
        }

        try {
            UpdateInfo? update = await updateService.CheckForUpdatesAsync().ConfigureAwait(false);
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
                new InlineProgress<int>(percent => SetStatus($"正在下載更新... {percent}%"))
            ).ConfigureAwait(false);

            dialogService.ShowInformation("準備更新", "更新已下載完成，應用程式將重新啟動以套用新版本。");
            updateService.ApplyUpdateAndRestart(update);
        } catch (Exception ex) {
            // 更新檢查是背景輔助功能，網路或安裝狀態異常都不應影響匯出操作。
            SetStatus($"更新檢查未完成：{ex.Message}");
        }
    }

    private void SetStatus(string? message) {
        StatusMessage = message;
        Changed?.Invoke();
    }
}
