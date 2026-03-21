namespace CloudyWing.SchemaExporter.Core.Exporting;

/// <summary>
/// 當資料庫結構描述載入失敗時擲回的例外狀況。
/// </summary>
public sealed class ExportConnectionException : ExportWorkflowException {
    /// <summary>
    /// 初始化 <see cref="ExportConnectionException"/> 類別的新執行個體，使用指定的錯誤訊息及內部例外狀況參考。
    /// </summary>
    /// <param name="message">連線相關的錯誤訊息。</param>
    /// <param name="innerException">造成此失敗的內部例外狀況。</param>
    public ExportConnectionException(string message, Exception innerException) : base(message, innerException) { }
}

