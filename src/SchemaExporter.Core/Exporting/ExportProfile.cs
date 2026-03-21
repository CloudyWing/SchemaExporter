namespace CloudyWing.SchemaExporter.Exporting;

/// <summary>
/// Defines filters and preferences for a schema export operation.
/// </summary>
public sealed class ExportProfile {
    /// <summary>
    /// Gets or sets the profile name displayed to users.
    /// </summary>
    public string Name { get; set; } = "Default";

    /// <summary>
    /// Gets or sets schema name patterns to include (empty means all schemas).
    /// Supports wildcards: * for any characters, ? for single character.
    /// </summary>
    public List<string> IncludeSchemas { get; set; } = [];

    /// <summary>
    /// Gets or sets schema name patterns to exclude.
    /// Exclusions are applied after inclusions.
    /// </summary>
    public List<string> ExcludeSchemas { get; set; } = [];

    /// <summary>
    /// Gets or sets object name patterns to include (empty means all objects).
    /// </summary>
    public List<string> IncludeObjects { get; set; } = [];

    /// <summary>
    /// Gets or sets object name patterns to exclude.
    /// </summary>
    public List<string> ExcludeObjects { get; set; } = [];

    /// <summary>
    /// Gets or sets whether to include views in addition to tables.
    /// </summary>
    public bool IncludeViews { get; set; } = true;
}
