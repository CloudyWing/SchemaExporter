namespace CloudyWing.SchemaExporter.Core.Exporting.CommentSql;

/// <summary>
/// 描述如何把註解內容寫成「字面值與字元函式串接」的運算式，用來避免在字面值內放實際換行。
/// </summary>
/// <param name="GetCodePointToEncode">判斷指定位置的字元是否要改以字元函式輸出，要的話回傳碼位，否則回傳 <see langword="null" />。</param>
/// <param name="SplitLiteral">把一段連續的字面值拆成長度適中的片段。</param>
/// <param name="QuoteLiteral">把字面值片段加上引號並跳脫單引號。</param>
/// <param name="CodeFunctionName">字元函式名稱，例如 <c>NCHAR</c> 或 <c>CHR</c>。</param>
/// <param name="Joiner">串接運算子，含前後空白。</param>
internal sealed record CommentValueExpressionStyle(
    Func<string, int, int?> GetCodePointToEncode,
    Func<string, IEnumerable<string>> SplitLiteral,
    Func<string, string> QuoteLiteral,
    string CodeFunctionName,
    string Joiner
);
