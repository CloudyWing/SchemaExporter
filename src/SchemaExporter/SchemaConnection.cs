#nullable enable

namespace CloudyWing.SchemaExporter;

public class SchemaConnection {
    public string Name { get; set; } = "";

    public DatabaseType DatabaseType { get; set; } = DatabaseType.SqlServer;

    public string ConnectionString { get; set; } = "";
}
