namespace CloudyWing.SchemaExporter.Core.Exporting.Diffs;

/// <summary>
/// 表示 schema 差異文件的統計摘要。
/// </summary>
public sealed class SchemaDiffSummary {
    /// <summary>
    /// 取得或設定新增的物件數量。
    /// </summary>
    public int AddedObjects { get; init; }

    /// <summary>
    /// 取得或設定移除的物件數量。
    /// </summary>
    public int RemovedObjects { get; init; }

    /// <summary>
    /// 取得或設定修改的物件數量。
    /// </summary>
    public int ModifiedObjects { get; init; }

    /// <summary>
    /// 取得或設定新增的欄位數量。
    /// </summary>
    public int AddedColumns { get; init; }

    /// <summary>
    /// 取得或設定移除的欄位數量。
    /// </summary>
    public int RemovedColumns { get; init; }

    /// <summary>
    /// 取得或設定修改的欄位數量。
    /// </summary>
    public int ModifiedColumns { get; init; }

    /// <summary>
    /// 取得或設定新增的索引數量。
    /// </summary>
    public int AddedIndexes { get; init; }

    /// <summary>
    /// 取得或設定移除的索引數量。
    /// </summary>
    public int RemovedIndexes { get; init; }

    /// <summary>
    /// 取得或設定修改的索引數量。
    /// </summary>
    public int ModifiedIndexes { get; init; }

    /// <summary>
    /// 取得或設定新增的程序與函數數量。
    /// </summary>
    public int AddedRoutines { get; init; }

    /// <summary>
    /// 取得或設定移除的程序與函數數量。
    /// </summary>
    public int RemovedRoutines { get; init; }

    /// <summary>
    /// 取得或設定修改的程序與函數數量。
    /// </summary>
    public int ModifiedRoutines { get; init; }
}

