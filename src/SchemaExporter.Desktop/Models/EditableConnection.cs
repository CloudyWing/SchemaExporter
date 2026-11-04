using CloudyWing.SchemaExporter.Core;

namespace CloudyWing.SchemaExporter.Desktop.Models;

/// <summary>
/// 提供可於設定頁編輯的連線設定模型。
/// </summary>
internal sealed class EditableConnection {
    /// <summary>
    /// 取得編輯期間的穩定識別碼，供畫面在名稱變更時仍能辨識同一筆連線。
    /// </summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>
    /// 取得或設定連線名稱。
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// 取得或設定資料庫類型。
    /// </summary>
    public DatabaseType DatabaseType { get; set; } = DatabaseType.SqlServer;

    /// <summary>
    /// 取得或設定連線字串。
    /// </summary>
    public string ConnectionString { get; set; } = "";

    /// <summary>
    /// 取得或設定此連線預設使用的匯出設定檔名稱；<see langword="null"/> 表示使用第一個設定檔。
    /// </summary>
    public string? ExportProfileName { get; set; }

    /// <summary>
    /// 從 <see cref="SchemaConnection"/> 建立對應的 <see cref="EditableConnection"/> 執行個體。
    /// </summary>
    /// <param name="connection">來源連線設定。</param>
    /// <returns>對應的可編輯連線設定執行個體。</returns>
    public static EditableConnection FromSchemaConnection(SchemaConnection connection) {
        ArgumentNullException.ThrowIfNull(connection);

        return new EditableConnection {
            Name = connection.Name,
            DatabaseType = connection.DatabaseType,
            ConnectionString = connection.ConnectionString,
            ExportProfileName = connection.ExportProfileName
        };
    }

    /// <summary>
    /// 將目前的可編輯連線設定轉換為 <see cref="SchemaConnection"/> 執行個體。
    /// </summary>
    /// <returns>包含已套用修剪處理的 <see cref="SchemaConnection"/> 執行個體。</returns>
    public SchemaConnection ToSchemaConnection() {
        return new SchemaConnection {
            Name = Name.Trim(),
            DatabaseType = DatabaseType,
            ConnectionString = ConnectionString.Trim(),
            ExportProfileName = string.IsNullOrWhiteSpace(ExportProfileName)
                ? null
                : ExportProfileName.Trim()
        };
    }
}
