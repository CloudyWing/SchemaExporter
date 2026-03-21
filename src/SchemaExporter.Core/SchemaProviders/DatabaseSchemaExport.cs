namespace CloudyWing.SchemaExporter.Core.SchemaProviders;

/// <summary>
/// 表示匯出層所使用的與提供者無關的結構描述資料。
/// </summary>
public sealed class DatabaseSchemaExport {
    /// <summary>
    /// 取得已匯出的資料庫物件。
    /// </summary>
    public IReadOnlyList<DatabaseObjectSchema> Objects { get; init; } = [];

    /// <summary>
    /// 取得已匯出的資料行。
    /// </summary>
    public IReadOnlyList<DatabaseColumnSchema> Columns { get; init; } = [];

    /// <summary>
    /// 取得已匯出的索引。
    /// </summary>
    public IReadOnlyList<DatabaseIndexSchema> Indexes { get; init; } = [];

    /// <summary>
    /// 取得已匯出的程序。
    /// </summary>
    public IReadOnlyList<DatabaseRoutineSchema> Routines { get; init; } = [];
}

