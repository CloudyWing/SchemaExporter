namespace CloudyWing.SchemaExporter.Core.Exporting.Snapshots;

/// <summary>
/// 表示已序列化的結構描述快照文件。
/// </summary>
public sealed class SchemaSnapshotDocument {
    /// <summary>
    /// 取得或設定文件格式版本。
    /// </summary>
    public int SchemaVersion { get; init; }

    /// <summary>
    /// 取得或設定 snapshot 建立時間。
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
    /// 取得或設定數量統計資訊。
    /// </summary>
    public required SchemaSnapshotCounts Counts { get; init; }

    /// <summary>
    /// 取得或設定診斷資訊集合。
    /// </summary>
    public required IReadOnlyList<SchemaSnapshotDiagnostic> Diagnostics { get; init; }

    /// <summary>
    /// 取得或設定資料庫物件集合。
    /// </summary>
    public required IReadOnlyList<SchemaSnapshotObjectDocument> Objects { get; init; }

    /// <summary>
    /// 取得或設定程序與函數集合。
    /// </summary>
    public required IReadOnlyList<SchemaSnapshotRoutineDocument> Routines { get; init; }
}

