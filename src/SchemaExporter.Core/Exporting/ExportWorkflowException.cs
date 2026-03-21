namespace CloudyWing.SchemaExporter.Core.Exporting;

/// <summary>
/// 匯出工作流程失敗的基底例外狀況。
/// </summary>
public abstract class ExportWorkflowException : Exception {
    /// <summary>
    /// 初始化 <see cref="ExportWorkflowException"/> 類別的新執行個體，使用指定的錯誤訊息。
    /// </summary>
    /// <param name="message">錯誤訊息。</param>
    /// <param name="innerException">內部例外狀況（若有）。</param>
    protected ExportWorkflowException(string message, Exception? innerException = null)
        : base(message, innerException) {
    }
}

