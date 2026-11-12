namespace CloudyWing.SchemaExporter.Desktop.Hosting;

/// <summary>
/// 表示未處理例外的判定結果。
/// </summary>
internal enum WebViewExceptionDecision {
    /// <summary>
    /// 例外鏈中沒有 <see cref="ArgumentException"/> 或 <see cref="System.Runtime.InteropServices.COMException"/>。
    /// </summary>
    NotMatched,

    /// <summary>
    /// 例外鏈中含有組件載入類的例外，不可吞下。
    /// </summary>
    Excluded,

    /// <summary>
    /// 堆疊的第一個非 Marshal 型別不是 WebView2 的圖形相關型別。
    /// </summary>
    StackMismatch,

    /// <summary>
    /// 尚未完成第一次成功渲染。
    /// </summary>
    NotYetRendered,

    /// <summary>
    /// 視窗內已達吞下次數上限。
    /// </summary>
    OverLimit,

    /// <summary>
    /// 可以吞下。
    /// </summary>
    Swallow
}
