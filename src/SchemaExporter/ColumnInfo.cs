namespace CloudyWing.SchemaExporter;

/// <summary>
/// 表示舊版資料表欄位清單畫面使用的欄位資訊。
/// </summary>
internal sealed class ColumnInfo {
    /// <summary>
    /// 取得或設定欄位所屬的結構描述名稱。
    /// </summary>
    public string SchemaName { get; init; } = "";

    /// <summary>
    /// 取得或設定欄位所屬的資料表名稱。
    /// </summary>
    public string TableName { get; init; } = "";

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
