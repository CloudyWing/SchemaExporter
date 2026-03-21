namespace CloudyWing.SchemaExporter.Core.Exporting;

/// <summary>
/// 表示 manifest 中記錄的匯出數量統計。
/// </summary>
internal sealed class ExportManifestCounts {
    /// <summary>
    /// 取得或設定物件數量。
    /// </summary>
    public int Objects { get; init; }

    /// <summary>
    /// 取得或設定欄位數量。
    /// </summary>
    public int Columns { get; init; }

    /// <summary>
    /// 取得或設定索引數量。
    /// </summary>
    public int Indexes { get; init; }

    /// <summary>
    /// 取得或設定程序與函數數量。
    /// </summary>
    public int Routines { get; init; }
}

