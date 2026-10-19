namespace CloudyWing.SchemaExporter.Core.IntegrationTests.Infrastructure;

/// <summary>
/// 表示在測試容器內以命令列工具執行 SQL 檔案的結果。
/// </summary>
/// <param name="ExitCode">命令列工具的結束代碼。</param>
/// <param name="Stdout">標準輸出。</param>
/// <param name="Stderr">標準錯誤輸出。</param>
internal sealed record ScriptExecutionResult(long ExitCode, string Stdout, string Stderr) {
    /// <summary>
    /// 取得結束代碼與輸出的合併文字，供斷言失敗時顯示。
    /// </summary>
    public string Describe() {
        return $"exit code {ExitCode}{Environment.NewLine}stdout:{Environment.NewLine}{Stdout}{Environment.NewLine}stderr:{Environment.NewLine}{Stderr}";
    }
}
