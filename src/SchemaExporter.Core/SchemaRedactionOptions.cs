namespace CloudyWing.SchemaExporter.Core;

/// <summary>
/// 設定 schema 匯出時的敏感 metadata 遮罩規則。
/// </summary>
public sealed class SchemaRedactionOptions {
    /// <summary>
    /// 取得或設定是否啟用 redaction。
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// 取得或設定遮罩後使用的替代文字。
    /// </summary>
    public string ReplacementText { get; set; } = "[REDACTED]";

    /// <summary>
    /// 取得用來判斷敏感物件、欄位或 routine 名稱的規則運算式集合。
    /// </summary>
    public IReadOnlyList<string> SensitiveNamePatterns { get; init; } = [
        "password",
        "passwd",
        "pwd",
        "secret",
        "token",
        "api[_-]?key",
        "credential",
        "private[_-]?key",
        "connection[_-]?string",
        "salt"
    ];

    /// <summary>
    /// 取得用來遮罩 metadata 文字內容的規則運算式集合。
    /// </summary>
    public IReadOnlyList<string> SensitiveTextPatterns { get; init; } = [];
}
