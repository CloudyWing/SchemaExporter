namespace CloudyWing.SchemaExporter {
    internal sealed class TableInfo {
        public string SchemaName { get; set; }

        public string TableName { get; set; }

        public string SheeterName => TableName.Length > 31
                ? TableName[..31]
                : TableName;

        public string TableType { get; set; }

        public string TableDescription { get; set; }
    }
}
