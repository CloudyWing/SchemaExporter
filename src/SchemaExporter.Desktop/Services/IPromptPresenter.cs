using CloudyWing.SchemaExporter.Desktop.State;

namespace CloudyWing.SchemaExporter.Desktop.Services;

/// <summary>
/// 定義單一 WebView 內實際顯示提示的能力。
/// </summary>
internal interface IPromptPresenter {
    /// <summary>
    /// 顯示 Snackbar 提示。
    /// </summary>
    /// <param name="kind">提示的嚴重度。</param>
    /// <param name="message">訊息內容。</param>
    void ShowSnackbar(StatusKind kind, string message);

    /// <summary>
    /// 顯示只有「確定」按鈕的對話框。
    /// </summary>
    /// <param name="title">對話框標題。</param>
    /// <param name="message">訊息內容。</param>
    /// <returns>使用者按下「確定」時回傳 <see langword="true"/>；否則回傳 <see langword="false"/>。</returns>
    Task<bool> ShowMessageAsync(string title, string message);

    /// <summary>
    /// 顯示有確認與取消按鈕的對話框。
    /// </summary>
    /// <param name="title">對話框標題。</param>
    /// <param name="message">訊息內容。</param>
    /// <param name="confirmText">確認按鈕的文字。</param>
    /// <param name="cancelText">取消按鈕的文字。</param>
    /// <returns>使用者按下確認按鈕時回傳 <see langword="true"/>；否則回傳 <see langword="false"/>。</returns>
    Task<bool> ConfirmAsync(string title, string message, string confirmText, string cancelText);
}
