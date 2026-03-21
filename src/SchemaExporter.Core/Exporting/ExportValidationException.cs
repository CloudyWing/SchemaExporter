namespace CloudyWing.SchemaExporter.Core.Exporting;

/// <summary>
/// 當匯出驗證失敗時擲回的例外狀況。
/// </summary>
public sealed class ExportValidationException : ExportWorkflowException {
    /// <summary>
    /// 初始化 <see cref="ExportValidationException"/> 類別的新執行個體，使用指定的錯誤訊息。
    /// </summary>
    /// <param name="message">驗證錯誤訊息。</param>
    public ExportValidationException(string message) : base(message) { }

    /// <summary>
    /// 初始化 <see cref="ExportValidationException"/> 類別的新執行個體，使用指定的錯誤訊息及內部例外狀況參考。
    /// </summary>
    /// <param name="message">驗證錯誤訊息。</param>
    /// <param name="innerException">造成此驗證失敗的內部例外狀況。</param>
    public ExportValidationException(string message, Exception innerException) : base(message, innerException) { }
}

