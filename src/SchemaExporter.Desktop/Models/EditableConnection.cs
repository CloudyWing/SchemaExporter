using System.Security.Cryptography;
using CloudyWing.SchemaExporter.Core;
using CloudyWing.SchemaExporter.Infrastructure.Security;

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
    /// 取得或設定一個值，用以指出儲存時是否以 DPAPI 加密連線字串。
    /// </summary>
    public bool IsEncrypted { get; set; }

    /// <summary>
    /// 取得無法解密的原始加密值；<see langword="null"/> 表示已正常解密或未加密。
    /// </summary>
    /// <remarks>
    /// 設定檔在其他電腦或使用者帳號加密時無法解開。使用者未重新輸入連線字串時，儲存會保留這個原始值，不會清掉。
    /// </remarks>
    public string? UnreadableProtectedValue { get; private set; }

    /// <summary>
    /// 取得一個值，用以指出連線字串是否為環境變數參照（<c>${env:NAME}</c>）。環境變數參照不加密。
    /// </summary>
    public bool IsEnvironmentReference => ConnectionStringFormat.TryGetEnvironmentVariableName(ConnectionString, out _);

    /// <summary>
    /// 從 <see cref="SchemaConnection"/> 建立對應的 <see cref="EditableConnection"/> 執行個體，加密的連線字串會解密後供編輯。
    /// </summary>
    /// <param name="connection">來源連線設定。</param>
    /// <param name="protector">解密連線字串的加密器。</param>
    /// <returns>對應的可編輯連線設定執行個體。</returns>
    public static EditableConnection FromSchemaConnection(SchemaConnection connection, IConnectionStringProtector protector) {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(protector);

        EditableConnection editable = new() {
            Name = connection.Name,
            DatabaseType = connection.DatabaseType,
            ConnectionString = connection.ConnectionString,
            ExportProfileName = connection.ExportProfileName
        };

        if (!ConnectionStringFormat.IsProtected(connection.ConnectionString)) {
            return editable;
        }

        editable.IsEncrypted = true;
        try {
            editable.ConnectionString = protector.Unprotect(connection.ConnectionString);
        } catch (Exception ex) when (ex is CryptographicException or PlatformNotSupportedException) {
            editable.ConnectionString = "";
            editable.UnreadableProtectedValue = connection.ConnectionString;
        }

        return editable;
    }

    /// <summary>
    /// 將目前的可編輯連線設定轉換為 <see cref="SchemaConnection"/> 執行個體，需要時加密連線字串。
    /// </summary>
    /// <param name="protector">加密連線字串的加密器。</param>
    /// <returns>包含已套用修剪處理的 <see cref="SchemaConnection"/> 執行個體。</returns>
    /// <exception cref="PlatformNotSupportedException">要求加密但目前作業系統不支援時。</exception>
    public SchemaConnection ToSchemaConnection(IConnectionStringProtector protector) {
        ArgumentNullException.ThrowIfNull(protector);

        return new SchemaConnection {
            Name = Name.Trim(),
            DatabaseType = DatabaseType,
            ConnectionString = BuildStoredConnectionString(protector),
            ExportProfileName = string.IsNullOrWhiteSpace(ExportProfileName)
                ? null
                : ExportProfileName.Trim()
        };
    }

    private string BuildStoredConnectionString(IConnectionStringProtector protector) {
        string connectionString = ConnectionString.Trim();
        if (connectionString.Length == 0 && UnreadableProtectedValue is not null) {
            return UnreadableProtectedValue;
        }

        if (!IsEncrypted || connectionString.Length == 0 || IsEnvironmentReference) {
            return connectionString;
        }

        return protector.Protect(connectionString);
    }
}
