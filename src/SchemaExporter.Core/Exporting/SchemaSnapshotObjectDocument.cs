#nullable enable
namespace CloudyWing.SchemaExporter.Exporting;
public sealed class SchemaSnapshotObjectDocument {
    public string SchemaName { get; init; } = "";
    public string ObjectName { get; init; } = "";
    public string ObjectType { get; init; } = "";
    public string ObjectDescription { get; init; } = "";
    public List<SchemaSnapshotColumnDocument> Columns { get; set; } = [];
    public List<SchemaSnapshotIndexDocument> Indexes { get; set; } = [];
}
