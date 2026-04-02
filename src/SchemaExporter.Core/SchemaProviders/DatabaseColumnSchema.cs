namespace CloudyWing.SchemaExporter.Core.SchemaProviders;

/// <summary>
/// 表示與提供者無關的資料庫資料行。
/// </summary>
public sealed class DatabaseColumnSchema {
    /// <summary>
    /// 取得或設定結構描述名稱。
    /// </summary>
    public required string SchemaName { get; set; }

    /// <summary>
    /// 取得或設定所屬物件名稱。
    /// </summary>
    public required string ObjectName { get; set; }

    /// <summary>
    /// 取得或設定所屬物件類型。
    /// </summary>
    public required string ObjectType { get; set; }

    /// <summary>
    /// 取得或設定資料行名稱。
    /// </summary>
    public required string ColumnName { get; set; }

    /// <summary>
    /// 取得或設定提供者特定的資料行類型。
    /// </summary>
    public required string ColumnType { get; set; }

    /// <summary>
    /// 取得或設定資料行是否可為 null。
    /// </summary>
    public required string IsNullable { get; set; }

    /// <summary>
    /// 取得或設定資料行的預設運算式。
    /// </summary>
    public string? ColumnDefault { get; set; }

    /// <summary>
    /// 取得或設定資料行是否為主鍵的一部分。
    /// </summary>
    public required string IsPrimaryKey { get; set; }

    /// <summary>
    /// 取得或設定資料行是否為識別欄位。
    /// </summary>
    public required string IsIdentity { get; set; }

    /// <summary>
    /// 取得或設定資料行描述。
    /// </summary>
    public string? ColumnDescription { get; set; }

    /// <summary>
    /// 取得或設定資料行的序數位置。
    /// </summary>
    public int ColumnOrder { get; set; }

    /// <summary>
    /// 取得所屬物件索引鍵。
    /// </summary>
    public DatabaseObjectKey ObjectKey => new(SchemaName, ObjectName, ObjectType);
}

