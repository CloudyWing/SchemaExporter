namespace CloudyWing.SchemaExporter.Cli;

/// <summary>
/// 定義 CLI 命令的處理結果代碼。
/// </summary>
internal enum CliExitCode {
    /// <summary>
    /// 命令成功完成，或使用者要求顯示說明。
    /// </summary>
    Success = 0,

    /// <summary>
    /// 命令列引數無法解析。
    /// </summary>
    ArgumentError = 1,

    /// <summary>
    /// 匯出或 diff 工作流程發生可預期錯誤。
    /// </summary>
    WorkflowError = 2,

    /// <summary>
    /// 命令執行期間發生未預期錯誤。
    /// </summary>
    UnexpectedError = 3,

    /// <summary>
    /// 使用者以 Ctrl+C 取消命令；已寫出的檔案可能不完整，不應視為成功結果。
    /// </summary>
    Canceled = 4
}
