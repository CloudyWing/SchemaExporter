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
    /// 取得或設定數量統計資訊。
    /// </summary>
    public SchemaSnapshotCounts Counts { get; set; } = new();

    /// <summary>
    /// 取得或設定診斷資訊集合。
    /// </summary>
    public List<SchemaSnapshotDiagnostic> Diagnostics { get; set; } = [];

    /// <summary>
    /// 取得或設定資料庫物件集合。
    /// </summary>
    public List<SchemaSnapshotObjectDocument> Objects { get; set; } = [];

    /// <summary>
    /// 取得或設定程序與函數集合。
    /// </summary>
    public List<SchemaSnapshotRoutineDocument> Routines { get; set; } = [];
}

