namespace CloudyWing.SchemaExporter.Core.Exporting;

/// <summary>
/// 依工作流程區域分類診斷。
/// </summary>
public enum ExportDiagnosticCategory {
    /// <summary>
    /// 一般匯出資訊。
    /// </summary>
    General = 0,

    /// <summary>
    /// 篩選相關資訊。
    /// </summary>
    Filtering = 1,

    /// <summary>
    /// 檔案或工作表命名調整。
    /// </summary>
    Naming = 2,

    /// <summary>
    /// 檢視表支援層級資訊。
    /// </summary>
    ViewSupport = 3,

    /// <summary>
    /// 連線/設定解析資訊。
    /// </summary>
    Configuration = 4,

    /// <summary>
    /// 程序文件支援資訊。
    /// </summary>
    RoutineSupport = 5,

    /// <summary>
    /// 執行時間與摘要資訊。
    /// </summary>
    Execution = 6,

    /// <summary>
    /// 敏感 metadata 遮罩資訊。
    /// </summary>
    Redaction = 7
}

