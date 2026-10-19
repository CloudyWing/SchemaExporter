using System.Globalization;
using System.Text;

namespace CloudyWing.SchemaExporter.Core.Exporting.CommentSql;

/// <summary>
/// 提供註解 SQL 產生共用的字元檢查與顯示轉義工具。
/// </summary>
internal static class CommentSqlText {
    /// <summary>
    /// 產生的腳本固定使用的換行字元。
    /// </summary>
    public const string NewLine = "\n";

    /// <summary>
    /// 檢查註解內容是否含有無法安全寫入腳本的字元。
    /// 允許 Tab、CR、LF；其餘控制字元、行分隔與段落分隔字元、未成對的 surrogate 一律不支援。
    /// </summary>
    /// <param name="text">註解內容。</param>
    /// <returns>第一個不支援字元的說明；全部可用時回傳 <see langword="null" />。</returns>
    public static string? FindUnsupportedCommentCharacter(string text) {
        return FindUnsupportedCharacter(text, allowTabAndLineBreaks: true);
    }

    /// <summary>
    /// 檢查物件或欄位名稱是否含有無法安全寫入腳本的字元，不允許任何控制字元。
    /// </summary>
    /// <param name="text">名稱。</param>
    /// <returns>第一個不支援字元的說明；全部可用時回傳 <see langword="null" />。</returns>
    public static string? FindUnsupportedNameCharacter(string text) {
        return FindUnsupportedCharacter(text, allowTabAndLineBreaks: false);
    }

    /// <summary>
    /// 計算文字轉為 UTF-8 後的位元組數。
    /// </summary>
    /// <param name="text">已通過字元檢查的文字。</param>
    /// <returns>UTF-8 位元組數。</returns>
    public static int GetUtf8ByteCount(string text) {
        return Encoding.UTF8.GetByteCount(text);
    }

    /// <summary>
    /// 把文字拆成不超過指定 UTF-8 位元組數的片段，不會在 surrogate pair 中間切開。
    /// </summary>
    /// <param name="text">已通過字元檢查的文字。</param>
    /// <param name="maxBytes">每個片段的 UTF-8 位元組上限。</param>
    /// <returns>依序排列的片段。</returns>
    public static IEnumerable<string> SplitByUtf8Bytes(string text, int maxBytes) {
        StringBuilder chunk = new();
        int chunkBytes = 0;

        foreach (Rune rune in text.EnumerateRunes()) {
            int runeBytes = rune.Utf8SequenceLength;
            if (chunkBytes + runeBytes > maxBytes && chunk.Length > 0) {
                yield return chunk.ToString();
                chunk.Clear();
                chunkBytes = 0;
            }

            chunk.Append(rune.ToString());
            chunkBytes += runeBytes;
        }

        if (chunk.Length > 0) {
            yield return chunk.ToString();
        }
    }

    /// <summary>
    /// 把文字拆成不超過指定 UTF-16 字元數的片段，不會在 surrogate pair 中間切開。
    /// </summary>
    /// <param name="text">已通過字元檢查的文字。</param>
    /// <param name="maxChars">每個片段的字元數上限。</param>
    /// <returns>依序排列的片段。</returns>
    public static IEnumerable<string> SplitByChars(string text, int maxChars) {
        int start = 0;
        while (start < text.Length) {
            int length = Math.Min(maxChars, text.Length - start);
            if (length < text.Length - start && char.IsHighSurrogate(text[start + length - 1])) {
                length--;
            }

            yield return text.Substring(start, length);
            start += length;
        }
    }

    /// <summary>
    /// 把註解內容轉成一組串接運算式的行：一般文字成為字面值片段，需要編碼的字元成為字元函式呼叫，
    /// 連續的字元函式呼叫放在同一行。
    /// </summary>
    /// <param name="comment">已通過檢查的非空註解。</param>
    /// <param name="style">目標資料庫的運算式寫法。</param>
    /// <returns>依序排列的運算式行，至少一行。</returns>
    public static List<string> BuildValueLines(string comment, CommentValueExpressionStyle style) {
        List<string> lines = [];
        StringBuilder literal = new();
        List<string> codeParts = [];

        void FlushLiteral() {
            if (literal.Length == 0) {
                return;
            }

            foreach (string chunk in style.SplitLiteral(literal.ToString())) {
                lines.Add(style.QuoteLiteral(chunk));
            }

            literal.Clear();
        }

        void FlushCodeParts() {
            if (codeParts.Count == 0) {
                return;
            }

            lines.Add(string.Join(style.Joiner, codeParts));
            codeParts.Clear();
        }

        for (int index = 0; index < comment.Length; index++) {
            int? codePoint = style.GetCodePointToEncode(comment, index);
            if (codePoint is null) {
                FlushCodeParts();
                literal.Append(comment[index]);
                continue;
            }

            FlushLiteral();
            codeParts.Add(string.Create(CultureInfo.InvariantCulture, $"{style.CodeFunctionName}({codePoint})"));
        }

        FlushLiteral();
        FlushCodeParts();
        return lines;
    }

    /// <summary>
    /// 產生可放進單行 SQL 註解的值描述：加雙引號，並以 JSON 風格轉義反斜線、雙引號與控制字元，
    /// 讓空白、換行與 Tab 都能被辨識。<c>$(</c> 也會轉義，避免 sqlcmd 把它當成變數替換。
    /// </summary>
    /// <param name="value">要描述的值。</param>
    /// <returns>依 <paramref name="maxBytesPerLine" /> 拆成多段的描述；第一段含起始引號，最後一段含結束引號。</returns>
    public static IReadOnlyList<string> DescribeValue(string value, int maxBytesPerLine = 400) {
        List<string> lines = [];
        StringBuilder line = new("\"");
        int lineBytes = 0;

        foreach (string token in EscapeToTokens(value)) {
            int tokenBytes = Encoding.UTF8.GetByteCount(token);
            if (lineBytes + tokenBytes > maxBytesPerLine && lineBytes > 0) {
                lines.Add(line.ToString());
                line.Clear();
                lineBytes = 0;
            }

            line.Append(token);
            lineBytes += tokenBytes;
        }

        line.Append('"');
        lines.Add(line.ToString());
        return lines;
    }

    /// <summary>
    /// 把名稱轉成可放進單行 SQL 註解的文字，控制字元以 JSON 風格轉義。
    /// </summary>
    /// <param name="text">名稱。</param>
    /// <returns>可安全放進註解的文字。</returns>
    public static string EscapeForComment(string text) {
        return string.Concat(EscapeToTokens(text));
    }

    private static string? FindUnsupportedCharacter(string text, bool allowTabAndLineBreaks) {
        int index = 0;
        while (index < text.Length) {
            if (Rune.DecodeFromUtf16(text.AsSpan(index), out Rune rune, out int consumed) != System.Buffers.OperationStatus.Done) {
                return $"位置 {index} 有未成對的 surrogate 字元";
            }

            UnicodeCategory category = Rune.GetUnicodeCategory(rune);
            bool isLineBreakOrTab = rune.Value is '\t' or '\r' or '\n';
            bool isUnsupported = category is UnicodeCategory.Control
                or UnicodeCategory.LineSeparator
                or UnicodeCategory.ParagraphSeparator;
            if (isUnsupported && !(allowTabAndLineBreaks && isLineBreakOrTab)) {
                return $"位置 {index} 有不支援的控制字元 U+{rune.Value:X4}";
            }

            index += consumed;
        }

        return null;
    }

    private static IEnumerable<string> EscapeToTokens(string value) {
        for (int index = 0; index < value.Length; index++) {
            char current = value[index];
            switch (current) {
                case '\\':
                    yield return "\\\\";
                    break;
                case '"':
                    yield return "\\\"";
                    break;
                case '\r':
                    yield return "\\r";
                    break;
                case '\n':
                    yield return "\\n";
                    break;
                case '\t':
                    yield return "\\t";
                    break;
                case '$' when index + 1 < value.Length && value[index + 1] == '(':
                    yield return "\\u0024";
                    break;
                default:
                    if (char.IsControl(current) || current is (char)0x2028 or (char)0x2029) {
                        yield return $"\\u{(int)current:X4}";
                    } else if (char.IsHighSurrogate(current) && index + 1 < value.Length && char.IsLowSurrogate(value[index + 1])) {
                        yield return value.Substring(index, 2);
                        index++;
                    } else {
                        yield return current.ToString();
                    }

                    break;
            }
        }
    }
}
