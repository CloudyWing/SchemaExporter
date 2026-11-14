using CloudyWing.SchemaExporter.Desktop.State;

namespace CloudyWing.SchemaExporter.Desktop.Services;

/// <summary>
/// 定義畫面內提示的呼叫入口，讓 State 類別不直接依賴 MudBlazor 或 WPF。
/// </summary>
internal interface IPromptService {
    /// <summary>
    /// 顯示非阻塞的 Snackbar 提示，立即返回；沒有可用的畫面時丟棄提示。
    /// </summary>
    /// <param name="kind">提示的嚴重度。</param>
    /// <param name="message">訊息內容。</param>
    void Notify(StatusKind kind, string message);

    /// <summary>
    /// 顯示只有「確定」按鈕的對話框。
    /// </summary>
    /// <param name="title">對話框標題。</param>
    /// <param name="message">訊息內容。</param>
    /// <returns>使用者按下「確定」時回傳 <see langword="true"/>；按下 ESC、畫面已釋放、沒有可用的畫面或顯示失敗時回傳 <see langword="false"/>。</returns>
    Task<bool> ShowMessageAsync(string title, string message);

    /// <summary>
    /// 顯示有確認與取消按鈕的對話框。
    /// </summary>
    /// <param name="title">對話框標題。</param>
    /// <param name="message">訊息內容。</param>
    /// <param name="confirmText">確認按鈕的文字。</param>
    /// <param name="cancelText">取消按鈕的文字。</param>
    /// <returns>使用者按下確認按鈕時回傳 <see langword="true"/>；按下取消按鈕、ESC、畫面已釋放、沒有可用的畫面或顯示失敗時回傳 <see langword="false"/>。</returns>
    Task<bool> ConfirmAsync(string title, string message, string confirmText, string cancelText);
}
