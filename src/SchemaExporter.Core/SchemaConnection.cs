namespace CloudyWing.SchemaExporter.Core;

/// <summary>
/// 表示用於結構描述匯出的具名資料庫連線。
/// </summary>
public class SchemaConnection {
    /// <summary>
    /// 取得或設定在 UI 中顯示的名稱。
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// 取得或設定資料庫提供者類型。省略時預設為 SQL Server。
    /// </summary>
    public DatabaseType DatabaseType { get; set; } = DatabaseType.SqlServer;

    /// <summary>
    /// 取得或設定資料庫連接字串。
    /// </summary>
    public required string ConnectionString { get; set; }

    /// <summary>
    /// 取得或設定此連線使用的匯出設定檔名稱。
    /// 若為 null 或空白，則使用預設設定檔。
    /// </summary>
    public string? ExportProfileName { get; set; }
}

