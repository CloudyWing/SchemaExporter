#nullable enable
namespace CloudyWing.SchemaExporter.Exporting;
public sealed class SchemaSnapshotCounts {
    public int Objects { get; init; }
    public int Columns { get; init; }
    public int Indexes { get; init; }
    public int Routines { get; init; }
}
