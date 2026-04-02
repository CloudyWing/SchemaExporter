using CloudyWing.SchemaExporter.Core.SchemaProviders;

namespace CloudyWing.SchemaExporter.Core.Exporting;

/// <summary>
/// 表示套用篩選條件後的資料庫結構描述匯出資料。
/// </summary>
internal sealed class FilteredSchemaExport {
    /// <summary>
    /// 取得篩選後的資料庫物件清單。
    /// </summary>
    public required IReadOnlyList<DatabaseObjectSchema> Objects { get; init; }

    /// <summary>
    /// 取得篩選後的資料庫欄位清單。
    /// </summary>
    public required IReadOnlyList<DatabaseColumnSchema> Columns { get; init; }

    /// <summary>
    /// 取得篩選後的資料庫索引清單。
    /// </summary>
    public required IReadOnlyList<DatabaseIndexSchema> Indexes { get; init; }

    /// <summary>
    /// 取得篩選後的資料庫程序與函數清單。
    /// </summary>
    public required IReadOnlyList<DatabaseRoutineSchema> Routines { get; init; }
}

