namespace CloudyWing.SchemaExporter.Core.Exporting;

/// <summary>
/// 表示一次匯出作業的輸出規劃，包含目標檔案路徑。
/// </summary>
/// <param name="FilePath">輸出檔案的完整路徑。</param>
internal readonly record struct OutputPlan(string FilePath);

