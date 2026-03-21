#nullable enable
namespace CloudyWing.SchemaExporter.Exporting;
public sealed class SchemaSnapshotDiagnostic {
    public string Severity { get; init; } = "";
    public string Category { get; init; } = "";
    public string SupportLevel { get; init; } = "";
    public string? AffectedObject { get; init; }
    public string Message { get; init; } = "";
}
