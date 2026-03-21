#nullable enable

namespace CloudyWing.SchemaExporter.Exporting;

/// <summary>
/// Defines the severity levels for export diagnostics.
/// </summary>
public enum DiagnosticSeverity {
    /// <summary>
    /// Informational message about export behavior.
    /// </summary>
    Info = 0,

    /// <summary>
    /// Warning about potential issues or limitations.
    /// </summary>
    Warning = 1
}
