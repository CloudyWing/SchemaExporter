namespace CloudyWing.SchemaExporter {
    internal sealed class ColumnInfo {
        public string TableName { get; set; }

        public string ColumnName { get; set; }

        public string ColumnType { get; set; }

        public string IsNullable { get; set; }

        public string ColumnDefault { get; set; }

        public string IsPrimaryKey { get; set; }

        public string IsIdentity { get; set; }

        public string ColumnDescription { get; set; }
    }
}
