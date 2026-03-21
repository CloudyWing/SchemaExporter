#nullable enable

namespace CloudyWing.SchemaExporter;

/// <summary>
/// Represents the supported database providers for schema extraction.
/// </summary>
public enum DatabaseType {
    /// <summary>
    /// Microsoft SQL Server.
    /// </summary>
    SqlServer = 0,

    /// <summary>
    /// Oracle Database.
    /// </summary>
    Oracle = 1
}
