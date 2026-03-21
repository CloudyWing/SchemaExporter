#nullable enable

namespace CloudyWing.SchemaExporter.SchemaProviders;

/// <summary>
/// Represents a provider-neutral database object.
/// </summary>
public sealed class DatabaseObjectSchema {
    /// <summary>
    /// Gets or sets the schema name.
    /// </summary>
    public string SchemaName { get; set; } = "";

    /// <summary>
    /// Gets or sets the object name.
    /// </summary>
    public string ObjectName { get; set; } = "";

    /// <summary>
    /// Gets or sets the object type.
    /// </summary>
    public string ObjectType { get; set; } = "";

    /// <summary>
    /// Gets or sets the object description.
    /// </summary>
    public string ObjectDescription { get; set; } = "";

    /// <summary>
    /// Gets the object key.
    /// </summary>
    public DatabaseObjectKey ObjectKey => new(SchemaName, ObjectName, ObjectType);
}
