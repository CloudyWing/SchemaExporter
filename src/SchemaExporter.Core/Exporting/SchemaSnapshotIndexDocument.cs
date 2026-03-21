#nullable enable
namespace CloudyWing.SchemaExporter.Exporting;
public sealed class SchemaSnapshotIndexDocument {
    public string IndexName { get; init; } = "";
    public string IsPrimaryKey { get; init; } = "";
    public string IsClustered { get; init; } = "";
    public string IsUnique { get; init; } = "";
    public string IsForeignKey { get; init; } = "";
    public string Columns { get; init; } = "";
    public string OtherColumns { get; init; } = "";
}
