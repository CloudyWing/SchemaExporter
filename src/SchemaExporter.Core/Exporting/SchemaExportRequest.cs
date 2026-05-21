namespace CloudyWing.SchemaExporter.Core.Exporting;

/// <summary>
/// 表示執行一次 schema 匯出所需的完整請求資料。
/// </summary>
public sealed class SchemaExportRequest {
    /// <summary>
    /// 取得匯出所使用的資料庫連線。
    /// </summary>
    public required SchemaConnection Connection { get; init; }

    /// <summary>
    /// 取得匯出輸出的基底目錄路徑。
    /// </summary>
    public required string ExportPath { get; init; }

    /// <summary>
    /// 取得匯出所使用的設定檔。
    /// </summary>
    public required ExportProfile Profile { get; init; }

    /// <summary>
    /// 取得實際套用的輸出結果選項。
    /// </summary>
    public required ExportResultOptions ResultOptions { get; init; }

    /// <summary>
    /// 取得實際套用的敏感 metadata 遮罩選項。
    /// </summary>
    public SchemaRedactionOptions Redaction { get; init; } = new();
}
