namespace CloudyWing.SchemaExporter.Core.Exporting;

/// <summary>
/// 表示一次匯出流程各階段的執行時間摘要。
/// </summary>
internal sealed class ExportExecutionSummary {
    /// <summary>
    /// 取得或設定驗證階段耗時。
    /// </summary>
    public TimeSpan ValidationDuration { get; set; }

    /// <summary>
    /// 取得或設定載入結構資訊階段耗時。
    /// </summary>
    public TimeSpan SchemaLoadDuration { get; set; }

    /// <summary>
    /// 取得或設定套用篩選條件階段耗時。
    /// </summary>
    public TimeSpan FilteringDuration { get; set; }

    /// <summary>
    /// 取得或設定產生活頁簿階段耗時。
    /// </summary>
    public TimeSpan WorkbookDuration { get; set; }

    /// <summary>
    /// 取得或設定整理附加產物階段耗時。
    /// </summary>
    public TimeSpan ArtifactDuration { get; set; }

    /// <summary>
    /// 取得或設定整體匯出流程耗時。
    /// </summary>
    public TimeSpan TotalDuration { get; set; }
}

