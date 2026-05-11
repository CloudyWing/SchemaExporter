namespace CloudyWing.SchemaExporter.Core.Exporting;

/// <summary>
/// 表示成功匯出作業的結果。
/// </summary>
public sealed class ExportResult {
    /// <summary>
    /// 取得產生的匯出檔案路徑。
    /// </summary>
    public required string OutputFilePath { get; init; }

    /// <summary>
    /// 取得產生的 manifest 檔案路徑（若已建立）。
    /// </summary>
    public string? ManifestFilePath { get; init; }

    /// <summary>
    /// 取得產生的 JSON 附屬檔案路徑（若已建立）。
    /// </summary>
    public string? JsonSidecarFilePath { get; init; }

    /// <summary>
    /// 取得產生的 Markdown 附屬檔案路徑（若已建立）。
    /// </summary>
    public string? MarkdownSidecarFilePath { get; init; }

    /// <summary>
    /// 取得產生的 AI context 檔案路徑（若已建立）。
    /// </summary>
    public string? AiContextFilePath { get; init; }

    /// <summary>
    /// 取得產生的結構描述快照檔案路徑（若已建立）。
    /// </summary>
    public string? SnapshotFilePath { get; init; }

    /// <summary>
    /// 取得產生的結構描述差異比對檔案路徑（若已建立）。
    /// </summary>
    public string? DiffFilePath { get; init; }

    /// <summary>
    /// 取得輸出目錄路徑。
    /// </summary>
    public string OutputDirectoryPath => Path.GetDirectoryName(OutputFilePath) ?? "";

    /// <summary>
    /// 取得匯出所使用的連線名稱。
    /// </summary>
    public required string ConnectionName { get; init; }

    /// <summary>
    /// 取得匯出所使用的設定檔名稱。
    /// </summary>
    public required string ProfileName { get; init; }

    /// <summary>
    /// 取得匯出期間收集的診斷資訊。
    /// </summary>
    public required IReadOnlyList<ExportDiagnostic> Diagnostics { get; init; }
}

