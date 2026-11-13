namespace CloudyWing.SchemaExporter.Desktop.Services;

/// <summary>
/// 代表一個 <see cref="IPromptPresenter"/> 在 <see cref="PromptService"/> 的登錄；釋放即登出。
/// </summary>
internal sealed class PromptRegistration : IDisposable {
    private readonly Action<PromptRegistration> onDisposed;
    private readonly TaskCompletionSource detached = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int isDisposed;

    /// <summary>
    /// 初始化 <see cref="PromptRegistration"/> 類別的新執行個體。
    /// </summary>
    /// <param name="presenter">登錄的 presenter。</param>
    /// <param name="onDisposed">登出時通知登錄來源移除此登錄的回呼。</param>
    public PromptRegistration(IPromptPresenter presenter, Action<PromptRegistration> onDisposed) {
        ArgumentNullException.ThrowIfNull(presenter);
        ArgumentNullException.ThrowIfNull(onDisposed);

        Presenter = presenter;
        this.onDisposed = onDisposed;
    }

    /// <summary>
    /// 取得登錄的 presenter。
    /// </summary>
    public IPromptPresenter Presenter { get; }

    /// <summary>
    /// 取得在此登錄登出時完成的工作。
    /// </summary>
    public Task Detached => detached.Task;

    /// <summary>
    /// 登出此登錄；重複呼叫不會有額外效果。
    /// </summary>
    public void Dispose() {
        if (Interlocked.Exchange(ref isDisposed, 1) == 1) {
            return;
        }

        onDisposed(this);
        // TaskCompletionSource 以非同步延續建立，不會在元件 Dispose 的呼叫堆疊內同步執行呼叫端的後續程式。
        detached.TrySetResult();
    }
}
