using CloudyWing.SchemaExporter.Desktop.State;
using Microsoft.Extensions.Logging;

namespace CloudyWing.SchemaExporter.Desktop.Services;

/// <summary>
/// 管理各 WebView 的 <see cref="IPromptPresenter"/> 登錄，並把 State 類別的提示轉給作用中的 presenter。
/// </summary>
/// <remarks>
/// 作用中的 presenter 是最後一個仍在登錄的 presenter。presenter 登出、不存在或顯示失敗時，
/// 等待使用者回應的提示一律回傳 <see langword="false"/>，不向呼叫端擲回例外。
/// </remarks>
internal sealed partial class PromptService : IPromptService {
    private readonly ILogger<PromptService> logger;
    private readonly Lock gate = new();
    private readonly List<PromptRegistration> registrations = [];

    /// <summary>
    /// 初始化 <see cref="PromptService"/> 類別的新執行個體。
    /// </summary>
    /// <param name="logger">用於記錄顯示失敗的記錄器。</param>
    public PromptService(ILogger<PromptService> logger) {
        ArgumentNullException.ThrowIfNull(logger);

        this.logger = logger;
    }

    /// <summary>
    /// 登錄 presenter，並成為作用中的 presenter。
    /// </summary>
    /// <param name="presenter">要登錄的 presenter。</param>
    /// <returns>釋放時登出的登錄物件。</returns>
    public IDisposable Attach(IPromptPresenter presenter) {
        ArgumentNullException.ThrowIfNull(presenter);

        PromptRegistration registration = new(presenter, Detach);
        lock (gate) {
            registrations.Add(registration);
        }

        return registration;
    }

    /// <inheritdoc/>
    public void Notify(StatusKind kind, string message) {
        ArgumentNullException.ThrowIfNull(message);

        PromptRegistration? registration = GetActiveRegistration();
        if (registration is null) {
            return;
        }

        try {
            registration.Presenter.ShowSnackbar(kind, message);
        } catch (Exception ex) {
            LogPresentFailed(logger, ex);
        }
    }

    /// <inheritdoc/>
    public async Task<bool> ShowMessageAsync(string title, string message) {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(message);

        return await PresentAsync(presenter => presenter.ShowMessageAsync(title, message));
    }

    /// <inheritdoc/>
    public async Task<bool> ConfirmAsync(string title, string message, string confirmText, string cancelText) {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(confirmText);
        ArgumentNullException.ThrowIfNull(cancelText);

        return await PresentAsync(presenter => presenter.ConfirmAsync(title, message, confirmText, cancelText));
    }

    private async Task<bool> PresentAsync(Func<IPromptPresenter, Task<bool>> present) {
        PromptRegistration? registration = GetActiveRegistration();
        if (registration is null) {
            return false;
        }

        Task<bool> presented;
        try {
            presented = present(registration.Presenter);
        } catch (Exception ex) {
            LogPresentFailed(logger, ex);
            return false;
        }

        // presenter 所在的 WebView 被釋放時，MudBlazor 不一定會完成結果工作，因此與登出訊號競爭。
        Task completed = await Task.WhenAny(presented, registration.Detached);
        if (!ReferenceEquals(completed, presented)) {
            ObserveFault(presented);
            return false;
        }

        try {
            return await presented;
        } catch (Exception ex) {
            LogPresentFailed(logger, ex);
            return false;
        }
    }

    private PromptRegistration? GetActiveRegistration() {
        lock (gate) {
            return registrations.Count > 0 ? registrations[^1] : null;
        }
    }

    private void Detach(PromptRegistration registration) {
        lock (gate) {
            registrations.Remove(registration);
        }
    }

    // 放棄等待的工作之後若才失敗，需要觀察例外，避免出現未觀察的工作例外。
    private static void ObserveFault(Task<bool> presented) {
        _ = presented.ContinueWith(
            static task => _ = task.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default
        );
    }

    [LoggerMessage(EventId = 3100, Level = LogLevel.Warning, Message = "Failed to present a prompt through the active presenter.")]
    private static partial void LogPresentFailed(ILogger logger, Exception exception);
}
