namespace CloudyWing.SchemaExporter.Desktop.Hosting;

/// <summary>
/// 表示重建排程輪詢的結果種類。
/// </summary>
internal enum RebuildPollKind {
    /// <summary>
    /// 沒有待處理的重建。
    /// </summary>
    Idle,

    /// <summary>
    /// 延遲時間尚未到期。
    /// </summary>
    Waiting,

    /// <summary>
    /// 延遲已到期，但匯出進行中，延後重新檢查。
    /// </summary>
    Deferred,

    /// <summary>
    /// 可以開始重建。
    /// </summary>
    Ready
}
