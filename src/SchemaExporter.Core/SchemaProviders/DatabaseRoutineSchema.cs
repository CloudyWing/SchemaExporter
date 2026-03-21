#nullable enable

namespace CloudyWing.SchemaExporter.SchemaProviders;

/// <summary>
/// Represents a provider-neutral stored routine.
/// </summary>
public sealed class DatabaseRoutineSchema {
    /// <summary>
    /// Gets or sets the schema name.
    /// </summary>
    public string SchemaName { get; set; } = "";

    /// <summary>
    /// Gets or sets the optional containing package or object name.
    /// </summary>
    public string ContainerName { get; set; } = "";

    /// <summary>
    /// Gets or sets the routine name.
    /// </summary>
    public string RoutineName { get; set; } = "";

    /// <summary>
    /// Gets or sets the routine type.
    /// </summary>
    public string RoutineType { get; set; } = "";

    /// <summary>
    /// Gets or sets the overload identifier.
    /// </summary>
    public string OverloadIdentifier { get; set; } = "";

    /// <summary>
    /// Gets or sets the formatted parameter signature.
    /// </summary>
    public string ParameterSignature { get; set; } = "";

    /// <summary>
    /// Gets or sets the return type.
    /// </summary>
    public string ReturnType { get; set; } = "";

    /// <summary>
    /// Gets or sets the routine description.
    /// </summary>
    public string RoutineDescription { get; set; } = "";

    /// <summary>
    /// Gets or sets the routine definition.
    /// </summary>
    public string RoutineDefinition { get; set; } = "";

    /// <summary>
    /// Gets the routine key.
    /// </summary>
    public DatabaseRoutineKey RoutineKey => new(
        SchemaName,
        ContainerName,
        RoutineName,
        RoutineType,
        OverloadIdentifier
    );

    /// <summary>
    /// Gets the schema-qualified routine name.
    /// </summary>
    public string QualifiedName => string.IsNullOrWhiteSpace(ContainerName)
        ? $"{SchemaName}.{RoutineName}"
        : $"{SchemaName}.{ContainerName}.{RoutineName}";
}
