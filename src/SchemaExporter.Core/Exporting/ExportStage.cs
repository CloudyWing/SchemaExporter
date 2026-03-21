namespace CloudyWing.SchemaExporter.Exporting;

/// <summary>
/// Defines the stages of an export operation.
/// </summary>
public enum ExportStage {
    /// <summary>
    /// Validating connection and export settings.
    /// </summary>
    Validating = 0,

    /// <summary>
    /// Loading schema metadata from the database.
    /// </summary>
    LoadingSchema = 1,

    /// <summary>
    /// Applying filters to the loaded schema.
    /// </summary>
    ApplyingFilters = 2,

    /// <summary>
    /// Building spreadsheet sheets and writing to file.
    /// </summary>
    GeneratingExport = 3,

    /// <summary>
    /// Writing manifest and performing post-export actions.
    /// </summary>
    Finalizing = 4,

    /// <summary>
    /// Export completed successfully.
    /// </summary>
    Completed = 5
}
