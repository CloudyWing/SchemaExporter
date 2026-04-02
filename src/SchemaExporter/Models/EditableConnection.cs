using CloudyWing.SchemaExporter.Core;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CloudyWing.SchemaExporter.Models;

/// <summary>
/// 提供可於設定 UI 中編輯的連線設定模型，支援屬性變更通知。
/// </summary>
internal sealed class EditableConnection : ObservableObject {
    /// <summary>
    /// 取得或設定連線名稱。
    /// </summary>
    public string Name {
        get;
        set => SetProperty(ref field, value);
    } = "";

    /// <summary>
    /// 取得或設定資料庫類型。
    /// </summary>
    public DatabaseType DatabaseType {
        get;
        set => SetProperty(ref field, value);
    } = DatabaseType.SqlServer;

    /// <summary>
    /// 取得或設定連線字串。
    /// </summary>
    public string ConnectionString {
        get;
        set => SetProperty(ref field, value);
    } = "";

    /// <summary>
    /// 取得或設定此連線預設使用的匯出設定檔名稱；<see langword="null"/> 表示使用第一個設定檔。
    /// </summary>
    public string? ExportProfileName {
        get;
        set => SetProperty(ref field, value);
    }

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
