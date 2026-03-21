#nullable enable

namespace CloudyWing.SchemaExporter.SchemaProviders;

public sealed class DatabaseColumnSchema {
    public string SchemaName { get; set; } = "";

    public string ObjectName { get; set; } = "";

    public string ObjectType { get; set; } = "";

    public string ColumnName { get; set; } = "";

    public string ColumnType { get; set; } = "";

    public string IsNullable { get; set; } = "";

    public string ColumnDefault { get; set; } = "";

    public string IsPrimaryKey { get; set; } = "";

    public string IsIdentity { get; set; } = "";

    public string ColumnDescription { get; set; } = "";

    public int ColumnOrder { get; set; }

    public DatabaseObjectKey ObjectKey => new(SchemaName, ObjectName, ObjectType);
}
