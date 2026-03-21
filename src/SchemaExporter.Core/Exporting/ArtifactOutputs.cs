namespace CloudyWing.SchemaExporter.Exporting;

internal sealed class ArtifactOutputs {
    public string? ManifestFilePath { get; init; }
    public string? JsonSidecarFilePath { get; init; }
    public string? MarkdownSidecarFilePath { get; init; }
    public string? SnapshotFilePath { get; init; }
    public string? DiffFilePath { get; init; }
}
