#nullable enable

namespace CloudyWing.SchemaExporter.SchemaProviders;

/// <summary>
/// Represents a provider-neutral database column.
/// </summary>
public sealed class DatabaseColumnSchema {
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
    /// Gets or sets the column name.
    /// </summary>
    public string ColumnName { get; set; } = "";

    /// <summary>
    /// Gets or sets the provider-specific column type.
    /// </summary>
    public string ColumnType { get; set; } = "";

    /// <summary>
    /// Gets or sets whether the column is nullable.
    /// </summary>
    public string IsNullable { get; set; } = "";

    /// <summary>
    /// Gets or sets the column default expression.
    /// </summary>
    public string ColumnDefault { get; set; } = "";

    /// <summary>
    /// Gets or sets whether the column is part of the primary key.
    /// </summary>
    public string IsPrimaryKey { get; set; } = "";

    /// <summary>
    /// Gets or sets whether the column is an identity column.
    /// </summary>
    public string IsIdentity { get; set; } = "";

    /// <summary>
    /// Gets or sets the column description.
    /// </summary>
    public string ColumnDescription { get; set; } = "";

    /// <summary>
    /// Gets or sets the ordinal position of the column.
    /// </summary>
    public int ColumnOrder { get; set; }

    /// <summary>
    /// Gets the owning object key.
    /// </summary>
    public DatabaseObjectKey ObjectKey => new(SchemaName, ObjectName, ObjectType);
}
