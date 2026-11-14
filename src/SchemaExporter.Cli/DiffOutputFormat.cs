namespace CloudyWing.SchemaExporter.Cli;

/// <summary>
/// 定義 diff 命令的輸出檔案格式。
/// </summary>
internal enum DiffOutputFormat {
    /// <summary>
    /// 以 JSON 格式輸出差異報告。
    /// </summary>
    Json,

    /// <summary>
    /// 以 Markdown 格式輸出差異報告。
    /// </summary>
    Markdown
}
