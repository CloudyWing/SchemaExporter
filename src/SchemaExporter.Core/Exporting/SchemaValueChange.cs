namespace CloudyWing.SchemaExporter.Core.Exporting;

/// <summary>
/// 表示單一屬性在差異文件中的前後值。
/// </summary>
public sealed class SchemaValueChange {
    /// <summary>
    /// 取得或設定異動前的值。
    /// </summary>
    public string? Previous { get; init; }

    /// <summary>
    /// 取得或設定異動後的值。
    /// </summary>
    public string? Current { get; init; }
}

