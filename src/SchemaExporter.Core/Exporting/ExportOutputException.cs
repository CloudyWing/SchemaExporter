namespace CloudyWing.SchemaExporter.Core.Exporting;

/// <summary>
/// 當輸出產生或檔案處理失敗時擲回的例外狀況。
/// </summary>
public sealed class ExportOutputException : ExportWorkflowException {
    /// <summary>
    /// 初始化 <see cref="ExportOutputException"/> 類別的新執行個體，使用指定的錯誤訊息。
    /// </summary>
    /// <param name="message">輸出相關的錯誤訊息。</param>
    public ExportOutputException(string message) : base(message) { }

    /// <summary>
    /// 初始化 <see cref="ExportOutputException"/> 類別的新執行個體，使用指定的錯誤訊息及內部例外狀況參考。
    /// </summary>
    /// <param name="message">輸出相關的錯誤訊息。</param>
    /// <param name="innerException">造成此失敗的內部例外狀況。</param>
    public ExportOutputException(string message, Exception innerException) : base(message, innerException) { }
}

