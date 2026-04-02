namespace CloudyWing.SchemaExporter.Core.Exporting;

/// <summary>
/// 表示 manifest 中記錄的一筆診斷資訊。
/// </summary>
internal sealed class ExportManifestDiagnostic {
    /// <summary>
    /// 取得或設定診斷嚴重性。
    /// </summary>
    public required string Severity { get; init; }

    /// <summary>
    /// 取得或設定診斷分類。
    /// </summary>
    public required string Category { get; init; }

    /// <summary>
    /// 取得或設定支援等級描述。
    /// </summary>
    public string? SupportLevel { get; init; }

    /// <summary>
    /// 取得或設定受影響的物件識別。
    /// </summary>
    public string? AffectedObject { get; init; }

    /// <summary>
    /// 取得或設定診斷訊息。
    /// </summary>
    public required string Message { get; init; }
}

