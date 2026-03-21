#nullable enable

namespace CloudyWing.SchemaExporter.SchemaProviders;

/// <summary>
/// Represents the provider-agnostic schema data used by the export layer.
/// </summary>
public sealed class DatabaseSchemaExport {
    /// <summary>
    /// Gets the exported database objects.
    /// </summary>
    public IReadOnlyList<DatabaseObjectSchema> Objects { get; init; } = [];

    /// <summary>
    /// Gets the exported columns.
    /// </summary>
    public IReadOnlyList<DatabaseColumnSchema> Columns { get; init; } = [];

    /// <summary>
    /// Gets the exported indexes.
    /// </summary>
    public IReadOnlyList<DatabaseIndexSchema> Indexes { get; init; } = [];

    /// <summary>
    /// Gets the exported routines.
    /// </summary>
    public IReadOnlyList<DatabaseRoutineSchema> Routines { get; init; } = [];
}
