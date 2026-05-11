namespace CloudyWing.SchemaExporter.Core.Exporting;

/// <summary>
/// 表示匯出執行時對預設輸出選項的覆寫值。
/// </summary>
public sealed class ExportOptionOverrides {
    /// <summary>
    /// 取得或設定匯出輸出的基底目錄路徑覆寫值。
    /// </summary>
    public string? OutputPath { get; init; }

    /// <summary>
    /// 取得或設定是否產生 manifest 檔案的覆寫值。
    /// </summary>
    public bool? GenerateManifest { get; init; }

    /// <summary>
    /// 取得或設定是否產生 JSON sidecar 檔案的覆寫值。
    /// </summary>
    public bool? GenerateJsonSidecar { get; init; }

    /// <summary>
    /// 取得或設定是否產生 Markdown sidecar 檔案的覆寫值。
    /// </summary>
    public bool? GenerateMarkdownSidecar { get; init; }

    /// <summary>
    /// 取得或設定是否產生 Schema Summary 檔案的覆寫值。
    /// </summary>
    public bool? GenerateSchemaSummary { get; init; }

    /// <summary>
    /// 取得或設定是否產生 schema snapshot 檔案的覆寫值。
    /// </summary>
    public bool? GenerateSchemaSnapshot { get; init; }

    /// <summary>
    /// 取得或設定是否在匯出完成後開啟輸出資料夾的覆寫值。
    /// </summary>
    public bool? OpenOutputFolder { get; init; }

    /// <summary>
    /// 取得或設定是否在檔名中附加時間戳記的覆寫值。
    /// </summary>
    public bool? UseTimestamp { get; init; }

    /// <summary>
    /// 取得或設定差異比對基準 snapshot 路徑的覆寫值。
    /// </summary>
    public string? DiffSourceSnapshotPath { get; init; }

    /// <summary>
    /// 取得或設定一個值，用以指出是否套用 <see cref="DiffSourceSnapshotPath"/> 覆寫。
    /// </summary>
    public bool OverrideDiffSourceSnapshotPath { get; init; }
}
