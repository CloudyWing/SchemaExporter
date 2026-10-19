using System.Text.Json.Serialization;

namespace CloudyWing.SchemaExporter.Core.Exporting.CommentSql;

/// <summary>
/// 表示註解清單中的一筆註解，對應資料表、檢視表或欄位。
/// </summary>
public sealed class CommentSqlEntry {
    /// <summary>
    /// 取得或設定結構描述名稱（SQL Server 的 schema，Oracle 的 owner）。
    /// </summary>
    [JsonPropertyName("schema")]
    public string? Schema { get; init; }

    /// <summary>
    /// 取得或設定資料表或檢視表名稱。
    /// </summary>
    [JsonPropertyName("object")]
    public string? ObjectName { get; init; }

    /// <summary>
    /// 取得或設定欄位名稱；為 <see langword="null" /> 或空字串時，代表資料表或檢視表層級的註解。
    /// </summary>
    [JsonPropertyName("column")]
    public string? Column { get; init; }

    /// <summary>
    /// 取得或設定註解內容。
    /// </summary>
    [JsonPropertyName("comment")]
    public string? Comment { get; init; }
}
