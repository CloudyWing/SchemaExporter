namespace CloudyWing.SchemaExporter.Exporting;

internal sealed class ExportExecutionSummary {
    public TimeSpan ValidationDuration { get; set; }
    public TimeSpan SchemaLoadDuration { get; set; }
    public TimeSpan FilteringDuration { get; set; }
    public TimeSpan WorkbookDuration { get; set; }
    public TimeSpan ArtifactDuration { get; set; }
    public TimeSpan TotalDuration { get; set; }
}
