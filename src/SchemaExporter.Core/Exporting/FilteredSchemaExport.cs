using CloudyWing.SchemaExporter.SchemaProviders;

namespace CloudyWing.SchemaExporter.Exporting;

internal sealed class FilteredSchemaExport {
    public List<DatabaseObjectSchema> Objects { get; init; } = [];
    public List<DatabaseColumnSchema> Columns { get; init; } = [];
    public List<DatabaseIndexSchema> Indexes { get; init; } = [];
    public List<DatabaseRoutineSchema> Routines { get; init; } = [];
}
