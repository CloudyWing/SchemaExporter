using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CloudyWing.SchemaExporter.Desktop.Hosting;

/// <summary>
/// 在 WebView 重建後把新控制項導回重建前的路由，整段還原有單一絕對期限，不依賴 WPF 視覺樹。
/// </summary>
/// <remarks>
/// 新控制項的 renderer 尚未就緒時派送會回傳 <see langword="false"/>，需要輪詢；renderer 卡住時派送本身可能永遠不完成，
/// 因此每次派送都有等待上限，整段還原也有期限，避免重建中旗標永遠無法解除。
/// 逾時後原本的派送仍可能稍後在 renderer 上執行，委派在導覽前會再確認還原工作尚未結束且目標仍有效。
/// </remarks>
internal sealed class WebViewRouteRestorer {
    /// <summary>
    /// 整段還原的絕對期限，於還原開始時起算，不會因重試而重設。
    /// </summary>
    internal static readonly TimeSpan RestoreDeadline = TimeSpan.FromSeconds(10);

    /// <summary>
    /// 單次派送的等待上限，實際等待為此值與剩餘期限中較小者。
    /// </summary>
    internal static readonly TimeSpan AttemptTimeout = TimeSpan.FromSeconds(1);

    /// <summary>
    /// 兩次嘗試之間的間隔。
    /// </summary>
    internal static readonly TimeSpan RetryInterval = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// 最多嘗試的次數。
    /// </summary>
    internal const int MaxAttempts = 50;

    private readonly TimeProvider timeProvider;
    private readonly Action<string, string?> log;

    /// <summary>
    /// 初始化 <see cref="WebViewRouteRestorer"/> 類別的新執行個體。
    /// </summary>
    /// <param name="timeProvider">提供期限、逾時與間隔計時的時間來源。</param>
    /// <param name="log">寫入復原記錄的函式，參數為事件名稱與細節。</param>
    internal WebViewRouteRestorer(TimeProvider timeProvider, Action<string, string?> log) {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(log);

        this.timeProvider = timeProvider;
        this.log = log;
    }

    /// <summary>
    /// 反覆派送導覽工作到新控制項，直到成功、目標失效、用完次數或超過期限；方法一定會返回，失敗只記錄不擲出。
    /// 目標失效時記錄 <c>RouteRestoreSkipped</c> 後結束，不視為失敗。
    /// </summary>
    /// <param name="route">要還原的 base 相對路由。</param>
    /// <param name="dispatchAsync">把工作派送到新控制項 renderer 的函式，控制項尚未就緒時回傳 <see langword="false"/>。</param>
    /// <param name="isTargetValid">回傳目標控制項是否仍有效（仍是目前控制項且視窗不在關閉流程中）的函式。</param>
    /// <returns>表示還原作業的工作。</returns>
    internal async Task RestoreAsync(
        string route,
        Func<Action<IServiceProvider>, Task<bool>> dispatchAsync,
        Func<bool> isTargetValid
    ) {
        ArgumentException.ThrowIfNullOrEmpty(route);
        ArgumentNullException.ThrowIfNull(dispatchAsync);
        ArgumentNullException.ThrowIfNull(isTargetValid);

        // 狀態只屬於這一次還原，晚到的委派透過它得知還原已結束。
        RestoreState state = new();
        long startTimestamp = timeProvider.GetTimestamp();
        try {
            for (int attempt = 0; attempt < MaxAttempts; attempt++) {
                // 逾時的派送可能在上一次嘗試之後、這次嘗試之前才執行並完成導覽，視為還原成功。
                if (state.HasNavigated) {
                    log("RouteRestored", route);
                    return;
                }

                if (!isTargetValid()) {
                    log("RouteRestoreSkipped", "target-invalid");
                    return;
                }

                TimeSpan remaining = RestoreDeadline - timeProvider.GetElapsedTime(startTimestamp);
                if (remaining <= TimeSpan.Zero) {
                    log("RouteRestoreFailed", "deadline-exceeded");
                    return;
                }

                TimeSpan wait = remaining < AttemptTimeout ? remaining : AttemptTimeout;
                Task<bool>? dispatchTask = null;
                try {
                    dispatchTask = dispatchAsync(services => state.TryNavigate(services, route, isTargetValid));
                    await dispatchTask.WaitAsync(wait, timeProvider);
                } catch (TimeoutException) when (dispatchTask is not null && !dispatchTask.IsCompleted) {
                    // 該次派送逾時視為失敗，間隔後重試；逾時的委派之後仍可能執行，由 RestoreState 防護。
                } catch (Exception ex) {
                    log("RouteRestoreFailed", WebViewRecoveryLog.FormatException(ex));
                    return;
                }

                // 無論該次派送成功、回傳 false 或逾時，只要委派已經導覽就不再重試。
                if (state.HasNavigated) {
                    log("RouteRestored", route);
                    return;
                }

                // 重試間隔也受期限約束，剩餘時間不足一個間隔時只等剩餘時間，確保總時長不超過期限。
                TimeSpan remainingBeforeDelay = RestoreDeadline - timeProvider.GetElapsedTime(startTimestamp);
                if (remainingBeforeDelay <= TimeSpan.Zero) {
                    log("RouteRestoreFailed", "deadline-exceeded");
                    return;
                }

                await Task.Delay(remainingBeforeDelay < RetryInterval ? remainingBeforeDelay : RetryInterval, timeProvider);
            }

            if (state.HasNavigated) {
                log("RouteRestored", route);
                return;
            }

            log("RouteRestoreFailed", $"attempts-exhausted:{MaxAttempts.ToString(CultureInfo.InvariantCulture)}");
        } finally {
            state.Finish();
        }
    }

    /// <summary>
    /// 單次還原專屬的狀態，供逾時後才執行的委派判斷還原是否已結束或已導覽。
    /// </summary>
    /// <remarks>
    /// 還原迴圈在 UI 執行緒執行，委派則在 WebView renderer 的 Dispatcher 執行，兩者通常不在同一執行緒，
    /// 因此旗標以 <see cref="Volatile"/> 與 <see cref="Interlocked"/> 存取，不使用 lock，也不持有其他共用狀態。
    /// </remarks>
    private sealed class RestoreState {
        private int isFinished;
        private int hasNavigated;

        internal bool HasNavigated => Volatile.Read(ref hasNavigated) != 0;

        internal void Finish() {
            Volatile.Write(ref isFinished, 1);
        }

        internal void TryNavigate(IServiceProvider services, string route, Func<bool> isTargetValid) {
            // 逾時的派送可能在還原結束後才於 renderer 執行，此時不能再導覽。
            if (Volatile.Read(ref isFinished) != 0 || !isTargetValid()) {
                return;
            }

            // 逾時的派送與重試的派送可能先後執行，同一次還原只導覽一次；旗標在導覽前就設定，避免重入時導覽兩次。
            if (Interlocked.Exchange(ref hasNavigated, 1) != 0) {
                return;
            }

            services.GetRequiredService<NavigationManager>().NavigateTo(route);
        }
    }
}
