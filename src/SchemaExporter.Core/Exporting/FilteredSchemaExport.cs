using CloudyWing.SchemaExporter.Core.SchemaProviders;

namespace CloudyWing.SchemaExporter.Core.Exporting;

internal sealed class FilteredSchemaExport {
    /// <summary>
    /// Gets the filtered database objects.
    /// </summary>
    public IReadOnlyList<DatabaseObjectSchema> Objects { get; init; } = [];

    /// <summary>
    /// Gets the filtered database columns.
    /// </summary>
    public IReadOnlyList<DatabaseColumnSchema> Columns { get; init; } = [];

    /// <summary>
    /// Gets the filtered database indexes.
    /// </summary>
    public IReadOnlyList<DatabaseIndexSchema> Indexes { get; init; } = [];

    /// <summary>
    /// Gets the filtered database routines.
    /// </summary>
    public IReadOnlyList<DatabaseRoutineSchema> Routines { get; init; } = [];
}

