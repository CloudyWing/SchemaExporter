namespace CloudyWing.SchemaExporter.Core.Exporting;

/// <summary>
/// 定義匯出診斷的嚴重性層級。
/// </summary>
public enum DiagnosticSeverity {
    /// <summary>
    /// 關於匯出行為的資訊訊息。
    /// </summary>
    Info = 0,

    /// <summary>
    /// 關於潛在問題或限制的警告。
    /// </summary>
    Warning = 1,

    /// <summary>
    /// 關於已知錯誤或無法完成項目的診斷。
    /// </summary>
    Error = 2
}

