namespace CloudyWing.SchemaExporter.Core.Exporting;

/// <summary>
/// 定義處理現有匯出檔案的策略。
/// </summary>
public enum OverwriteStrategy {
    /// <summary>
    /// 不提示直接覆寫現有檔案。
    /// </summary>
    Overwrite = 0,

    /// <summary>
    /// 附加數字後綴以建立唯一的檔案名稱。
    /// </summary>
    AppendSuffix = 1,

    /// <summary>
    /// 若檔案已存在則使匯出失敗。
    /// </summary>
    Fail = 2
}

