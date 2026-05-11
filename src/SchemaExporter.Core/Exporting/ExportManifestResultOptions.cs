namespace CloudyWing.SchemaExporter.Core.Exporting;

/// <summary>
/// 表示 manifest 中記錄的結果選項快照。
/// </summary>
internal sealed class ExportManifestResultOptions {
    /// <summary>
    /// 取得或設定一個值，用以指出是否使用時間戳記。
    /// </summary>
    public bool UseTimestamp { get; init; }

    /// <summary>
    /// 取得或設定時間戳記格式。
    /// </summary>
    public required string TimestampFormat { get; init; }

    /// <summary>
    /// 取得或設定檔案覆寫策略名稱。
    /// </summary>
    public required string OverwriteStrategy { get; init; }

    /// <summary>
    /// 取得或設定一個值，用以指出是否在完成後開啟輸出資料夾。
    /// </summary>
    public bool OpenOutputFolder { get; init; }

    /// <summary>
    /// 取得或設定一個值，用以指出是否產生 manifest。
    /// </summary>
    public bool GenerateManifest { get; init; }

    /// <summary>
    /// 取得或設定一個值，用以指出是否產生 JSON sidecar。
    /// </summary>
    public bool GenerateJsonSidecar { get; init; }

    /// <summary>
    /// 取得或設定一個值，用以指出是否產生 Markdown sidecar。
    /// </summary>
    public bool GenerateMarkdownSidecar { get; init; }

    /// <summary>
    /// 取得或設定一個值，用以指出是否產生 Schema Summary。
    /// </summary>
    public bool GenerateSchemaSummary { get; init; }

    /// <summary>
    /// 取得或設定一個值，用以指出是否產生 schema snapshot。
    /// </summary>
    public bool GenerateSchemaSnapshot { get; init; }

    /// <summary>
    /// 取得或設定來源 snapshot 路徑。
    /// </summary>
    public string? DiffSourceSnapshotPath { get; init; }
}

