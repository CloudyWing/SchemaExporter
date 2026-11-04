using System.Text.RegularExpressions;

namespace CloudyWing.SchemaExporter.Infrastructure.Security;

/// <summary>
/// 判斷設定檔中連線字串值的保存格式：明文、環境變數參照（<c>${env:NAME}</c>）或 DPAPI 加密（<c>dpapi:</c> 前綴）。
/// </summary>
public static partial class ConnectionStringFormat {
    /// <summary>
    /// DPAPI 加密值的前綴。
    /// </summary>
    public const string ProtectedPrefix = "dpapi:";

    /// <summary>
    /// 判斷值是否為 DPAPI 加密格式。
    /// </summary>
    /// <param name="value">設定檔中保存的值。</param>
    /// <returns>為加密格式時回傳 <see langword="true"/>；否則回傳 <see langword="false"/>。</returns>
    public static bool IsProtected(string? value) {
        return value is not null && value.Trim().StartsWith(ProtectedPrefix, StringComparison.Ordinal);
    }

    /// <summary>
    /// 判斷值是否為環境變數參照，整個值必須是 <c>${env:NAME}</c>。
    /// </summary>
    /// <param name="value">設定檔中保存的值。</param>
    /// <param name="variableName">當此方法回傳 <see langword="true"/> 時，包含環境變數名稱；否則為空字串。</param>
    /// <returns>為環境變數參照時回傳 <see langword="true"/>；否則回傳 <see langword="false"/>。</returns>
    public static bool TryGetEnvironmentVariableName(string? value, out string variableName) {
        Match match = value is null ? Match.Empty : EnvironmentReferencePattern().Match(value.Trim());
        variableName = match.Success ? match.Groups["name"].Value : "";
        return match.Success;
    }

    // 只接受整個值為單一參照，不做字串內插，避免連線字串本身含有 ${ 時被誤判。
    [GeneratedRegex(@"^\$\{env:(?<name>[A-Za-z_][A-Za-z0-9_]*)\}$", RegexOptions.CultureInvariant)]
    private static partial Regex EnvironmentReferencePattern();
}
