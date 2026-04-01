using Velopack;

namespace CloudyWing.SchemaExporter.Services;

internal interface IUpdateService {
    Task<UpdateInfo?> CheckForUpdatesAsync(CancellationToken cancellationToken = default);

    Task DownloadUpdateAsync(
        UpdateInfo update,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default
    );

    void ApplyUpdateAndRestart(UpdateInfo update);
}
