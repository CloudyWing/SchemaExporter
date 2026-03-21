namespace CloudyWing.SchemaExporter.Core.Exporting;

/// <summary>
/// 定義匯出作業的各個階段。
/// </summary>
public enum ExportStage {
    /// <summary>
    /// 驗證連線與匯出設定。
    /// </summary>
    Validating = 0,

    /// <summary>
    /// 從資料庫載入結構描述中繼資料。
    /// </summary>
    LoadingSchema = 1,

    /// <summary>
    /// 對已載入的結構描述套用篩選條件。
    /// </summary>
    ApplyingFilters = 2,

    /// <summary>
    /// 建立試算表工作表並寫入檔案。
    /// </summary>
    GeneratingExport = 3,

    /// <summary>
    /// 寫入 manifest 並執行匯出後動作。
    /// </summary>
    Finalizing = 4,

    /// <summary>
    /// 匯出已成功完成。
    /// </summary>
    Completed = 5
}

