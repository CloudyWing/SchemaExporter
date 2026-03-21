namespace CloudyWing.SchemaExporter.Exporting;

internal sealed class ExportManifestDiagnostic {
    public string Severity { get; init; } = "";
    public string Category { get; init; } = "";
    public string SupportLevel { get; init; } = "";
    public string? AffectedObject { get; init; }
    public string Message { get; init; } = "";
}
