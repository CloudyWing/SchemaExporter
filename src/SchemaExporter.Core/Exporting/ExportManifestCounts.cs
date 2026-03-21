namespace CloudyWing.SchemaExporter.Exporting;

internal sealed class ExportManifestCounts {
    public int Objects { get; init; }
    public int Columns { get; init; }
    public int Indexes { get; init; }
    public int Routines { get; init; }
}
