namespace CloudyWing.SchemaExporter;

internal sealed class IndexInfo {
    /// <summary>
    /// 取得或設定結構描述名稱。
    /// </summary>
    public string SchemaName { get; init; } = "";

    /// <summary>
    /// 取得或設定資料表名稱。
    /// </summary>
    public string TableName { get; init; } = "";

    /// <summary>
    /// 取得或設定索引名稱。
    /// </summary>
    public string IndexName { get; init; } = "";

    /// <summary>
    /// 取得或設定指示是否為主鍵的值。
    /// </summary>
    public string IsPrimaryKey { get; init; } = "";

    /// <summary>
    /// 取得或設定指示是否為叢集索引的值。
    /// </summary>
    public string IsClustered { get; init; } = "";

    /// <summary>
    /// 取得或設定指示是否為唯一索引的值。
    /// </summary>
    public string IsUnique { get; init; } = "";

    /// <summary>
    /// 取得或設定指示是否為外鍵的值。
    /// </summary>
    public string IsForeignKey { get; init; } = "";

    /// <summary>
    /// 取得或設定索引包含的資料行清單。
    /// </summary>
    public string Columns { get; init; } = "";

    /// <summary>
    /// 取得或設定索引關聯的其他資料行清單。
    /// </summary>
    public string OtherColumns { get; init; } = "";
}
