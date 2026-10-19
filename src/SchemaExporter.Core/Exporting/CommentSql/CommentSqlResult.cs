namespace CloudyWing.SchemaExporter.Core.Exporting.CommentSql;

/// <summary>
/// 表示註解 SQL 產生的結果。
/// </summary>
public sealed class CommentSqlResult {
    /// <summary>
    /// 取得或設定目標資料庫類型。
    /// </summary>
    public required DatabaseType DatabaseType { get; init; }

    /// <summary>
    /// 取得或設定完整的 SQL 腳本文字，換行固定為 LF。
    /// </summary>
    public required string SqlText { get; init; }

    /// <summary>
    /// 取得或設定寫入檔案時是否需要 UTF-8 BOM。
    /// </summary>
    public required bool EmitUtf8Bom { get; init; }

    /// <summary>
    /// 取得或設定新增段的語句數量（資料庫目前沒有註解者）。
    /// </summary>
    public int AddCount { get; init; }

    /// <summary>
    /// 取得或設定差異段的語句數量（資料庫已有註解且與清單不同者）。
    /// </summary>
    public int UpdateCount { get; init; }

    /// <summary>
    /// 取得或設定因資料庫現有註解與清單相同而略過的筆數。
    /// </summary>
    public int UnchangedCount { get; init; }

    /// <summary>
    /// 取得或設定因清單註解為空字串而略過的筆數。
    /// </summary>
    public int SkippedEmptyCount { get; init; }

    /// <summary>
    /// 取得或設定警告訊息集合。
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>
    /// 取得 UTF-8 編碼的檔案內容，依 <see cref="EmitUtf8Bom" /> 決定是否附加 BOM。
    /// </summary>
    /// <returns>可直接寫入檔案的位元組。</returns>
    public byte[] GetFileBytes() {
        System.Text.UTF8Encoding encoding = new(EmitUtf8Bom);
        byte[] preamble = encoding.GetPreamble();
        byte[] body = encoding.GetBytes(SqlText);

        byte[] fileBytes = new byte[preamble.Length + body.Length];
        preamble.CopyTo(fileBytes, 0);
        body.CopyTo(fileBytes, preamble.Length);
        return fileBytes;
    }
}
