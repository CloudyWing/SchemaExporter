namespace CloudyWing.SchemaExporter.Core.Exporting.Diffs;

/// <summary>
/// Specifies the type of change detected between two schema snapshots.
/// </summary>
public enum SchemaChangeType {
    /// <summary>
    /// Indicates that the schema element was added.
    /// </summary>
    Added = 0,

    /// <summary>
    /// Indicates that the schema element was removed.
    /// </summary>
    Removed = 1,

    /// <summary>
    /// Indicates that the schema element was modified.
    /// </summary>
    Modified = 2
}

