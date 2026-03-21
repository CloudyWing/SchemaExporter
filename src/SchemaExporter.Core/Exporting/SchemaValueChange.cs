#nullable enable
namespace CloudyWing.SchemaExporter.Exporting;
public sealed class SchemaValueChange {
    public string? Previous { get; init; }
    public string? Current { get; init; }
}
