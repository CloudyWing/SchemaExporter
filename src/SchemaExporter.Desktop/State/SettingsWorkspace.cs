using System.IO;
using CloudyWing.SchemaExporter.Core;
using CloudyWing.SchemaExporter.Core.Exporting;
using CloudyWing.SchemaExporter.Desktop.Models;
using CloudyWing.SchemaExporter.Infrastructure.Configuration;
using CloudyWing.SchemaExporter.Infrastructure.Security;

namespace CloudyWing.SchemaExporter.Desktop.State;

/// <summary>
/// 保存設定頁的編輯狀態，負責連線、匯出設定檔與 Redaction 規則的讀取、編輯與儲存。
/// </summary>
internal sealed class SettingsWorkspace {
    private readonly ISettingsService settingsService;
    private readonly IConnectionStringProtector protector;

    /// <summary>
    /// 初始化 <see cref="SettingsWorkspace"/> 類別的新執行個體。
    /// </summary>
    /// <param name="settingsService">設定檔存取服務。</param>
    /// <param name="protector">加密與解密連線字串的加密器。</param>
    public SettingsWorkspace(ISettingsService settingsService, IConnectionStringProtector protector) {
        ArgumentNullException.ThrowIfNull(settingsService);
        ArgumentNullException.ThrowIfNull(protector);

        this.settingsService = settingsService;
        this.protector = protector;
    }

    /// <summary>
    /// 取得可供編輯的連線設定。
    /// </summary>
    public List<EditableConnection> Connections { get; } = [];

    /// <summary>
    /// 取得可供編輯的匯出設定檔。
    /// </summary>
    public List<EditableExportProfile> Profiles { get; } = [];

    /// <summary>
    /// 取得系統支援的資料庫類型。
    /// </summary>
    public IReadOnlyList<DatabaseType> DatabaseTypes { get; } = Enum.GetValues<DatabaseType>();

    /// <summary>
    /// 取得或設定目前選取的連線設定。
    /// </summary>
    public EditableConnection? SelectedConnection { get; set; }

    /// <summary>
    /// 取得或設定目前選取的匯出設定檔。
    /// </summary>
    public EditableExportProfile? SelectedProfile { get; set; }

    /// <summary>
    /// 取得或設定一個值，用以指出是否在匯出前遮罩敏感 metadata。
    /// </summary>
    public bool RedactionEnabled { get; set; }

    /// <summary>
    /// 取得或設定遮罩後的替代文字。
    /// </summary>
    public string RedactionReplacementText { get; set; } = "[REDACTED]";

    /// <summary>
    /// 取得或設定敏感名稱 pattern，每行一筆。
    /// </summary>
    public string SensitiveNamePatternsText { get; set; } = "";

    /// <summary>
    /// 取得或設定敏感內容 pattern，每行一筆。
    /// </summary>
    public string SensitiveTextPatternsText { get; set; } = "";

    /// <summary>
    /// 取得最近一次載入或儲存失敗的訊息；<see langword="null"/> 表示沒有錯誤。
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// 取得一個值，用以指出設定是否已成功載入，可以編輯與儲存。
    /// </summary>
    public bool IsLoaded { get; private set; }

    /// <summary>
    /// 從設定檔載入設定，捨棄尚未儲存的編輯。不驗證內容，驗證在儲存時進行。
    /// </summary>
    /// <returns>代表非同步作業的工作。</returns>
    public async Task LoadAsync() {
        SchemaOptions options;
        try {
            // 不在載入時驗證，設定檔內容不合規則時仍可在此頁修正後儲存。
            options = await settingsService.ReadAsync();
        } catch (Exception ex) when (SettingsErrors.IsRecoverable(ex)) {
            IsLoaded = false;
            ErrorMessage = $"無法載入設定：{ex.Message}";
            return;
        }

        Connections.Clear();
        Connections.AddRange(options.Connections.Select(x => EditableConnection.FromSchemaConnection(x, protector)));
        Profiles.Clear();
        Profiles.AddRange(options.ExportProfiles.Select(EditableExportProfile.FromExportProfile));
        SelectedConnection = Connections.FirstOrDefault();
        SelectedProfile = Profiles.FirstOrDefault();

        SchemaRedactionOptions redaction = options.Redaction;
        RedactionEnabled = redaction.Enabled;
        RedactionReplacementText = redaction.ReplacementText;
        SensitiveNamePatternsText = MultilineText.Join(redaction.SensitiveNamePatterns);
        SensitiveTextPatternsText = MultilineText.Join(redaction.SensitiveTextPatterns);

        ErrorMessage = null;
        IsLoaded = true;
    }

    /// <summary>
    /// 驗證並儲存連線、設定檔與 Redaction 規則。匯出資料夾、輸出選項與上次選取項目以設定檔最新內容為準。
    /// </summary>
    /// <returns>儲存成功時回傳 <see langword="true"/>；驗證或寫入失敗時回傳 <see langword="false"/>，原因見 <see cref="ErrorMessage"/>。</returns>
    public async Task<bool> SaveAsync() {
        if (!IsLoaded) {
            return false;
        }

        try {
            // 重新讀取最新設定再合併，避免覆蓋匯出頁在本頁載入後儲存的預設值。
            SchemaOptions latestOptions = await settingsService.ReadAsync();
            SchemaOptions options = new() {
                ExportPath = latestOptions.ExportPath,
                Connections = [.. Connections.Select(x => x.ToSchemaConnection(protector))],
                ExportProfiles = [.. Profiles.Select(x => x.ToExportProfile())],
                ExportResultOptions = latestOptions.ExportResultOptions,
                Redaction = BuildRedactionOptions(),
                LastSelectedConnectionName = latestOptions.LastSelectedConnectionName,
                LastSelectedProfileName = latestOptions.LastSelectedProfileName
            };

            await settingsService.SaveAsync(options);
            ErrorMessage = null;
            return true;
        } catch (PlatformNotSupportedException ex) {
            ErrorMessage = $"無法加密連線字串：{ex.Message}";
            return false;
        } catch (Exception ex) when (SettingsErrors.IsRecoverable(ex) || ex is System.Security.Cryptography.CryptographicException) {
            ErrorMessage = ex is ExportValidationException ? ex.Message : $"無法儲存設定：{ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// 新增一個具有唯一名稱的連線設定，並將其設為目前選取項目。
    /// </summary>
    public void AddConnection() {
        EditableConnection connection = new() {
            Name = CreateUniqueName("New Connection", Connections.Select(x => x.Name))
        };
        Connections.Add(connection);
        SelectedConnection = connection;
    }

    /// <summary>
    /// 移除目前選取的連線設定，並自動選取相鄰項目。
    /// </summary>
    public void RemoveSelectedConnection() {
        if (SelectedConnection is null) {
            return;
        }

        int removedIndex = Connections.IndexOf(SelectedConnection);
        Connections.Remove(SelectedConnection);
        SelectedConnection = Connections.Count == 0
            ? null
            : Connections[Math.Min(removedIndex, Connections.Count - 1)];
    }

    /// <summary>
    /// 新增一個具有唯一名稱的匯出設定檔，並將其設為目前選取項目。
    /// </summary>
    public void AddProfile() {
        EditableExportProfile profile = new() {
            Name = CreateUniqueName("New Profile", Profiles.Select(x => x.Name)),
            IncludeViews = true
        };
        Profiles.Add(profile);
        SelectedProfile = profile;
    }

    /// <summary>
    /// 移除目前選取的匯出設定檔，並清除所有連線對此設定檔的參照。
    /// </summary>
    public void RemoveSelectedProfile() {
        if (SelectedProfile is null) {
            return;
        }

        string removedProfileName = SelectedProfile.Name;
        int removedIndex = Profiles.IndexOf(SelectedProfile);
        Profiles.Remove(SelectedProfile);

        foreach (EditableConnection connection in Connections.Where(x =>
            string.Equals(x.ExportProfileName, removedProfileName, StringComparison.OrdinalIgnoreCase)
        )) {
            connection.ExportProfileName = null;
        }

        SelectedProfile = Profiles.Count == 0
            ? null
            : Profiles[Math.Min(removedIndex, Profiles.Count - 1)];
    }

    /// <summary>
    /// 變更匯出設定檔名稱，並同步更新參照舊名稱的連線。
    /// </summary>
    /// <param name="profile">要變更名稱的設定檔。</param>
    /// <param name="newName">新名稱。</param>
    public void RenameProfile(EditableExportProfile profile, string? newName) {
        ArgumentNullException.ThrowIfNull(profile);

        string previousName = profile.Name;
        string name = newName ?? "";
        if (string.Equals(previousName, name, StringComparison.Ordinal)) {
            return;
        }

        profile.Name = name;
        foreach (EditableConnection connection in Connections.Where(x =>
            string.Equals(x.ExportProfileName, previousName, StringComparison.OrdinalIgnoreCase)
        )) {
            connection.ExportProfileName = name;
        }
    }

    private SchemaRedactionOptions BuildRedactionOptions() {
        List<string> namePatterns = [];
        MultilineText.ReplaceWith(namePatterns, SensitiveNamePatternsText);
        List<string> textPatterns = [];
        MultilineText.ReplaceWith(textPatterns, SensitiveTextPatternsText);

        return new SchemaRedactionOptions {
            Enabled = RedactionEnabled,
            ReplacementText = RedactionReplacementText,
            SensitiveNamePatterns = namePatterns,
            SensitiveTextPatterns = textPatterns
        };
    }

    private static string CreateUniqueName(string prefix, IEnumerable<string> existingNames) {
        HashSet<string> usedNames = existingNames
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (usedNames.Add(prefix)) {
            return prefix;
        }

        for (int index = 1; ; index++) {
            string candidateName = $"{prefix} {index}";
            if (usedNames.Add(candidateName)) {
                return candidateName;
            }
        }
    }
}
