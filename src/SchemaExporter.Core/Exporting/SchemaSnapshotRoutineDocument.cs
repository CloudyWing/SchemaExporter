#nullable enable
namespace CloudyWing.SchemaExporter.Exporting;
public sealed class SchemaSnapshotRoutineDocument {
    public string SchemaName { get; init; } = "";
    public string ContainerName { get; init; } = "";
    public string RoutineName { get; init; } = "";
    public string RoutineType { get; init; } = "";
    public string OverloadIdentifier { get; init; } = "";
    public string ParameterSignature { get; init; } = "";
    public string ReturnType { get; init; } = "";
    public string RoutineDescription { get; init; } = "";
    public string RoutineDefinition { get; init; } = "";
}
