#nullable enable

namespace CloudyWing.SchemaExporter;

/// <summary>
/// Represents a named database connection used for schema export.
/// </summary>
public class SchemaConnection {
    /// <summary>
    /// Gets or sets the display name shown in the UI.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Gets or sets the database provider type. Defaults to SQL Server when omitted from configuration.
    /// </summary>
    public DatabaseType DatabaseType { get; set; } = DatabaseType.SqlServer;

    /// <summary>
    /// Gets or sets the database connection string.
    /// </summary>
    public string ConnectionString { get; set; } = "";

    /// <summary>
    /// Gets or sets the name of the export profile to use for this connection.
    /// If null or empty, the default profile is used.
    /// </summary>
    public string? ExportProfileName { get; set; }
}
