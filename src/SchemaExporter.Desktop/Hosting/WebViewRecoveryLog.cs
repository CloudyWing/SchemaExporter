using System.Globalization;
using System.IO;
using System.Text;

namespace CloudyWing.SchemaExporter.Desktop.Hosting;

/// <summary>
/// 將 WebView 復原流程的事件寫入本機記錄檔，一行一筆，可由任何執行緒呼叫。
/// </summary>
/// <remarks>
/// 檔案超過 <see cref="MaxFileBytes"/> 時，舊檔改名為「原檔名.1」（覆蓋既有的 .1）後再寫入新檔。
/// 寫檔失敗時直接略過，不影響程式。
/// </remarks>
internal sealed class WebViewRecoveryLog {
    /// <summary>
    /// 記錄檔輪替的大小上限（位元組）。
    /// </summary>
    internal const long MaxFileBytes = 1024 * 1024;

    private static readonly UTF8Encoding Utf8NoBom = new(false);

    private readonly string filePath;
    private readonly TimeProvider timeProvider;
    private readonly Lock syncRoot = new();

    /// <summary>
    /// 初始化 <see cref="WebViewRecoveryLog"/> 類別的新執行個體。
    /// </summary>
    /// <param name="filePath">記錄檔完整路徑。</param>
    /// <param name="timeProvider">提供記錄時間的時間來源。</param>
    internal WebViewRecoveryLog(string filePath, TimeProvider timeProvider) {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(timeProvider);

        this.filePath = filePath;
        this.timeProvider = timeProvider;
    }

    /// <summary>
    /// 寫入一筆記錄，格式為「UTC ISO 8601 時間 事件 細節」。
    /// </summary>
    /// <param name="eventName">事件名稱，不含空白。</param>
    /// <param name="detail">細節；換行字元會被取代為空白。</param>
    internal void Write(string eventName, string? detail = null) {
        string timestamp = timeProvider.GetUtcNow().UtcDateTime
            .ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
        string line = string.IsNullOrWhiteSpace(detail)
            ? $"{timestamp} {eventName}"
            : $"{timestamp} {eventName} {detail.ReplaceLineEndings(" ")}";

        lock (syncRoot) {
            try {
                string? directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory)) {
                    Directory.CreateDirectory(directory);
                }

                RotateIfNeeded();
                File.AppendAllText(filePath, line + Environment.NewLine, Utf8NoBom);
            } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
                // 記錄失敗不可影響程式，直接略過。
            }
        }
    }

    /// <summary>
    /// 將例外格式化為記錄用的細節，內容為型別完整名稱與 HResult，不含訊息。
    /// </summary>
    /// <param name="exception">要格式化的例外。</param>
    /// <returns>格式為「型別完整名稱:0xHResult」的字串。</returns>
    internal static string FormatException(Exception exception) {
        ArgumentNullException.ThrowIfNull(exception);

        return $"{exception.GetType().FullName}:0x{exception.HResult:X8}";
    }

    private void RotateIfNeeded() {
        FileInfo info = new(filePath);
        if (info.Exists && info.Length > MaxFileBytes) {
            File.Move(filePath, filePath + ".1", overwrite: true);
        }
    }
}
