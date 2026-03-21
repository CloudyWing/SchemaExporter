#nullable enable

namespace CloudyWing.SchemaExporter.Exporting;

/// <summary>
/// Indicates how completely a feature is supported in export output.
/// </summary>
public enum ExportSupportLevel {
    /// <summary>
    /// Fully supported.
    /// </summary>
    Full = 0,

    /// <summary>
    /// Partially supported.
    /// </summary>
    Partial = 1,

    /// <summary>
    /// Not supported.
    /// </summary>
    Unsupported = 2
}
