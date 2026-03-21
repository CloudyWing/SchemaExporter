using CloudyWing.SchemaExporter.Exporting;

namespace CloudyWing.SchemaExporter;

/// <summary>
/// Configuration options for schema export operations.
/// </summary>
public class SchemaOptions {
    /// <summary>
    /// Gets the configuration section name.
    /// </summary>
    public const string OptionsName = "Schema";

    /// <summary>
    /// Gets or sets the base directory path for export output.
    /// </summary>
    public string ExportPath { get; set; } = "";

    /// <summary>
    /// Gets or sets the list of available database connections.
    /// </summary>
    public List<SchemaConnection> Connections { get; set; } = [];

    /// <summary>
    /// Gets or sets the available export profiles defining filters and preferences.
    /// </summary>
    public List<ExportProfile> ExportProfiles { get; set; } = [];

    /// <summary>
    /// Gets or sets the default export result options for file naming and post-export actions.
    /// </summary>
    public ExportResultOptions ExportResultOptions { get; set; } = new();
}
