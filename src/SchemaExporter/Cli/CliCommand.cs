namespace CloudyWing.SchemaExporter.Cli;

/// <summary>
/// 定義 CLI 支援的子命令類型。
/// </summary>
internal enum CliCommand {
    /// <summary>
    /// 匯出 schema 至 Excel 活頁簿。
    /// </summary>
    Export,

    /// <summary>
    /// 比對兩個 schema snapshot 並輸出差異報告。
    /// </summary>
    Diff
}