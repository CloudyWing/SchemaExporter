namespace CloudyWing.SchemaExporter {
    internal sealed class IndexInfo {
        public string TableName { get; set; }

        public string IndexName { get; set; }

        public string IsPrimaryKey { get; set; }

        public string IsClustered { get; set; }

        public string IsUnique { get; set; }

        public string IsForeignKey { get; set; }

        public string Columns { get; set; }

        public string OtherColumns { get; set; }
    }
}
