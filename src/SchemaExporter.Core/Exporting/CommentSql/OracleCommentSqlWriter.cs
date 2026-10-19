using System.Text;

namespace CloudyWing.SchemaExporter.Core.Exporting.CommentSql;

/// <summary>
/// 以 <c>COMMENT ON</c> 輸出 Oracle 表、檢視表與欄位註解。
/// </summary>
/// <remarks>
/// 識別字一律加雙引號以保留大小寫。註解不含換行且夠短時，輸出單行 <c>COMMENT ON</c>；
/// 含換行或過長時，改用 PL/SQL 區塊，以 <c>CHR()</c> 串接換行並以 <c>EXECUTE IMMEDIATE</c> 執行同樣的 <c>COMMENT ON</c>。
/// 原因是 SQL*Plus 會把字串內以 <c>/</c>、<c>.</c>、<c>#</c>、<c>@</c> 開頭的行當成指令，
/// 且單行超過約 3,000 字元會失敗，兩者都無法靠 <c>COMMENT ON</c> 字面值內的換行迴避。
/// </remarks>
internal sealed class OracleCommentSqlWriter : ICommentSqlWriter {
    /// <summary>
    /// Oracle 註解的上限為 4,000 位元組，位元組數以 UTF-8（AL32UTF8）計算。
    /// </summary>
    internal const int MaxCommentBytes = 4000;

    private const int PlainStatementMaxBytes = 2000;
    private const int ChunkMaxBytes = 600;

    private static readonly CommentValueExpressionStyle ValueStyle = new(
        GetCodePointToEncode,
        text => CommentSqlText.SplitByUtf8Bytes(text, ChunkMaxBytes),
        QuoteLiteral,
        "CHR",
        " || "
    );

    /// <inheritdoc/>
    public bool EmitUtf8Bom => false;

    /// <inheritdoc/>
    public string? ValidateComment(string comment) {
        int byteCount = CommentSqlText.GetUtf8ByteCount(comment);
        if (byteCount > MaxCommentBytes) {
            return $"註解長度 {byteCount} 位元組（UTF-8），超過 Oracle 註解上限 {MaxCommentBytes} 位元組";
        }

        return null;
    }

    /// <inheritdoc/>
    public string? ValidateName(string name) {
        if (name.Contains('"', StringComparison.Ordinal)) {
            return "Oracle 識別字不可包含雙引號";
        }

        return null;
    }

    /// <inheritdoc/>
    public void WriteHeader(StringBuilder builder, string connectionName) {
        builder.Append("SET DEFINE OFF").Append(CommentSqlText.NewLine);
        builder.Append("SET SQLBLANKLINES ON").Append(CommentSqlText.NewLine);
        builder.Append("-- SchemaExporter comment SQL (Oracle, COMMENT ON)").Append(CommentSqlText.NewLine);
        builder.Append("-- Snapshot connection: ").Append(CommentSqlText.EscapeForComment(connectionName)).Append(CommentSqlText.NewLine);
        builder.Append("-- File encoding: UTF-8 without BOM. Run with SQL*Plus using NLS_LANG=AMERICAN_AMERICA.AL32UTF8.").Append(CommentSqlText.NewLine);
        builder.Append("-- COMMENT ON replaces the existing comment, so every statement is add-or-update.").Append(CommentSqlText.NewLine);
    }

    /// <inheritdoc/>
    public void WriteStatement(StringBuilder builder, CommentSqlTarget target, string comment) {
        string prefix = target.IsColumn
            ? $"COMMENT ON COLUMN {QuoteIdentifier(target.SchemaName)}.{QuoteIdentifier(target.ObjectName)}.{QuoteIdentifier(target.ColumnName!)} IS "
            : $"COMMENT ON TABLE {QuoteIdentifier(target.SchemaName)}.{QuoteIdentifier(target.ObjectName)} IS ";

        bool hasLineBreak = comment.Contains('\r', StringComparison.Ordinal) || comment.Contains('\n', StringComparison.Ordinal);
        string plainStatement = prefix + "'" + comment.Replace("'", "''", StringComparison.Ordinal) + "';";
        if (!hasLineBreak && CommentSqlText.GetUtf8ByteCount(plainStatement) <= PlainStatementMaxBytes) {
            builder.Append(plainStatement).Append(CommentSqlText.NewLine);
            return;
        }

        WriteBlockStatement(builder, prefix, comment);
    }

    private static string QuoteIdentifier(string name) {
        return "\"" + name + "\"";
    }

    private static string QuoteLiteral(string text) {
        return "'" + text.Replace("'", "''", StringComparison.Ordinal) + "'";
    }

    // 換行以 CHR 輸出，字面值內不放實際換行，避免 SQL*Plus 把字串內的行當成指令。
    private static int? GetCodePointToEncode(string comment, int index) {
        return comment[index] switch {
            '\r' => 13,
            '\n' => 10,
            _ => null
        };
    }

    private static void WriteBlockStatement(StringBuilder builder, string prefix, string comment) {
        List<string> lines = CommentSqlText.BuildValueLines(comment, ValueStyle);

        builder.Append("DECLARE").Append(CommentSqlText.NewLine);
        builder.Append("    v_comment VARCHAR2(4000);").Append(CommentSqlText.NewLine);
        builder.Append("BEGIN").Append(CommentSqlText.NewLine);
        builder.Append("    v_comment := ").Append(lines[0]);
        for (int index = 1; index < lines.Count; index++) {
            builder.Append(CommentSqlText.NewLine).Append("        || ").Append(lines[index]);
        }

        builder.Append(';').Append(CommentSqlText.NewLine);
        builder.Append("    EXECUTE IMMEDIATE ").Append(QuoteLiteral(prefix + "'"))
            .Append(" || REPLACE(v_comment, '''', '''''') || '''';").Append(CommentSqlText.NewLine);
        builder.Append("END;").Append(CommentSqlText.NewLine);
        builder.Append('/').Append(CommentSqlText.NewLine);
    }
}
