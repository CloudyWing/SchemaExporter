using System.Globalization;

namespace CloudyWing.SchemaExporter.Desktop.Hosting;

/// <summary>
/// 釋放被替換下來的舊 BlazorWebView：先等待正常釋放，逾時或失敗時強制釋放，最後一律移除，不依賴 WPF 視覺樹。
/// </summary>
/// <remarks>
/// BlazorWebView 的正常釋放可能永遠不會完成，導致舊的 WebView2 程序殘留，因此以逾時保護，
/// 逾時後改由呼叫端提供的強制釋放動作直接釋放底層控制項。釋放過程的任何失敗都只記錄，不會擲出。
/// </remarks>
internal sealed class WebViewReleaser {
    private readonly TimeSpan disposeTimeout;
    private readonly TimeProvider timeProvider;
    private readonly Action<string, string?> log;

    /// <summary>
    /// 初始化 <see cref="WebViewReleaser"/> 類別的新執行個體。
    /// </summary>
    /// <param name="disposeTimeout">等待正常釋放完成的時間上限。</param>
    /// <param name="timeProvider">提供逾時計時的時間來源。</param>
    /// <param name="log">寫入復原記錄的函式，參數為事件名稱與細節。</param>
    internal WebViewReleaser(TimeSpan disposeTimeout, TimeProvider timeProvider, Action<string, string?> log) {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(log);

        this.disposeTimeout = disposeTimeout;
        this.timeProvider = timeProvider;
        this.log = log;
    }

    /// <summary>
    /// 釋放舊控制項。正常釋放在逾時內成功時只記錄成功；逾時或失敗時呼叫強制釋放動作；不論結果如何，最後都會呼叫移除動作。
    /// </summary>
    /// <param name="disposeAsync">正常釋放舊控制項的動作。</param>
    /// <param name="forceDispose">強制釋放舊控制項的動作，找不到可釋放的底層控制項時回傳 <see langword="false"/>。</param>
    /// <param name="remove">將舊控制項自視覺樹移除的動作。</param>
    /// <returns>表示釋放作業的工作；正常釋放與強制釋放的失敗都不會擲出。</returns>
    internal async Task ReleaseAsync(Func<ValueTask> disposeAsync, Func<bool> forceDispose, Action remove) {
        ArgumentNullException.ThrowIfNull(disposeAsync);
        ArgumentNullException.ThrowIfNull(forceDispose);
        ArgumentNullException.ThrowIfNull(remove);

        try {
            bool isDisposed = await TryDisposeAsync(disposeAsync);
            if (!isDisposed) {
                TryForceDispose(forceDispose);
            }
        } finally {
            remove();
        }
    }

    private async Task<bool> TryDisposeAsync(Func<ValueTask> disposeAsync) {
        Task? disposeTask = null;
        try {
            disposeTask = disposeAsync().AsTask();
            await disposeTask.WaitAsync(disposeTimeout, timeProvider);
            log("RebuildDisposed", null);
            return true;
        } catch (TimeoutException) when (disposeTask is not null && !disposeTask.IsCompleted) {
            log("DisposeTimedOut", $"{disposeTimeout.TotalSeconds.ToString(CultureInfo.InvariantCulture)}s");
            return false;
        } catch (Exception ex) {
            // 舊控制項的圖形資源可能已失效，釋放失敗只記錄，後續改走強制釋放。
            log("OldViewDisposeFailed", WebViewRecoveryLog.FormatException(ex));
            return false;
        }
    }

    private void TryForceDispose(Func<bool> forceDispose) {
        try {
            if (forceDispose()) {
                log("ForceDisposed", null);
            } else {
                log("ForceDisposeFailed", "control-not-found");
            }
        } catch (Exception ex) {
            // 強制釋放不重試，失敗只記錄。
            log("ForceDisposeFailed", WebViewRecoveryLog.FormatException(ex));
        }
    }
}
