namespace CloudyWing.SchemaExporter.Core.Exporting;

/// <summary>
/// 定義結構描述匯出作業的篩選條件與偏好設定。
/// </summary>
public sealed class ExportProfile {
    /// <summary>
    /// 取得或設定顯示給使用者的設定檔名稱。
    /// </summary>
    public string Name { get; set; } = "Default";

    /// <summary>
    /// 取得要納入的結構描述名稱模式（空白表示全部納入）。
    /// 支援萬用字元：* 代表任意字元，? 代表單一字元。
    /// </summary>
    public IReadOnlyList<string> IncludeSchemas { get; init; } = [];

    /// <summary>
    /// 取得要排除的結構描述名稱模式。
    /// 排除規則在納入規則套用之後生效。
    /// </summary>
    public IReadOnlyList<string> ExcludeSchemas { get; init; } = [];

    /// <summary>
    /// 取得要納入的物件名稱模式（空白表示全部納入）。
    /// </summary>
    public IReadOnlyList<string> IncludeObjects { get; init; } = [];

    /// <summary>
    /// 取得要排除的物件名稱模式。
    /// </summary>
    public IReadOnlyList<string> ExcludeObjects { get; init; } = [];

    /// <summary>
    /// 取得或設定是否在資料表之外額外納入檢視表。
    /// </summary>
    public bool IncludeViews { get; set; } = true;
}

