namespace CloudyWing.SchemaExporter.Core.Exporting;

/// <summary>
/// 向 UI 回報匯出的增量進度。
/// </summary>
public sealed class ExportProgress {
    /// <summary>
    /// 取得或設定目前的匯出階段。
    /// </summary>
    public ExportStage Stage { get; set; }

    /// <summary>
    /// 取得或設定目前的進度訊息。
    /// </summary>
    public required string Message { get; set; }

    /// <summary>
    /// 取得或設定完成百分比（0-100），若無法確定則為 null。
    /// </summary>
    public int? PercentComplete { get; set; }
}

