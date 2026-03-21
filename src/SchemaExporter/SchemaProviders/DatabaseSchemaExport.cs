#nullable enable

namespace CloudyWing.SchemaExporter.SchemaProviders;

/// <summary>
/// Represents the provider-agnostic schema data used by the export layer.
/// </summary>
public sealed class DatabaseSchemaExport {
    public IReadOnlyList<DatabaseObjectSchema> Objects { get; init; } = [];

    public IReadOnlyList<DatabaseColumnSchema> Columns { get; init; } = [];

    public IReadOnlyList<DatabaseIndexSchema> Indexes { get; init; } = [];
}
