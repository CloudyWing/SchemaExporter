#nullable enable

namespace CloudyWing.SchemaExporter.SchemaProviders;

public sealed class DatabaseObjectSchema {
    public string SchemaName { get; set; } = "";

    public string ObjectName { get; set; } = "";

    public string ObjectType { get; set; } = "";

    public string ObjectDescription { get; set; } = "";

    public DatabaseObjectKey ObjectKey => new(SchemaName, ObjectName, ObjectType);
}
