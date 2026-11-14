namespace CloudyWing.SchemaExporter.Desktop.Services;

/// <summary>
/// 定義原生檔案與資料夾選擇對話框。
/// </summary>
internal interface IFileDialogService {
    /// <summary>
    /// 開啟資料夾選擇對話框。
    /// </summary>
    /// <param name="title">對話框標題。</param>
    /// <param name="initialDirectory">初始資料夾；不存在時由系統決定。</param>
    /// <returns>使用者選取的資料夾完整路徑；取消時回傳 <see langword="null"/>。</returns>
    string? PickFolder(string title, string? initialDirectory);

    /// <summary>
    /// 開啟檔案選擇對話框。
    /// </summary>
    /// <param name="title">對話框標題。</param>
    /// <param name="filter">檔案類型篩選字串，格式同 Win32 對話框，例如 <c>JSON (*.json)|*.json</c>。</param>
    /// <param name="initialPath">初始檔案或資料夾；不存在時由系統決定。</param>
    /// <returns>使用者選取的檔案完整路徑；取消時回傳 <see langword="null"/>。</returns>
    string? PickFile(string title, string filter, string? initialPath);
}
