namespace CloudyWing.SchemaExporter.Core.Exporting;

/// <summary>
/// 表示功能在匯出輸出中的支援程度。
/// </summary>
public enum ExportSupportLevel {
    /// <summary>
    /// 完整支援。
    /// </summary>
    Full = 0,

    /// <summary>
    /// 部分支援。
    /// </summary>
    Partial = 1,

    /// <summary>
    /// 不支援。
    /// </summary>
    Unsupported = 2
}

