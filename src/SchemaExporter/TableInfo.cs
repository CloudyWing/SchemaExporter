namespace CloudyWing.SchemaExporter;

internal sealed class TableInfo {
    /// <summary>
    /// 取得或設定結構描述名稱。
    /// </summary>
    public string SchemaName { get; init; } = "";

    /// <summary>
    /// 取得或設定資料表名稱。
    /// </summary>
    public string TableName { get; init; } = "";

    /// <summary>
    /// 取得工作表名稱，為資料表名稱的前 31 個字元。
    /// </summary>
    public string SheeterName => TableName.Length > 31
            ? TableName[..31]
            : TableName;

    /// <summary>
    /// 取得或設定資料表類型。
    /// </summary>
    public string TableType { get; init; } = "";

    /// <summary>
    /// 取得或設定資料表描述。
    /// </summary>
    public string TableDescription { get; init; } = "";
}
