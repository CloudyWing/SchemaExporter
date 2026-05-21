namespace CloudyWing.SchemaExporter.Core.Exporting;

/// <summary>
/// 表示資料庫 provider 對特定 schema metadata 類型的支援狀態。
/// </summary>
internal sealed class ProviderCapability {
    /// <summary>
    /// 取得支援項目名稱。
    /// </summary>
    public required string Area { get; init; }

    /// <summary>
    /// 取得支援層級。
    /// </summary>
    public required ExportSupportLevel SupportLevel { get; init; }

    /// <summary>
    /// 取得支援範圍說明。
    /// </summary>
    public required string Notes { get; init; }
}
