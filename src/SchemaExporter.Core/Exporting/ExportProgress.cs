namespace CloudyWing.SchemaExporter.Exporting;

/// <summary>
/// Reports incremental export progress to the UI.
/// </summary>
public sealed class ExportProgress {
    /// <summary>
    /// Gets or sets the current export stage.
    /// </summary>
    public ExportStage Stage { get; set; }

    /// <summary>
    /// Gets or sets the current progress message.
    /// </summary>
    public string Message { get; set; } = "";

    /// <summary>
    /// Gets or sets the percentage complete (0-100), or null if indeterminate.
    /// </summary>
    public int? PercentComplete { get; set; }
}
