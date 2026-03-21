#nullable enable
namespace CloudyWing.SchemaExporter.Exporting;
public sealed class SchemaDiffSummary {
    public int AddedObjects { get; init; }
    public int RemovedObjects { get; init; }
    public int ModifiedObjects { get; init; }
    public int AddedColumns { get; init; }
    public int RemovedColumns { get; init; }
    public int ModifiedColumns { get; init; }
    public int AddedIndexes { get; init; }
    public int RemovedIndexes { get; init; }
    public int ModifiedIndexes { get; init; }
    public int AddedRoutines { get; init; }
    public int RemovedRoutines { get; init; }
    public int ModifiedRoutines { get; init; }
}
