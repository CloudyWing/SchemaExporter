namespace CloudyWing.SchemaExporter.Core.Exporting;

/// <summary>
/// 表示匯出過程中發出的診斷訊息。
/// </summary>
public sealed class ExportDiagnostic {
    /// <summary>
    /// 取得診斷的嚴重性層級。
    /// </summary>
    public DiagnosticSeverity Severity { get; init; } = DiagnosticSeverity.Info;

    /// <summary>
    /// 取得診斷類別。
    /// </summary>
    public ExportDiagnosticCategory Category { get; init; } = ExportDiagnosticCategory.General;

    /// <summary>
    /// 取得與診斷相關的支援層級（適用時）。
    /// </summary>
    public ExportSupportLevel? SupportLevel { get; init; }

    /// <summary>
    /// 取得診斷訊息。
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// 取得受影響的資料庫物件（適用時）。
    /// </summary>
    public string? AffectedObject { get; init; }

    /// <summary>
    /// 取得用於顯示的本地化嚴重性文字。
    /// </summary>
    public string SeverityText => Severity switch {
        DiagnosticSeverity.Info => "資訊",
        DiagnosticSeverity.Warning => "警告",
        DiagnosticSeverity.Error => "錯誤",
        _ => Severity.ToString()
    };

    /// <summary>
    /// 取得用於顯示的本地化支援層級文字。
    /// </summary>
    public string SupportLevelText => SupportLevel switch {
        ExportSupportLevel.Full => "完整支援",
        ExportSupportLevel.Partial => "部分支援",
        ExportSupportLevel.Unsupported => "不支援",
        _ => ""
    };

    /// <summary>
    /// 取得用於顯示的受影響物件名稱；當 <see cref="AffectedObject"/> 為 <see langword="null"/> 時傳回空字串。
    /// </summary>
    public string AffectedObjectDisplay => AffectedObject ?? "";

    /// <summary>
    /// 取得用於顯示的本地化類別文字。
    /// </summary>
    public string CategoryText => Category switch {
        ExportDiagnosticCategory.General => "一般",
        ExportDiagnosticCategory.Filtering => "篩選",
        ExportDiagnosticCategory.Naming => "命名",
        ExportDiagnosticCategory.ViewSupport => "檢視表支援",
        ExportDiagnosticCategory.Configuration => "設定",
        ExportDiagnosticCategory.RoutineSupport => "程序支援",
        ExportDiagnosticCategory.Execution => "執行",
        ExportDiagnosticCategory.Redaction => "Redaction",
        _ => Category.ToString()
    };
}

