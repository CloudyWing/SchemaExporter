namespace CloudyWing.SchemaExporter.Exporting;

internal sealed class SchemaJsonSidecar {
    public SchemaSnapshotDocument Snapshot { get; init; } = new();
    public SchemaDiffDocument? Diff { get; init; }
}
