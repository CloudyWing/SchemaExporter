using Microsoft.Extensions.Logging;
using Velopack;
using Velopack.Sources;

namespace CloudyWing.SchemaExporter.Services;

/// <summary>
/// 以 Velopack 實作 <see cref="IUpdateService"/> 的應用程式更新服務，使用 GitHub 作為更新來源。
/// </summary>
internal sealed partial class VelopackUpdateService : IUpdateService {
    private const string GitHubRepository = "https://github.com/CloudyWing/SchemaExporter";
    private readonly ILogger<VelopackUpdateService> logger;
    private readonly UpdateManager updateManager;

    /// <summary>
    /// 初始化 <see cref="VelopackUpdateService"/> 類別的新執行個體，並設定 GitHub 更新來源。
    /// </summary>
    /// <param name="logger">用於記錄更新相關訊息的記錄器。</param>
    public VelopackUpdateService(ILogger<VelopackUpdateService> logger) {
        ArgumentNullException.ThrowIfNull(logger);

        this.logger = logger;
        updateManager = new UpdateManager(new GithubSource(GitHubRepository, accessToken: null, prerelease: false));
    }

    /// <inheritdoc/>
    public async Task<UpdateInfo?> CheckForUpdatesAsync() {
        if (!updateManager.IsInstalled) {
            LogUpdateCheckSkipped(logger);
            return null;
        }

        return await updateManager.CheckForUpdatesAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task DownloadUpdateAsync(
        UpdateInfo update,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default
    ) {
        ArgumentNullException.ThrowIfNull(update);

        if (!updateManager.IsInstalled) {
            throw new InvalidOperationException("目前不是以安裝版本執行，無法下載更新。");
        }

        await updateManager.DownloadUpdatesAsync(
            update,
            progress is null ? null : progress.Report,
            cancellationToken
        ).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void ApplyUpdateAndRestart(UpdateInfo update) {
        ArgumentNullException.ThrowIfNull(update);

        if (!updateManager.IsInstalled) {
            throw new InvalidOperationException("目前不是以安裝版本執行，無法套用更新。");
        }

        updateManager.ApplyUpdatesAndRestart(update.TargetFullRelease);
    }

    [LoggerMessage(EventId = 3000, Level = LogLevel.Information, Message = "Velopack update check skipped because the app is not installed.")]
    private static partial void LogUpdateCheckSkipped(ILogger logger);
}
