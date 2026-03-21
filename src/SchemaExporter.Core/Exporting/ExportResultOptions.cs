namespace CloudyWing.SchemaExporter.Exporting;

/// <summary>
/// Configures output file naming and post-export actions.
/// </summary>
public sealed class ExportResultOptions {
    /// <summary>
    /// Gets or sets whether to append timestamp to the filename.
    /// </summary>
    public bool UseTimestamp { get; set; } = false;

    /// <summary>
    /// Gets or sets the timestamp format when <see cref="UseTimestamp"/> is true.
    /// Default is "yyyyMMdd_HHmmss".
    /// </summary>
    public string TimestampFormat { get; set; } = "yyyyMMdd_HHmmss";

    /// <summary>
    /// Gets or sets the overwrite strategy when a file already exists.
    /// </summary>
    public OverwriteStrategy OverwriteStrategy { get; set; } = OverwriteStrategy.Overwrite;

    /// <summary>
    /// Gets or sets whether to open the output folder after export completes.
    /// </summary>
    public bool OpenOutputFolder { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to generate a manifest file describing the export.
    /// </summary>
    public bool GenerateManifest { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to generate a JSON sidecar containing the exported schema and optional diff data.
    /// </summary>
    public bool GenerateJsonSidecar { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to generate a Markdown sidecar containing the exported schema and optional diff summary.
    /// </summary>
    public bool GenerateMarkdownSidecar { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to write a reusable schema snapshot JSON file.
    /// </summary>
    public bool GenerateSchemaSnapshot { get; set; } = false;

    /// <summary>
    /// Gets or sets the absolute path to a baseline schema snapshot used for diff generation.
    /// </summary>
    public string? DiffSourceSnapshotPath { get; set; }
}
