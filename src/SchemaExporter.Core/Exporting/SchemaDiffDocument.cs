#nullable enable
namespace CloudyWing.SchemaExporter.Exporting;
public sealed class SchemaDiffDocument {
    public int SchemaVersion { get; init; }
    public DateTimeOffset GeneratedAt { get; init; }
    public string LeftSnapshotPath { get; init; } = "";
    public string RightSnapshotPath { get; init; } = "";
    public SchemaDiffSummary Summary { get; init; } = new();
    public List<SchemaDiffEntry> ObjectChanges { get; init; } = [];
    public List<SchemaDiffEntry> ColumnChanges { get; init; } = [];
    public List<SchemaDiffEntry> IndexChanges { get; init; } = [];
    public List<SchemaDiffEntry> RoutineChanges { get; init; } = [];
}
