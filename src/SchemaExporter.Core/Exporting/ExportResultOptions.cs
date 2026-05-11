namespace CloudyWing.SchemaExporter.Core.Exporting;

/// <summary>
/// 設定輸出檔案命名及匯出後動作。
/// </summary>
public sealed class ExportResultOptions {
    /// <summary>
    /// 取得或設定是否在檔名中附加時間戳記。
    /// </summary>
    public bool UseTimestamp { get; set; }

    /// <summary>
    /// 取得或設定當 <see cref="UseTimestamp"/> 為 true 時所使用的時間戳記格式。
    /// 預設值為 "yyyyMMdd_HHmmss"。
    /// </summary>
    public string TimestampFormat { get; set; } = "yyyyMMdd_HHmmss";

    /// <summary>
    /// 取得或設定當檔案已存在時的覆寫策略。
    /// </summary>
    public OverwriteStrategy OverwriteStrategy { get; set; } = OverwriteStrategy.Overwrite;

    /// <summary>
    /// 取得或設定是否在匯出完成後開啟輸出資料夾。
    /// </summary>
    public bool OpenOutputFolder { get; set; }

    /// <summary>
    /// 取得或設定是否產生描述匯出內容的 manifest 檔案。
    /// </summary>
    public bool GenerateManifest { get; set; }

    /// <summary>
    /// 取得或設定是否產生包含匯出結構描述及選用差異比對資料的 JSON 附屬檔案。
    /// </summary>
    public bool GenerateJsonSidecar { get; set; }

    /// <summary>
    /// 取得或設定是否產生包含匯出結構描述及選用差異比對摘要的 Markdown 附屬檔案。
    /// </summary>
    public bool GenerateMarkdownSidecar { get; set; }

    /// <summary>
    /// 取得或設定是否產生供 AI Agent 讀取的 Markdown context 檔案。
    /// </summary>
    public bool GenerateAiContext { get; set; }

    /// <summary>
    /// 取得或設定是否寫入可重複使用的結構描述快照 JSON 檔案。
    /// </summary>
    public bool GenerateSchemaSnapshot { get; set; }

    /// <summary>
    /// 取得或設定用於差異比對產生的基準結構描述快照絕對路徑。
    /// </summary>
    public string? DiffSourceSnapshotPath { get; set; }
}

