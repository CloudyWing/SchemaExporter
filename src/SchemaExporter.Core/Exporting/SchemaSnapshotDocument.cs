#nullable enable

namespace CloudyWing.SchemaExporter.Exporting;

/// <summary>
/// Represents a serialized schema snapshot document.
/// </summary>
public sealed class SchemaSnapshotDocument {
    public int SchemaVersion { get; init; }
    public DateTimeOffset ExportedAt { get; init; }
    public string ConnectionName { get; init; } = "";
    public string DatabaseType { get; init; } = "";
    public string ProfileName { get; init; } = "";
    public string OutputFilePath { get; init; } = "";
    public SchemaSnapshotCounts Counts { get; set; } = new();
    public List<SchemaSnapshotDiagnostic> Diagnostics { get; set; } = [];
    public List<SchemaSnapshotObjectDocument> Objects { get; set; } = [];
    public List<SchemaSnapshotRoutineDocument> Routines { get; set; } = [];
}
