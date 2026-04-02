namespace CloudyWing.SchemaExporter.Core.SchemaProviders;

/// <summary>
/// 表示針對已篩選物件載入的欄位、索引與程序明細資料。
/// </summary>
public sealed class DatabaseSchemaDetails {
    /// <summary>
    /// 取得不含任何明細資料的空白結果。
    /// </summary>
    public static DatabaseSchemaDetails Empty { get; } = new() {
        Columns = Array.Empty<DatabaseColumnSchema>(),
        Indexes = Array.Empty<DatabaseIndexSchema>(),
        Routines = Array.Empty<DatabaseRoutineSchema>()
    };

    /// <summary>
    /// 取得已載入的欄位明細。
    /// </summary>
    public required IReadOnlyList<DatabaseColumnSchema> Columns { get; init; }

    /// <summary>
    /// 取得已載入的索引明細。
    /// </summary>
    public required IReadOnlyList<DatabaseIndexSchema> Indexes { get; init; }

    /// <summary>
    /// 取得已載入的程序明細。
    /// </summary>
    public required IReadOnlyList<DatabaseRoutineSchema> Routines { get; init; }
}
