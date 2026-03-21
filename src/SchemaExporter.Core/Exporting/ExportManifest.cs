namespace CloudyWing.SchemaExporter.Exporting;

internal sealed class ExportManifest {
    public DateTimeOffset ExportedAt { get; init; }
    public string ConnectionName { get; init; } = "";
    public string DatabaseType { get; init; } = "";
    public string ProfileName { get; init; } = "";
    public string OutputFilePath { get; init; } = "";
    public ExportManifestResultOptions ResultOptions { get; init; } = new();
    public ExportManifestCounts Counts { get; init; } = new();
    public List<ExportManifestDiagnostic> Diagnostics { get; init; } = [];
}
