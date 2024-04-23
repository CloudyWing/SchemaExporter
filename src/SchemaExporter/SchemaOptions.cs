namespace CloudyWing.SchemaExporter {
    public class SchemaOptions {
        public const string OptionsName = "Schema";

        public string ExportPath { get; set; }

        public List<SchemaConnection> Connections { get; set; } = [];
    }
}
