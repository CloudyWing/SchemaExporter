namespace CloudyWing.SchemaExporter.Desktop.Hosting;

/// <summary>
/// 表示 <see cref="WebViewRebuildScheduler.Poll"/> 的回傳內容。
/// </summary>
/// <param name="Kind">結果種類。</param>
/// <param name="Wait">建議下次輪詢前等待的時間。</param>
/// <param name="Reasons">累積的觸發原因，以逗號串接。</param>
/// <param name="IsFirstDeferral">本次是否為同一段待處理期間內第一次延後。</param>
internal readonly record struct RebuildPollResult(
    RebuildPollKind Kind,
    TimeSpan Wait,
    string Reasons,
    bool IsFirstDeferral
);
