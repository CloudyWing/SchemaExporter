#nullable enable

namespace CloudyWing.SchemaExporter.SchemaProviders;

/// <summary>
/// Identifies a database routine using schema, container, name, type, and overload.
/// </summary>
/// <param name="SchemaName">The database schema name.</param>
/// <param name="ContainerName">The optional containing package or object name.</param>
/// <param name="RoutineName">The routine name.</param>
/// <param name="RoutineType">The provider-specific routine type.</param>
/// <param name="OverloadIdentifier">The overload identifier, if any.</param>
public readonly record struct DatabaseRoutineKey(
    string SchemaName,
    string ContainerName,
    string RoutineName,
    string RoutineType,
    string OverloadIdentifier
);
