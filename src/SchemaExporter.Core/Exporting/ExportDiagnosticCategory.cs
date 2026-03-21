#nullable enable

namespace CloudyWing.SchemaExporter.Exporting;

/// <summary>
/// Classifies diagnostics by workflow area.
/// </summary>
public enum ExportDiagnosticCategory {
    /// <summary>
    /// General export information.
    /// </summary>
    General = 0,

    /// <summary>
    /// Filtering-related information.
    /// </summary>
    Filtering = 1,

    /// <summary>
    /// File or sheet naming adjustments.
    /// </summary>
    Naming = 2,

    /// <summary>
    /// View support-level information.
    /// </summary>
    ViewSupport = 3,

    /// <summary>
    /// Connection/configuration resolution information.
    /// </summary>
    Configuration = 4,

    /// <summary>
    /// Routine documentation support information.
    /// </summary>
    RoutineSupport = 5,

    /// <summary>
    /// Execution timing and summary information.
    /// </summary>
    Execution = 6
}
