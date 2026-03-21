#nullable enable

namespace CloudyWing.SchemaExporter.SchemaProviders;

/// <summary>
/// Identifies a database object using schema, name, and type.
/// </summary>
/// <param name="SchemaName">The database schema name.</param>
/// <param name="ObjectName">The database object name.</param>
/// <param name="ObjectType">The provider-specific object type.</param>
public readonly record struct DatabaseObjectKey(string SchemaName, string ObjectName, string ObjectType);
