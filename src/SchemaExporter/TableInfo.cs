namespace CloudyWing.SchemaExporter {
    internal sealed class TableInfo {
        public string SchemaName { get; init; } = "";

        public string TableName { get; init; } = "";

        public string SheeterName => TableName.Length > 31
                ? TableName[..31]
                : TableName;

        public string TableType { get; init; } = "";

        public string TableDescription { get; init; } = "";
    }
}
