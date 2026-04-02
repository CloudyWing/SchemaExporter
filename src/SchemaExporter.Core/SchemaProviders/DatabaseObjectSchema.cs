namespace CloudyWing.SchemaExporter.Core.SchemaProviders;

/// <summary>
/// 表示與提供者無關的資料庫物件。
/// </summary>
public sealed class DatabaseObjectSchema {
    /// <summary>
    /// 取得或設定結構描述名稱。
    /// </summary>
    public required string SchemaName { get; set; }

    /// <summary>
    /// 取得或設定物件名稱。
    /// </summary>
    public required string ObjectName { get; set; }

    /// <summary>
    /// 取得或設定物件類型。
    /// </summary>
    public required string ObjectType { get; set; }

    /// <summary>
    /// 取得或設定物件描述。
    /// </summary>
    public string? ObjectDescription { get; set; }

    /// <summary>
    /// 取得物件索引鍵。
    /// </summary>
    public DatabaseObjectKey ObjectKey => new(SchemaName, ObjectName, ObjectType);
}

