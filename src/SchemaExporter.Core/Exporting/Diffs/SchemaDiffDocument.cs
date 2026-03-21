namespace CloudyWing.SchemaExporter.Core.Exporting.Diffs;

/// <summary>
/// 表示兩份 schema snapshot 之間的差異文件。
/// </summary>
public sealed class SchemaDiffDocument {
    /// <summary>
    /// 取得或設定文件格式版本。
    /// </summary>
    public int SchemaVersion { get; init; }

    /// <summary>
    /// 取得或設定差異文件產生時間。
    /// </summary>
    public DateTimeOffset GeneratedAt { get; init; }

    /// <summary>
    /// 取得或設定左側 snapshot 路徑。
    /// </summary>
    public string LeftSnapshotPath { get; init; } = "";

    /// <summary>
    /// 取得或設定右側 snapshot 路徑。
    /// </summary>
    public string RightSnapshotPath { get; init; } = "";

    /// <summary>
    /// 取得或設定差異摘要。
    /// </summary>
    public SchemaDiffSummary Summary { get; init; } = new();

    /// <summary>
    /// 取得或設定物件層級的差異集合。
    /// </summary>
    public List<SchemaDiffEntry> ObjectChanges { get; init; } = [];

    /// <summary>
    /// 取得或設定欄位層級的差異集合。
    /// </summary>
    public List<SchemaDiffEntry> ColumnChanges { get; init; } = [];

    /// <summary>
    /// 取得或設定索引層級的差異集合。
    /// </summary>
    public List<SchemaDiffEntry> IndexChanges { get; init; } = [];

    /// <summary>
    /// 取得或設定程序與函數層級的差異集合。
    /// </summary>
    public List<SchemaDiffEntry> RoutineChanges { get; init; } = [];
}

