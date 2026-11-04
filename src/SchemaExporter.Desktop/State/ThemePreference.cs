using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CloudyWing.SchemaExporter.Desktop.State;

/// <summary>
/// 保存 Desktop 的主題偏好，寫入使用者設定目錄的 <c>desktop.json</c>。
/// </summary>
/// <remarks>
/// 主題只影響 Desktop，因此不寫入與 CLI 共用的 <c>appsettings.json</c>，也不改動 Core 的設定契約。
/// </remarks>
internal sealed class ThemePreference {
    private static readonly JsonSerializerOptions SerializerOptions = new() {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string filePath;

    /// <summary>
    /// 初始化 <see cref="ThemePreference"/> 類別的新執行個體，並同步讀取已儲存的主題。
    /// </summary>
    /// <param name="filePath">偏好檔完整路徑。</param>
    /// <remarks>
    /// 建構時同步讀取，讓第一次繪製就套用正確主題，避免畫面先以預設主題閃一下。檔案缺漏或損毀時使用 <see cref="ThemeMode.System"/>。
    /// </remarks>
    public ThemePreference(string filePath) {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        this.filePath = filePath;
        Mode = ReadMode(filePath);
    }

    /// <summary>
    /// 在主題模式變更時觸發。
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// 取得目前的主題模式。
    /// </summary>
    public ThemeMode Mode { get; private set; }

    /// <summary>
    /// 取得最近一次儲存失敗的訊息；<see langword="null"/> 表示沒有錯誤。
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// 立即套用並儲存主題模式。寫入失敗時仍套用於目前執行階段，原因見 <see cref="ErrorMessage"/>。
    /// </summary>
    /// <param name="mode">要套用的主題模式。</param>
    /// <returns>代表非同步作業的工作。</returns>
    public async Task SetModeAsync(ThemeMode mode) {
        if (mode == Mode) {
            return;
        }

        Mode = mode;
        Changed?.Invoke();

        try {
            string? directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory)) {
                Directory.CreateDirectory(directory);
            }

            // 先寫暫存檔再取代，避免寫到一半中斷時留下損毀的偏好檔。
            string tempPath = filePath + ".tmp";
            string json = JsonSerializer.Serialize(new PreferenceDocument { ThemeMode = mode }, SerializerOptions);
            await File.WriteAllTextAsync(tempPath, json + Environment.NewLine, new UTF8Encoding(false));
            File.Move(tempPath, filePath, overwrite: true);
            ErrorMessage = null;
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            ErrorMessage = $"無法儲存主題設定：{ex.Message}";
            Changed?.Invoke();
        }
    }

    private static ThemeMode ReadMode(string filePath) {
        try {
            if (!File.Exists(filePath)) {
                return ThemeMode.System;
            }

            PreferenceDocument? document = JsonSerializer.Deserialize<PreferenceDocument>(File.ReadAllText(filePath, Encoding.UTF8), SerializerOptions);
            return document?.ThemeMode is ThemeMode mode && Enum.IsDefined(mode) ? mode : ThemeMode.System;
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) {
            return ThemeMode.System;
        }
    }

    private sealed class PreferenceDocument {
        public ThemeMode? ThemeMode { get; init; }
    }
}
