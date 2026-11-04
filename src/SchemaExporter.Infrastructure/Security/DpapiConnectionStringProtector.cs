using System.Security.Cryptography;
using System.Text;

namespace CloudyWing.SchemaExporter.Infrastructure.Security;

/// <summary>
/// 以 Windows DPAPI（目前使用者範圍）實作 <see cref="IConnectionStringProtector"/>。
/// </summary>
/// <remarks>
/// 加密值只能由同一台電腦、同一個 Windows 使用者解開；複製設定檔到其他電腦或使用者後必須重新輸入連線字串。
/// </remarks>
public sealed class DpapiConnectionStringProtector : IConnectionStringProtector {
    private const string NotSupportedMessage = "加密連線字串只支援 Windows。";
    // 額外 entropy 讓其他程式即使以相同使用者呼叫 DPAPI，也無法直接解開本工具的值。
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("CloudyWing.SchemaExporter.ConnectionString.v1");

    /// <inheritdoc/>
    public string Protect(string connectionString) {
        ArgumentNullException.ThrowIfNull(connectionString);
        if (!OperatingSystem.IsWindows()) {
            throw new PlatformNotSupportedException(NotSupportedMessage);
        }

        byte[] protectedBytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(connectionString), Entropy, DataProtectionScope.CurrentUser);
        return ConnectionStringFormat.ProtectedPrefix + Convert.ToBase64String(protectedBytes);
    }

    /// <inheritdoc/>
    public string Unprotect(string protectedValue) {
        ArgumentNullException.ThrowIfNull(protectedValue);
        if (!OperatingSystem.IsWindows()) {
            throw new PlatformNotSupportedException(NotSupportedMessage);
        }

        string payload = protectedValue.Trim();
        if (!payload.StartsWith(ConnectionStringFormat.ProtectedPrefix, StringComparison.Ordinal)) {
            throw new CryptographicException("連線字串不是加密格式。");
        }

        byte[] protectedBytes;
        try {
            protectedBytes = Convert.FromBase64String(payload[ConnectionStringFormat.ProtectedPrefix.Length..]);
        } catch (FormatException ex) {
            throw new CryptographicException("加密連線字串的內容已損毀。", ex);
        }

        return Encoding.UTF8.GetString(ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser));
    }
}
