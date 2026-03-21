#nullable enable

using System.IO;

namespace CloudyWing.SchemaExporter.Exporting;

/// <summary>
/// Contains the outcome of a successful export operation.
/// </summary>
public sealed class ExportResult {
    /// <summary>
    /// Gets the path to the generated export file.
    /// </summary>
    public string OutputFilePath { get; init; } = "";

    /// <summary>
    /// Gets the generated manifest file path, if created.
    /// </summary>
    public string? ManifestFilePath { get; init; }

    /// <summary>
    /// Gets the generated JSON sidecar file path, if created.
    /// </summary>
    public string? JsonSidecarFilePath { get; init; }

    /// <summary>
    /// Gets the generated Markdown sidecar file path, if created.
    /// </summary>
    public string? MarkdownSidecarFilePath { get; init; }

    /// <summary>
    /// Gets the generated schema snapshot file path, if created.
    /// </summary>
    public string? SnapshotFilePath { get; init; }

    /// <summary>
    /// Gets the generated schema diff file path, if created.
    /// </summary>
    public string? DiffFilePath { get; init; }

    /// <summary>
    /// Gets the output directory path.
    /// </summary>
    public string OutputDirectoryPath => Path.GetDirectoryName(OutputFilePath) ?? "";

    /// <summary>
    /// Gets the connection name used for the export.
    /// </summary>
    public string ConnectionName { get; init; } = "";

    /// <summary>
    /// Gets the export profile name used for the export.
    /// </summary>
    public string ProfileName { get; init; } = "";

    /// <summary>
    /// Gets the diagnostics collected during export.
    /// </summary>
    public IReadOnlyList<ExportDiagnostic> Diagnostics { get; init; } = [];
}
