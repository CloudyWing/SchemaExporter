using System.Security.Cryptography;
using CloudyWing.SchemaExporter.Core;
using CloudyWing.SchemaExporter.Core.Exporting;

namespace CloudyWing.SchemaExporter.Infrastructure.Security;

/// <summary>
/// 依保存格式解析連線字串：明文照原值使用，<c>${env:NAME}</c> 讀取環境變數，<c>dpapi:</c> 以 DPAPI 解密。
/// </summary>
public sealed class ConnectionStringResolver : IConnectionStringResolver {
    private readonly IConnectionStringProtector protector;

    /// <summary>
    /// 初始化 <see cref="ConnectionStringResolver"/> 類別的新執行個體。
    /// </summary>
    /// <param name="protector">解密 DPAPI 值的加密器。</param>
    public ConnectionStringResolver(IConnectionStringProtector protector) {
        ArgumentNullException.ThrowIfNull(protector);
        this.protector = protector;
    }

    /// <inheritdoc/>
    public string Resolve(SchemaConnection connection) {
        ArgumentNullException.ThrowIfNull(connection);

        string value = connection.ConnectionString;
        if (ConnectionStringFormat.TryGetEnvironmentVariableName(value, out string variableName)) {
            string? environmentValue = Environment.GetEnvironmentVariable(variableName);
            return string.IsNullOrWhiteSpace(environmentValue)
                ? throw new ExportValidationException($"連線「{connection.Name}」參照的環境變數 {variableName} 未設定或為空白。")
                : environmentValue;
        }

        if (!ConnectionStringFormat.IsProtected(value)) {
            return value;
        }

        try {
            return protector.Unprotect(value);
        } catch (PlatformNotSupportedException) {
            throw new ExportValidationException($"連線「{connection.Name}」的連線字串已加密，只能在 Windows 上解密。");
        } catch (CryptographicException) {
            // 不附上原始例外訊息與加密內容，避免把設定值帶進錯誤輸出。
            throw new ExportValidationException(
                $"連線「{connection.Name}」的加密連線字串無法解密。加密值只能由加密時的同一台電腦與 Windows 使用者解開，請在設定頁重新輸入連線字串。"
            );
        }
    }
}
