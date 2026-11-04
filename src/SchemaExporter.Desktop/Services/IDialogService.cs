namespace CloudyWing.SchemaExporter.Desktop.Services;

/// <summary>
/// 定義原生訊息對話框，讓 Razor Component 與狀態物件不直接依賴 WPF。
/// </summary>
internal interface IDialogService {
    /// <summary>
    /// 顯示資訊訊息。
    /// </summary>
    /// <param name="title">對話框標題。</param>
    /// <param name="message">訊息內容。</param>
    void ShowInformation(string title, string message);

    /// <summary>
    /// 顯示警告訊息。
    /// </summary>
    /// <param name="title">對話框標題。</param>
    /// <param name="message">訊息內容。</param>
    void ShowWarning(string title, string message);

    /// <summary>
    /// 顯示錯誤訊息。
    /// </summary>
    /// <param name="title">對話框標題。</param>
    /// <param name="message">訊息內容。</param>
    void ShowError(string title, string message);

    /// <summary>
    /// 顯示是／否確認對話框。
    /// </summary>
    /// <param name="title">對話框標題。</param>
    /// <param name="message">訊息內容。</param>
    /// <returns>使用者選擇「是」時回傳 <see langword="true"/>；否則回傳 <see langword="false"/>。</returns>
    bool Confirm(string title, string message);
}
