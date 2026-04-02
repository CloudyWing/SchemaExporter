using Velopack;

namespace CloudyWing.SchemaExporter.Services;

/// <summary>
/// 定義應用程式更新的檢查、下載與套用作業。
/// </summary>
internal interface IUpdateService {
    /// <summary>
    /// 非同步檢查是否有可用的應用程式更新。
    /// </summary>
    /// <returns>若有可用更新則回傳 <see cref="UpdateInfo"/>；否則回傳 <see langword="null"/>。</returns>
    Task<UpdateInfo?> CheckForUpdatesAsync();

    /// <summary>
    /// 非同步下載指定的應用程式更新套件。
    /// </summary>
    /// <param name="update">要下載的更新資訊。</param>
    /// <param name="progress">用於回報下載進度（百分比）的進度提供者，可為 <see langword="null"/>。</param>
    /// <param name="cancellationToken">可用於取消作業的取消權杖。</param>
    /// <returns>代表非同步作業的工作。</returns>
    Task DownloadUpdateAsync(
        UpdateInfo update,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 套用已下載的更新並重新啟動應用程式。
    /// </summary>
    /// <param name="update">要套用的更新資訊。</param>
    void ApplyUpdateAndRestart(UpdateInfo update);
}
