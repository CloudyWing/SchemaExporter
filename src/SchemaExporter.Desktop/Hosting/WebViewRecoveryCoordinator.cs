using System.Windows.Threading;
using Microsoft.Win32;

namespace CloudyWing.SchemaExporter.Desktop.Hosting;

/// <summary>
/// 協調 BlazorWebView 白屏的復原：監聽系統事件並以 debounce 排程重建，並在已知的 WebView2 圖形例外發生時吞下例外後排程重建。
/// </summary>
/// <remarks>
/// 除 <see cref="RequestRebuild"/> 可由任何執行緒呼叫外，其餘成員只能在建立本物件的 UI 執行緒呼叫。
/// </remarks>
internal sealed class WebViewRecoveryCoordinator : IDisposable {
    /// <summary>
    /// 觸發後到重建前的延遲時間，匯出進行中時每次延後也使用此間隔。
    /// </summary>
    internal static readonly TimeSpan RebuildDelay = TimeSpan.FromSeconds(3);

    private static readonly TimeSpan ImmediatePollInterval = TimeSpan.FromMilliseconds(50);

    private readonly Dispatcher dispatcher;
    private readonly WebViewRecoveryLog log;
    private readonly WebViewRebuildScheduler scheduler;
    private readonly WebViewExceptionPolicy exceptionPolicy;
    private readonly DispatcherTimer timer;
    private Func<Task>? rebuildAsync;
    private bool isStarted;
    private bool isStopped;

    /// <summary>
    /// 初始化 <see cref="WebViewRecoveryCoordinator"/> 類別的新執行個體。
    /// </summary>
    /// <param name="dispatcher">UI 執行緒的 Dispatcher。</param>
    /// <param name="log">復原流程的記錄檔。</param>
    /// <param name="isExporting">回傳目前是否正在匯出的函式，於 UI 執行緒呼叫。</param>
    /// <param name="timeProvider">提供單調時鐘的時間來源。</param>
    internal WebViewRecoveryCoordinator(
        Dispatcher dispatcher,
        WebViewRecoveryLog log,
        Func<bool> isExporting,
        TimeProvider timeProvider
    ) {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(isExporting);
        ArgumentNullException.ThrowIfNull(timeProvider);

        this.dispatcher = dispatcher;
        this.log = log;
        scheduler = new WebViewRebuildScheduler(timeProvider, RebuildDelay, isExporting);
        exceptionPolicy = new WebViewExceptionPolicy(timeProvider);
        timer = new DispatcherTimer(DispatcherPriority.Normal, dispatcher);
        timer.Tick += OnTimerTick;
    }

    /// <summary>
    /// 開始監聽系統事件。
    /// </summary>
    /// <param name="rebuildAsync">實際重建 BlazorWebView 的函式，於 UI 執行緒呼叫。</param>
    internal void Start(Func<Task> rebuildAsync) {
        ArgumentNullException.ThrowIfNull(rebuildAsync);

        if (isStarted || isStopped) {
            return;
        }

        this.rebuildAsync = rebuildAsync;
        isStarted = true;
        SystemEvents.SessionSwitch += HandleSessionSwitch;
        SystemEvents.PowerModeChanged += HandlePowerModeChanged;
        SystemEvents.DisplaySettingsChanged += HandleDisplaySettingsChanged;
    }

    /// <summary>
    /// 取消訂閱系統事件並停止計時器。可重複呼叫。
    /// </summary>
    internal void Stop() {
        if (isStopped) {
            return;
        }

        isStopped = true;
        if (isStarted) {
            SystemEvents.SessionSwitch -= HandleSessionSwitch;
            SystemEvents.PowerModeChanged -= HandlePowerModeChanged;
            SystemEvents.DisplaySettingsChanged -= HandleDisplaySettingsChanged;
        }

        timer.Stop();
        timer.Tick -= OnTimerTick;
    }

    /// <inheritdoc />
    public void Dispose() {
        Stop();
    }

    /// <summary>
    /// 要求重建 BlazorWebView。可由任何執行緒呼叫，實際排程一律切回 UI 執行緒。
    /// </summary>
    /// <param name="reason">觸發原因，會寫入記錄檔。</param>
    /// <param name="skipDelay">為 <see langword="true"/> 時略過延遲，匯出進行中仍會延後。</param>
    internal void RequestRebuild(string reason, bool skipDelay = false) {
        log.Write("Trigger", reason);
        dispatcher.BeginInvoke(new Action(() => Schedule(reason, skipDelay)));
    }

    /// <summary>
    /// 寫入一筆復原流程的記錄。可由任何執行緒呼叫。
    /// </summary>
    /// <param name="eventName">事件名稱，不含空白。</param>
    /// <param name="detail">細節。</param>
    internal void WriteLog(string eventName, string? detail = null) {
        log.Write(eventName, detail);
    }

    /// <summary>
    /// 通知第一次成功渲染已完成，此後才允許吞下已知的 WebView2 圖形例外。
    /// </summary>
    internal void MarkFirstRenderCompleted() {
        if (exceptionPolicy.MarkFirstRenderCompleted()) {
            log.Write("FirstRenderCompleted");
        }
    }

    /// <summary>
    /// 在 <see cref="System.Windows.Application.DispatcherUnhandledException"/> 中呼叫，判定是否吞下例外。
    /// </summary>
    /// <param name="exception">未處理的例外。</param>
    /// <returns>吞下例外並已排程重建時為 <see langword="true"/>；其餘為 <see langword="false"/>，讓程式照常處理。</returns>
    internal bool TryHandleUnhandledException(Exception exception) {
        ArgumentNullException.ThrowIfNull(exception);

        WebViewExceptionDecision decision = exceptionPolicy.Evaluate(exception);
        if (decision == WebViewExceptionDecision.NotMatched) {
            return false;
        }

        string description = Describe(exception);
        switch (decision) {
            case WebViewExceptionDecision.Swallow:
                log.Write("ExceptionSwallowed", description);
                Schedule("SwallowedException", skipDelay: false);
                return true;
            case WebViewExceptionDecision.OverLimit:
                log.Write("ExceptionSwallowLimitExceeded", description);
                return false;
            default:
                log.Write("ExceptionNotSwallowed", $"{decision} {description}");
                return false;
        }
    }

    private static string Describe(Exception exception) {
        return $"{exception.GetType().FullName}: {exception.Message}";
    }

    private void HandleSessionSwitch(object sender, SessionSwitchEventArgs e) {
        if (e.Reason == SessionSwitchReason.SessionUnlock) {
            RequestRebuild("SessionUnlock");
        }
    }

    private void HandlePowerModeChanged(object sender, PowerModeChangedEventArgs e) {
        if (e.Mode == PowerModes.Resume) {
            RequestRebuild("PowerResume");
        }
    }

    private void HandleDisplaySettingsChanged(object? sender, EventArgs e) {
        RequestRebuild("DisplaySettingsChanged");
    }

    private void Schedule(string reason, bool skipDelay) {
        if (isStopped) {
            return;
        }

        scheduler.Trigger(reason, skipDelay);
        if (!scheduler.IsRebuilding) {
            RestartTimer(skipDelay ? ImmediatePollInterval : RebuildDelay);
        }
    }

    private void RestartTimer(TimeSpan interval) {
        timer.Stop();
        timer.Interval = interval > ImmediatePollInterval ? interval : ImmediatePollInterval;
        timer.Start();
    }

    private async void OnTimerTick(object? sender, EventArgs e) {
        timer.Stop();
        if (isStopped || scheduler.IsRebuilding) {
            return;
        }

        RebuildPollResult result = scheduler.Poll();
        switch (result.Kind) {
            case RebuildPollKind.Waiting:
                RestartTimer(result.Wait);
                break;
            case RebuildPollKind.Deferred:
                if (result.IsFirstDeferral) {
                    log.Write("RebuildDeferred", $"匯出進行中，匯出結束後才重建；原因={result.Reasons}");
                }

                RestartTimer(result.Wait);
                break;
            case RebuildPollKind.Ready:
                await RebuildAsync(result.Reasons);
                break;
        }
    }

    private async Task RebuildAsync(string reasons) {
        log.Write("RebuildStarted", reasons);
        try {
            if (rebuildAsync is null) {
                throw new InvalidOperationException("尚未呼叫 Start。");
            }

            // 重建中旗標由排程器在重建全程持有，並於 finally 解除；期間的觸發只記為待處理。
            await scheduler.RunRebuildAsync(rebuildAsync);
            log.Write("RebuildCompleted");
        } catch (Exception ex) {
            // 重建是復原手段，失敗時只記錄，避免復原流程本身讓程式結束。
            log.Write("RebuildFailed", Describe(ex));
        }

        if (!isStopped && scheduler.IsPending) {
            RestartTimer(RebuildDelay);
        }
    }
}
