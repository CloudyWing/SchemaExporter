using CloudyWing.SchemaExporter.Core.Exporting.Diffs;
using CloudyWing.SchemaExporter.Core.Exporting.Snapshots;

namespace CloudyWing.SchemaExporter.Core.Exporting;

/// <summary>
/// 表示 schema 匯出的 JSON sidecar 文件。
/// </summary>
internal sealed class SchemaJsonSidecar {
    /// <summary>
    /// 取得或設定目前匯出的 snapshot。
    /// </summary>
    public SchemaSnapshotDocument Snapshot { get; init; } = new();

    /// <summary>
    /// 取得或設定與基準 snapshot 的差異；若未產生差異則為 <see langword="null" />。
    /// </summary>
    public SchemaDiffDocument? Diff { get; init; }
}

