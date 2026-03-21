namespace CloudyWing.SchemaExporter.Core.Exporting;

/// <summary>
/// 表示匯出流程產生的附加產物路徑集合。
/// </summary>
internal sealed class ArtifactOutputs {
    /// <summary>
    /// 取得或設定 manifest 檔案路徑。
    /// </summary>
    public string? ManifestFilePath { get; init; }

    /// <summary>
    /// 取得或設定 JSON sidecar 檔案路徑。
    /// </summary>
    public string? JsonSidecarFilePath { get; init; }

    /// <summary>
    /// 取得或設定 Markdown sidecar 檔案路徑。
    /// </summary>
    public string? MarkdownSidecarFilePath { get; init; }

    /// <summary>
    /// 取得或設定 schema snapshot 檔案路徑。
    /// </summary>
    public string? SnapshotFilePath { get; init; }

    /// <summary>
    /// 取得或設定 schema diff 檔案路徑。
    /// </summary>
    public string? DiffFilePath { get; init; }
}

