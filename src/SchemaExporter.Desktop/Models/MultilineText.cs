namespace CloudyWing.SchemaExporter.Desktop.Models;

/// <summary>
/// 提供「每行一筆」文字欄位與字串清單之間的轉換。
/// </summary>
internal static class MultilineText {
    /// <summary>
    /// 將清單合併為每行一筆的文字。
    /// </summary>
    /// <param name="values">要合併的值。</param>
    /// <returns>以換行分隔的文字。</returns>
    public static string Join(IEnumerable<string> values) {
        return string.Join(Environment.NewLine, values);
    }

    /// <summary>
    /// 將多行文字切分並修剪後取代目標清單內容，忽略空白行與不分大小寫的重複項目。
    /// </summary>
    /// <param name="target">要取代內容的清單。</param>
    /// <param name="text">每行一筆的文字。</param>
    public static void ReplaceWith(List<string> target, string? text) {
        ReplaceWith(target, Split(text));
    }

    /// <summary>
    /// 以修剪後的值取代目標清單內容，忽略空白項目與不分大小寫的重複項目。
    /// </summary>
    /// <param name="target">要取代內容的清單。</param>
    /// <param name="values">來源值。</param>
    public static void ReplaceWith(List<string> target, IEnumerable<string> values) {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(values);

        List<string> normalized = values
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        target.Clear();
        target.AddRange(normalized);
    }

    /// <summary>
    /// 將多行文字切分為字串清單。
    /// </summary>
    /// <param name="text">每行一筆的文字。</param>
    /// <returns>切分後的值，尚未修剪與去重。</returns>
    public static IEnumerable<string> Split(string? text) {
        return text?.Split(["\r\n", "\n"], StringSplitOptions.None) ?? [];
    }
}
