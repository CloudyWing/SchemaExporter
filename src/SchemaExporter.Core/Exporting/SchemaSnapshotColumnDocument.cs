namespace CloudyWing.SchemaExporter.Core.Exporting;

/// <summary>
/// 表示 schema snapshot 中的一個欄位文件。
/// </summary>
public sealed class SchemaSnapshotColumnDocument {
    /// <summary>
    /// 取得或設定欄位名稱。
    /// </summary>
    public string ColumnName { get; init; } = "";

    /// <summary>
    /// 取得或設定欄位型別。
    /// </summary>
    public string ColumnType { get; init; } = "";

    /// <summary>
    /// 取得或設定欄位是否允許 <see langword="null" /> 的描述。
    /// </summary>
    public string IsNullable { get; init; } = "";

    /// <summary>
    /// 取得或設定欄位預設值描述。
    /// </summary>
    public string ColumnDefault { get; init; } = "";

    /// <summary>
    /// 取得或設定欄位是否為主鍵的描述。
    /// </summary>
    public string IsPrimaryKey { get; init; } = "";

    /// <summary>
    /// 取得或設定欄位是否為識別欄位的描述。
    /// </summary>
    public string IsIdentity { get; init; } = "";

    /// <summary>
    /// 取得或設定欄位描述。
    /// </summary>
    public string ColumnDescription { get; init; } = "";

    /// <summary>
    /// 取得或設定欄位排序。
    /// </summary>
    public int ColumnOrder { get; init; }
}

