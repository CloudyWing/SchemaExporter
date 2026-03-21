namespace CloudyWing.SchemaExporter {
    internal sealed class ColumnInfo {
        public string SchemaName { get; init; } = "";

        public string TableName { get; init; } = "";

        public string ColumnName { get; init; } = "";

        public string ColumnType { get; init; } = "";

        public string IsNullable { get; init; } = "";

        public string ColumnDefault { get; init; } = "";

        public string IsPrimaryKey { get; init; } = "";

        public string IsIdentity { get; init; } = "";

        public string ColumnDescription { get; init; } = "";

        public int ColumnOrder { get; init; }
    }
}
