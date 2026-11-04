namespace CloudyWing.SchemaExporter.Infrastructure.Security;

/// <summary>
/// 定義連線字串的加密與解密。
/// </summary>
public interface IConnectionStringProtector {
    /// <summary>
    /// 加密連線字串，回傳含 <see cref="ConnectionStringFormat.ProtectedPrefix"/> 前綴的保存值。
    /// </summary>
    /// <param name="connectionString">明文連線字串。</param>
    /// <returns>可寫入設定檔的加密值。</returns>
    /// <exception cref="PlatformNotSupportedException">目前作業系統不支援加密時。</exception>
    string Protect(string connectionString);

    /// <summary>
    /// 解密含 <see cref="ConnectionStringFormat.ProtectedPrefix"/> 前綴的保存值。
    /// </summary>
    /// <param name="protectedValue">設定檔中的加密值。</param>
    /// <returns>明文連線字串。</returns>
    /// <exception cref="PlatformNotSupportedException">目前作業系統不支援解密時。</exception>
    /// <exception cref="System.Security.Cryptography.CryptographicException">加密值損毀，或不是由目前的 Windows 使用者加密時。</exception>
    string Unprotect(string protectedValue);
}
