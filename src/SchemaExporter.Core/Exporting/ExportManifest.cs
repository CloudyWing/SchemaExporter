namespace CloudyWing.SchemaExporter.Core.Exporting;

/// <summary>
/// 表示匯出結果對應的 manifest 文件內容。
/// </summary>
internal sealed class ExportManifest {
    /// <summary>
    /// 取得或設定 manifest 建立時間。
    /// </summary>
    public DateTimeOffset ExportedAt { get; init; }

    /// <summary>
    /// 取得或設定連線名稱。
    /// </summary>
    public string ConnectionName { get; init; } = "";

    /// <summary>
    /// 取得或設定資料庫類型名稱。
    /// </summary>
    public string DatabaseType { get; init; } = "";

    /// <summary>
    /// 取得或設定匯出設定檔名稱。
    /// </summary>
    public string ProfileName { get; init; } = "";

    /// <summary>
    /// 取得或設定輸出活頁簿路徑。
    /// </summary>
    public string OutputFilePath { get; init; } = "";

    /// <summary>
    /// 取得或設定結果選項資訊。
    /// </summary>
    public ExportManifestResultOptions ResultOptions { get; init; } = new();

    /// <summary>
    /// 取得或設定匯出項目統計資訊。
    /// </summary>
    public ExportManifestCounts Counts { get; init; } = new();

    /// <summary>
    /// 取得或設定診斷資訊集合。
    /// </summary>
    public List<ExportManifestDiagnostic> Diagnostics { get; init; } = [];
}

