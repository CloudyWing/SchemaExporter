namespace CloudyWing.SchemaExporter.Exporting;

/// <summary>
/// Defines strategies for handling existing export files.
/// </summary>
public enum OverwriteStrategy {
    /// <summary>
    /// Overwrite the existing file without prompting.
    /// </summary>
    Overwrite = 0,

    /// <summary>
    /// Append a numeric suffix to create a unique filename.
    /// </summary>
    AppendSuffix = 1,

    /// <summary>
    /// Fail the export if the file already exists.
    /// </summary>
    Fail = 2
}
