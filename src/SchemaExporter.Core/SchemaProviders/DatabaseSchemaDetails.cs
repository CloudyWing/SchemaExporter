namespace CloudyWing.SchemaExporter.Core.SchemaProviders;

/// <summary>
/// 表示針對已篩選物件載入的欄位、索引與程序明細資料。
/// </summary>
public sealed class DatabaseSchemaDetails {
    /// <summary>
    /// 取得已載入的欄位明細。
    /// </summary>
    public IReadOnlyList<DatabaseColumnSchema> Columns { get; init; } = [];

    /// <summary>
    /// 取得已載入的索引明細。
    /// </summary>
    public IReadOnlyList<DatabaseIndexSchema> Indexes { get; init; } = [];

    /// <summary>
    /// 取得已載入的程序明細。
    /// </summary>
    public IReadOnlyList<DatabaseRoutineSchema> Routines { get; init; } = [];
}
