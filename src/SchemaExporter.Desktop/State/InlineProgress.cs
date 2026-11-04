namespace CloudyWing.SchemaExporter.Desktop.State;

/// <summary>
/// 在回報的執行緒上同步呼叫處理常式的 <see cref="IProgress{T}"/>。
/// </summary>
/// <typeparam name="T">進度資料型別。</typeparam>
/// <remarks>
/// <see cref="Progress{T}"/> 會把回報排入同步內容，最後一筆進度可能晚於匯出完成才執行而蓋掉完成訊息；
/// 這裡改為同步呼叫，畫面再由 Component 以 <c>InvokeAsync</c> 回到 Renderer 執行緒重新繪製。
/// </remarks>
internal sealed class InlineProgress<T> : IProgress<T> {
    private readonly Action<T> handler;

    /// <summary>
    /// 初始化 <see cref="InlineProgress{T}"/> 類別的新執行個體。
    /// </summary>
    /// <param name="handler">收到進度時呼叫的處理常式。</param>
    public InlineProgress(Action<T> handler) {
        ArgumentNullException.ThrowIfNull(handler);
        this.handler = handler;
    }

    /// <inheritdoc/>
    public void Report(T value) {
        handler(value);
    }
}
