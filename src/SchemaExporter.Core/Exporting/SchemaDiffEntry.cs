#nullable enable
namespace CloudyWing.SchemaExporter.Exporting;
public sealed class SchemaDiffEntry {
    public SchemaChangeType ChangeType { get; init; }
    public string Identifier { get; init; } = "";
    public Dictionary<string, SchemaValueChange> PropertyChanges { get; init; } = [];
}
