namespace CloudyWing.SchemaExporter.Core.SchemaProviders;

/// <summary>
/// 使用結構描述、名稱和類型識別資料庫物件。
/// </summary>
/// <param name="SchemaName">資料庫結構描述名稱。</param>
/// <param name="ObjectName">資料庫物件名稱。</param>
/// <param name="ObjectType">提供者特定的物件類型。</param>
public readonly record struct DatabaseObjectKey(string SchemaName, string ObjectName, string ObjectType);

