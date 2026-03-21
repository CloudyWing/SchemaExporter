#nullable enable

namespace CloudyWing.SchemaExporter.SchemaProviders;

/// <summary>
/// Represents a provider-neutral database index.
/// </summary>
public sealed class DatabaseIndexSchema {
    /// <summary>
    /// Gets or sets the schema name.
    /// </summary>
    public string SchemaName { get; set; } = "";

    /// <summary>
    /// Gets or sets the owning object name.
    /// </summary>
    public string ObjectName { get; set; } = "";

    /// <summary>
    /// Gets or sets the owning object type.
    /// </summary>
    public string ObjectType { get; set; } = "";

    /// <summary>
    /// Gets or sets the index name.
    /// </summary>
    public string IndexName { get; set; } = "";

    /// <summary>
    /// Gets or sets whether the index is the primary key.
    /// </summary>
    public string IsPrimaryKey { get; set; } = "";

    /// <summary>
    /// Gets or sets whether the index is clustered.
    /// </summary>
    public string IsClustered { get; set; } = "";

    /// <summary>
    /// Gets or sets whether the index is unique.
    /// </summary>
    public string IsUnique { get; set; } = "";

    /// <summary>
    /// Gets or sets whether the index represents a foreign key.
    /// </summary>
    public string IsForeignKey { get; set; } = "";

    /// <summary>
    /// Gets or sets the indexed columns.
    /// </summary>
    public string Columns { get; set; } = "";

    /// <summary>
    /// Gets or sets any non-key or referenced columns.
    /// </summary>
    public string OtherColumns { get; set; } = "";

    /// <summary>
    /// Gets the owning object key.
    /// </summary>
    public DatabaseObjectKey ObjectKey => new(SchemaName, ObjectName, ObjectType);
}
