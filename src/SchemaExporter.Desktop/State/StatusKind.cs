namespace CloudyWing.SchemaExporter.Desktop.State;

/// <summary>
/// 定義狀態訊息的呈現類型。
/// </summary>
public enum StatusKind {
    /// <summary>
    /// 一般資訊或進行中的狀態。
    /// </summary>
    Info,

    /// <summary>
    /// 作業成功完成。
    /// </summary>
    Success,

    /// <summary>
    /// 作業完成但需要使用者留意，或輸入需要修正。
    /// </summary>
    Warning,

    /// <summary>
    /// 作業失敗。
    /// </summary>
    Error
}
