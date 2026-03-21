#nullable enable

namespace CloudyWing.SchemaExporter.SchemaProviders;

public sealed class DatabaseIndexSchema {
    public string SchemaName { get; set; } = "";

    public string ObjectName { get; set; } = "";

    public string ObjectType { get; set; } = "";

    public string IndexName { get; set; } = "";

    public string IsPrimaryKey { get; set; } = "";

    public string IsClustered { get; set; } = "";

    public string IsUnique { get; set; } = "";

    public string IsForeignKey { get; set; } = "";

    public string Columns { get; set; } = "";

    public string OtherColumns { get; set; } = "";

    public DatabaseObjectKey ObjectKey => new(SchemaName, ObjectName, ObjectType);
}
