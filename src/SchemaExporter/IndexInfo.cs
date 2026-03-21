namespace CloudyWing.SchemaExporter {
    internal sealed class IndexInfo {
        public string SchemaName { get; init; } = "";

        public string TableName { get; init; } = "";

        public string IndexName { get; init; } = "";

        public string IsPrimaryKey { get; init; } = "";

        public string IsClustered { get; init; } = "";

        public string IsUnique { get; init; } = "";

        public string IsForeignKey { get; init; } = "";

        public string Columns { get; init; } = "";

        public string OtherColumns { get; init; } = "";
    }
}
