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
    public required string ConnectionName { get; init; }

    /// <summary>
    /// 取得或設定資料庫類型名稱。
    /// </summary>
    public required string DatabaseType { get; init; }

    /// <summary>
    /// 取得或設定匯出設定檔名稱。
    /// </summary>
    public required string ProfileName { get; init; }

    /// <summary>
    /// 取得或設定輸出活頁簿路徑。
    /// </summary>
    public required string OutputFilePath { get; init; }

    /// <summary>
    /// 取得或設定結果選項資訊。
    /// </summary>
    public required ExportManifestResultOptions ResultOptions { get; init; }

    /// <summary>
    /// 取得或設定匯出項目統計資訊。
    /// </summary>
    public required ExportManifestCounts Counts { get; init; }

    /// <summary>
    /// 取得或設定診斷資訊集合。
    /// </summary>
    public required IReadOnlyList<ExportManifestDiagnostic> Diagnostics { get; init; }
}

