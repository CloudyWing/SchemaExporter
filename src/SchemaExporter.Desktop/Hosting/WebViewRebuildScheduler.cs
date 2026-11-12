namespace CloudyWing.SchemaExporter.Desktop.Hosting;

/// <summary>
/// 決定何時重建 BlazorWebView 的 debounce 與延後狀態機，不依賴 WPF 與計時器，由呼叫端定期輪詢。
/// </summary>
/// <remarks>
/// 每次觸發以最後一次觸發的時間重新起算延遲；延遲到期時若匯出進行中則再等待一段延遲後重新檢查。
/// 重建進行中（<see cref="IsRebuilding"/>）收到的觸發只記為待處理，不會被 <see cref="Poll"/> 取出，重建結束後才回到 debounce 流程。
/// 本類別只能在 UI 執行緒使用，不具執行緒安全。
/// </remarks>
internal sealed class WebViewRebuildScheduler {
    private readonly TimeProvider timeProvider;
    private readonly TimeSpan delay;
    private readonly Func<bool> isExporting;
    private readonly List<string> reasons = [];
    private long lastTriggerTimestamp;
    private bool isImmediate;
    private bool isDeferralReported;

    /// <summary>
    /// 初始化 <see cref="WebViewRebuildScheduler"/> 類別的新執行個體。
    /// </summary>
    /// <param name="timeProvider">提供單調時鐘的時間來源。</param>
    /// <param name="delay">觸發後到重建前的延遲時間。</param>
    /// <param name="isExporting">回傳目前是否正在匯出的函式。</param>
    internal WebViewRebuildScheduler(TimeProvider timeProvider, TimeSpan delay, Func<bool> isExporting) {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(isExporting);

        this.timeProvider = timeProvider;
        this.delay = delay;
        this.isExporting = isExporting;
    }

    /// <summary>
    /// 取得一個值，用以指出是否有待處理的重建。
    /// </summary>
    internal bool IsPending { get; private set; }

    /// <summary>
    /// 取得一個值，用以指出是否有重建正在執行。旗標只在 <see cref="RunRebuildAsync"/> 內設定與解除。
    /// </summary>
    internal bool IsRebuilding { get; private set; }

    /// <summary>
    /// 登記一次重建觸發，並以此刻重新起算延遲。
    /// </summary>
    /// <param name="reason">觸發原因。</param>
    /// <param name="skipDelay">為 <see langword="true"/> 時略過延遲，匯出進行中仍會延後。</param>
    internal void Trigger(string reason, bool skipDelay = false) {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        reasons.Add(reason);
        lastTriggerTimestamp = timeProvider.GetTimestamp();
        IsPending = true;
        if (skipDelay) {
            isImmediate = true;
        }
    }

    /// <summary>
    /// 檢查目前是否該重建。回傳 <see cref="RebuildPollKind.Ready"/> 時，待處理狀態與累積原因會一併清除。
    /// </summary>
    /// <returns>輪詢結果。</returns>
    internal RebuildPollResult Poll() {
        if (IsRebuilding || !IsPending) {
            return new(RebuildPollKind.Idle, TimeSpan.Zero, "", false);
        }

        long now = timeProvider.GetTimestamp();
        if (!isImmediate) {
            TimeSpan elapsed = timeProvider.GetElapsedTime(lastTriggerTimestamp, now);
            if (elapsed < delay) {
                return new(RebuildPollKind.Waiting, delay - elapsed, JoinReasons(), false);
            }
        }

        if (isExporting()) {
            bool isFirstDeferral = !isDeferralReported;
            isDeferralReported = true;
            isImmediate = false;
            lastTriggerTimestamp = now;
            return new(RebuildPollKind.Deferred, delay, JoinReasons(), isFirstDeferral);
        }

        string joinedReasons = JoinReasons();
        reasons.Clear();
        IsPending = false;
        isImmediate = false;
        isDeferralReported = false;
        return new(RebuildPollKind.Ready, TimeSpan.Zero, joinedReasons, false);
    }

    /// <summary>
    /// 執行一次重建，整個執行期間 <see cref="IsRebuilding"/> 為 <see langword="true"/>，無論成功、失敗或擲出例外都會在結束時解除。
    /// </summary>
    /// <param name="rebuildAsync">實際的重建作業。</param>
    /// <returns>表示重建作業的工作；重建作業擲出的例外會原樣傳出。</returns>
    /// <exception cref="InvalidOperationException">已有重建正在執行。</exception>
    internal async Task RunRebuildAsync(Func<Task> rebuildAsync) {
        ArgumentNullException.ThrowIfNull(rebuildAsync);

        if (IsRebuilding) {
            throw new InvalidOperationException("已有重建正在執行。");
        }

        IsRebuilding = true;
        try {
            await rebuildAsync();
        } finally {
            IsRebuilding = false;
        }
    }

    private string JoinReasons() {
        return string.Join(", ", reasons);
    }
}
