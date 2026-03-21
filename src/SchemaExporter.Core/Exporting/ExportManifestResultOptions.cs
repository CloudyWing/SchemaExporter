namespace CloudyWing.SchemaExporter.Exporting;

internal sealed class ExportManifestResultOptions {
    public bool UseTimestamp { get; init; }
    public string TimestampFormat { get; init; } = "";
    public string OverwriteStrategy { get; init; } = "";
    public bool OpenOutputFolder { get; init; }
    public bool GenerateManifest { get; init; }
    public bool GenerateJsonSidecar { get; init; }
    public bool GenerateMarkdownSidecar { get; init; }
    public bool GenerateSchemaSnapshot { get; init; }
    public string DiffSourceSnapshotPath { get; init; } = "";
}
