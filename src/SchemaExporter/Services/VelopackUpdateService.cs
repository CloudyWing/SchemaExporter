using Microsoft.Extensions.Logging;
using Velopack;
using Velopack.Sources;

namespace CloudyWing.SchemaExporter.Services;

internal sealed partial class VelopackUpdateService : IUpdateService {
    private const string GitHubRepository = "https://github.com/CloudyWing/SchemaExporter";
    private readonly ILogger<VelopackUpdateService> logger;
    private readonly UpdateManager updateManager;

    public VelopackUpdateService(ILogger<VelopackUpdateService> logger) {
        ArgumentNullException.ThrowIfNull(logger, nameof(logger));
        this.logger = logger;
        updateManager = new UpdateManager(new GithubSource(GitHubRepository, accessToken: null, prerelease: false));
    }

    public async Task<UpdateInfo?> CheckForUpdatesAsync(CancellationToken cancellationToken = default) {
        if (!updateManager.IsInstalled) {
            LogUpdateCheckSkipped(logger);
            return null;
        }

        return await updateManager.CheckForUpdatesAsync().ConfigureAwait(false);
    }

    public async Task DownloadUpdateAsync(
        UpdateInfo update,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default
    ) {
        ArgumentNullException.ThrowIfNull(update, nameof(update));

        if (!updateManager.IsInstalled) {
            throw new InvalidOperationException("目前不是以安裝版本執行，無法下載更新。");
        }

        await updateManager.DownloadUpdatesAsync(
            update,
            progress is null ? null : progress.Report,
            cancellationToken
        ).ConfigureAwait(false);
    }

    public void ApplyUpdateAndRestart(UpdateInfo update) {
        ArgumentNullException.ThrowIfNull(update, nameof(update));

        if (!updateManager.IsInstalled) {
            throw new InvalidOperationException("目前不是以安裝版本執行，無法套用更新。");
        }

        updateManager.ApplyUpdatesAndRestart(update.TargetFullRelease);
    }

    [LoggerMessage(EventId = 3000, Level = LogLevel.Information, Message = "Velopack update check skipped because the app is not installed.")]
    private static partial void LogUpdateCheckSkipped(ILogger logger);
}
