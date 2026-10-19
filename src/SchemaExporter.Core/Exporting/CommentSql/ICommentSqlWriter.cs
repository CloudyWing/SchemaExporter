using System.Text;

namespace CloudyWing.SchemaExporter.Core.Exporting.CommentSql;

/// <summary>
/// 表示單一資料庫類型的註解 SQL 輸出實作。
/// </summary>
internal interface ICommentSqlWriter {
    /// <summary>
    /// 取得輸出檔案是否需要 UTF-8 BOM。
    /// </summary>
    bool EmitUtf8Bom { get; }

    /// <summary>
    /// 檢查註解內容是否超出該資料庫的限制。
    /// </summary>
    /// <param name="comment">已通過字元檢查的非空註解。</param>
    /// <returns>超出限制時的說明；可寫入時回傳 <see langword="null" />。</returns>
    string? ValidateComment(string comment);

    /// <summary>
    /// 檢查物件或欄位名稱是否無法寫入該資料庫的腳本。
    /// </summary>
    /// <param name="name">已通過控制字元檢查的名稱。</param>
    /// <returns>無法寫入時的說明；可寫入時回傳 <see langword="null" />。</returns>
    string? ValidateName(string name);

    /// <summary>
    /// 寫入腳本開頭，含檔頭說明與該資料庫工具需要的設定。
    /// </summary>
    /// <param name="builder">輸出目標。</param>
    /// <param name="connectionName">產生 Snapshot 的連線名稱。</param>
    void WriteHeader(StringBuilder builder, string connectionName);

    /// <summary>
    /// 寫入一筆設定註解的語句。
    /// </summary>
    /// <param name="builder">輸出目標。</param>
    /// <param name="target">註解目標。</param>
    /// <param name="comment">要寫入的註解內容。</param>
    void WriteStatement(StringBuilder builder, CommentSqlTarget target, string comment);
}
