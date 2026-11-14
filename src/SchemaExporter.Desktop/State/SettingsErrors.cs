using System.IO;
using System.Text.Json;
using CloudyWing.SchemaExporter.Core.Exporting;

namespace CloudyWing.SchemaExporter.Desktop.State;

/// <summary>
/// 判斷設定檔讀寫例外是否屬於可回報給使用者、不需中止畫面的錯誤。
/// </summary>
internal static class SettingsErrors {
    /// <summary>
    /// 判斷例外是否為設定檔讀寫時可預期的錯誤。
    /// </summary>
    /// <param name="ex">要判斷的例外。</param>
    /// <returns>可預期的設定錯誤時回傳 <see langword="true"/>；否則回傳 <see langword="false"/>。</returns>
    public static bool IsRecoverable(Exception ex) {
        return ex is IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or JsonException
            or ExportValidationException;
    }
}
