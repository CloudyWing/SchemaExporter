#nullable enable

namespace CloudyWing.SchemaExporter.Exporting;

/// <summary>
/// Represents a diagnostic message emitted during export.
/// </summary>
public sealed class ExportDiagnostic {
    /// <summary>
    /// Gets the severity level of the diagnostic.
    /// </summary>
    public DiagnosticSeverity Severity { get; init; } = DiagnosticSeverity.Info;

    /// <summary>
    /// Gets the diagnostic category.
    /// </summary>
    public ExportDiagnosticCategory Category { get; init; } = ExportDiagnosticCategory.General;

    /// <summary>
    /// Gets the support level associated with the diagnostic, when applicable.
    /// </summary>
    public ExportSupportLevel? SupportLevel { get; init; }

    /// <summary>
    /// Gets the diagnostic message.
    /// </summary>
    public string Message { get; init; } = "";

    /// <summary>
    /// Gets the affected database object, if applicable.
    /// </summary>
    public string? AffectedObject { get; init; }

    /// <summary>
    /// Gets the localized severity text for display.
    /// </summary>
    public string SeverityText => Severity switch {
        DiagnosticSeverity.Info => "資訊",
        DiagnosticSeverity.Warning => "警告",
        _ => Severity.ToString()
    };

    /// <summary>
    /// Gets the localized support level text for display.
    /// </summary>
    public string SupportLevelText => SupportLevel switch {
        ExportSupportLevel.Full => "完整支援",
        ExportSupportLevel.Partial => "部分支援",
        ExportSupportLevel.Unsupported => "不支援",
        _ => ""
    };

    /// <summary>
    /// Gets the localized category text for display.
    /// </summary>
    public string CategoryText => Category switch {
        ExportDiagnosticCategory.General => "一般",
        ExportDiagnosticCategory.Filtering => "篩選",
        ExportDiagnosticCategory.Naming => "命名",
        ExportDiagnosticCategory.ViewSupport => "檢視表支援",
        ExportDiagnosticCategory.Configuration => "設定",
        ExportDiagnosticCategory.RoutineSupport => "程序支援",
        ExportDiagnosticCategory.Execution => "執行",
        _ => Category.ToString()
    };
}
