namespace CloudyWing.SchemaExporter.Core.Exporting.CommentSql;

/// <summary>
/// 表示已對應到 Snapshot 的註解目標，名稱使用 Snapshot 內的實際寫法。
/// </summary>
/// <param name="SchemaName">結構描述名稱。</param>
/// <param name="ObjectName">資料表或檢視表名稱。</param>
/// <param name="IsView">目標物件是否為檢視表。</param>
/// <param name="ColumnName">欄位名稱；資料表或檢視表層級的註解為 <see langword="null" />。</param>
internal sealed record CommentSqlTarget(string SchemaName, string ObjectName, bool IsView, string? ColumnName) {
    /// <summary>
    /// 取得是否為欄位層級的註解目標。
    /// </summary>
    public bool IsColumn => ColumnName is not null;

    /// <summary>
    /// 取得可放進單行 SQL 註解的目標描述。
    /// </summary>
    public string Describe() {
        string text = $"{SchemaName}.{ObjectName}";
        if (ColumnName is not null) {
            text += $".{ColumnName}";
        }

        return CommentSqlText.EscapeForComment(text);
    }
}
