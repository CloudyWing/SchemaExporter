using System.Text;

namespace CloudyWing.SchemaExporter.Core.Exporting.CommentSql;

/// <summary>
/// 以 <c>sp_addextendedproperty</c> 與 <c>sp_updateextendedproperty</c> 輸出 SQL Server 註解（<c>MS_Description</c>）。
/// </summary>
/// <remarks>
/// 註解值一律放進 <c>nvarchar(4000)</c> 變數再傳給程序：換行與 <c>$(</c> 以 <c>NCHAR()</c> 串接，
/// 腳本中不會出現會被 sqlcmd 或 SSMS 當成批次分隔的單獨 <c>GO</c> 行，也不會觸發 sqlcmd 變數替換。
/// 每筆語句先以 <c>fn_listextendedproperty</c> 在執行時檢查屬性是否存在，存在就更新、不存在就新增，
/// 因為 Snapshot 無法區分「沒有屬性」與「屬性值為空字串」，而對後者呼叫新增程序會失敗。
/// </remarks>
internal sealed class SqlServerCommentSqlWriter : ICommentSqlWriter {
    /// <summary>
    /// 擴充屬性值的上限為 7,500 位元組，nvarchar 每個 UTF-16 code unit 佔 2 位元組。
    /// </summary>
    internal const int MaxCommentCodeUnits = 3750;

    private const int LiteralChunkChars = 800;

    private static readonly CommentValueExpressionStyle ValueStyle = new(
        GetCodePointToEncode,
        text => CommentSqlText.SplitByChars(text, LiteralChunkChars),
        Quote,
        "NCHAR",
        " + "
    );

    /// <inheritdoc/>
    public bool EmitUtf8Bom => true;

    /// <inheritdoc/>
    public string? ValidateComment(string comment) {
        if (comment.Length > MaxCommentCodeUnits) {
            return $"註解長度 {comment.Length} 個 UTF-16 字元，超過 SQL Server 擴充屬性值上限 {MaxCommentCodeUnits} 個字元（7,500 位元組）";
        }

        return null;
    }

    /// <inheritdoc/>
    public string? ValidateName(string name) {
        if (name.Contains("$(", StringComparison.Ordinal)) {
            return "名稱含有 \"$(\"，sqlcmd 會將其視為變數替換，不支援";
        }

        return null;
    }

    /// <inheritdoc/>
    public void WriteHeader(StringBuilder builder, string connectionName) {
        builder.Append("-- SchemaExporter comment SQL (SQL Server, MS_Description)").Append(CommentSqlText.NewLine);
        builder.Append("-- Snapshot connection: ").Append(CommentSqlText.EscapeForComment(connectionName)).Append(CommentSqlText.NewLine);
        builder.Append("-- File encoding: UTF-8 with BOM. Run with sqlcmd -i <file> or open in SSMS.").Append(CommentSqlText.NewLine);
        builder.Append("-- Each statement adds the property when it is missing and updates it otherwise.").Append(CommentSqlText.NewLine);
    }

    /// <inheritdoc/>
    public void WriteStatement(StringBuilder builder, CommentSqlTarget target, string comment) {
        WriteValueDeclaration(builder, comment);

        string level1Type = target.IsView ? "VIEW" : "TABLE";
        string levelArguments = $"N'SCHEMA', {Quote(target.SchemaName)}, N'{level1Type}', {Quote(target.ObjectName)}";
        string procedureArguments = $"@level0type = N'SCHEMA', @level0name = {Quote(target.SchemaName)}, "
            + $"@level1type = N'{level1Type}', @level1name = {Quote(target.ObjectName)}";
        string existsArguments = $"{levelArguments}, NULL, NULL";

        if (target.IsColumn) {
            string columnName = Quote(target.ColumnName!);
            procedureArguments += $", @level2type = N'COLUMN', @level2name = {columnName}";
            existsArguments = $"{levelArguments}, N'COLUMN', {columnName}";
        }

        builder.Append("IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', ")
            .Append(existsArguments).Append("))").Append(CommentSqlText.NewLine);
        builder.Append("    EXEC sys.sp_updateextendedproperty @name = N'MS_Description', @value = @value, ")
            .Append(procedureArguments).Append(';').Append(CommentSqlText.NewLine);
        builder.Append("ELSE").Append(CommentSqlText.NewLine);
        builder.Append("    EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = @value, ")
            .Append(procedureArguments).Append(';').Append(CommentSqlText.NewLine);
        builder.Append("GO").Append(CommentSqlText.NewLine);
    }

    private static string Quote(string name) {
        return "N'" + name.Replace("'", "''", StringComparison.Ordinal) + "'";
    }

    // 換行與 "$(" 改以 NCHAR 輸出：前者避免單獨一行 GO 切斷批次，後者避免 sqlcmd 把它當成變數替換。
    private static int? GetCodePointToEncode(string comment, int index) {
        return comment[index] switch {
            '\r' => 13,
            '\n' => 10,
            '$' when index + 1 < comment.Length && comment[index + 1] == '(' => 36,
            _ => null
        };
    }

    private static void WriteValueDeclaration(StringBuilder builder, string comment) {
        List<string> lines = CommentSqlText.BuildValueLines(comment, ValueStyle);

        builder.Append("DECLARE @value nvarchar(4000) = ").Append(lines[0]);
        for (int index = 1; index < lines.Count; index++) {
            builder.Append(CommentSqlText.NewLine).Append("    + ").Append(lines[index]);
        }

        builder.Append(';').Append(CommentSqlText.NewLine);
    }
}
