using System.Collections.ObjectModel;
using CloudyWing.SchemaExporter.Core;
using CloudyWing.SchemaExporter.Core.Exporting;
using CloudyWing.SchemaExporter.Models;
using CloudyWing.SchemaExporter.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CloudyWing.SchemaExporter.ViewModels;

/// <summary>
/// 提供設定視窗使用的 ViewModel，負責管理連線與匯出設定檔的讀取、編輯與儲存。
/// </summary>
internal sealed class SettingsViewModel : ObservableObject {
    private readonly ISettingsService settingsService;
    private SchemaOptions loadedOptions = CreateEmptyOptions();
    private readonly Dictionary<EditableExportProfile, string> profileNames = [];

    /// <summary>
    /// 初始化 <see cref="SettingsViewModel"/> 類別的新執行個體。
    /// </summary>
    /// <param name="settingsService">設定檔存取服務。</param>
    public SettingsViewModel(ISettingsService settingsService) {
        ArgumentNullException.ThrowIfNull(settingsService);
        this.settingsService = settingsService;
    }

    /// <summary>
    /// 取得可供編輯的連線設定集合。
    /// </summary>
    public ObservableCollection<EditableConnection> Connections { get; } = [];

    /// <summary>
    /// 取得可供編輯的匯出設定檔集合。
    /// </summary>
    public ObservableCollection<EditableExportProfile> Profiles { get; } = [];

    /// <summary>
    /// 取得系統支援的資料庫類型清單。
    /// </summary>
    public IReadOnlyList<DatabaseType> DatabaseTypes { get; } = Enum.GetValues<DatabaseType>();

    /// <summary>
    /// 取得或設定目前選取的連線設定。
    /// </summary>
    public EditableConnection? SelectedConnection {
        get;
        set => SetProperty(ref field, value);
    }

    /// <summary>
    /// 取得或設定目前選取的匯出設定檔。
    /// </summary>
    public EditableExportProfile? SelectedProfile {
        get;
        set => SetProperty(ref field, value);
    }

    /// <summary>
    /// 非同步從設定服務載入設定，並初始化可編輯的連線與設定檔集合。
    /// </summary>
    /// <returns>代表非同步作業的工作。</returns>
    public async Task LoadAsync() {
        SchemaOptions options = await settingsService.LoadAsync();
        loadedOptions = options;

        foreach (EditableExportProfile profile in Profiles) {
            DetachProfile(profile);
        }

        Connections.Clear();
        foreach (EditableConnection connection in options.Connections.Select(EditableConnection.FromSchemaConnection)) {
            Connections.Add(connection);
        }

        Profiles.Clear();
        profileNames.Clear();
        foreach (EditableExportProfile profile in options.ExportProfiles.Select(EditableExportProfile.FromExportProfile)) {
            AttachProfile(profile);
            Profiles.Add(profile);
        }

        SelectedConnection = Connections.FirstOrDefault();
        SelectedProfile = Profiles.FirstOrDefault();
    }

    /// <summary>
    /// 非同步將目前的連線與設定檔集合儲存至設定服務。
    /// </summary>
    /// <returns>代表非同步作業的工作。</returns>
    public async Task SaveAsync() {
        SchemaOptions options = BuildOptions();
        await settingsService.SaveAsync(options);
        loadedOptions = options;
        profileNames.Clear();
        foreach (EditableExportProfile profile in Profiles) {
            profileNames[profile] = profile.Name;
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
        AttachProfile(profile);
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
        DetachProfile(SelectedProfile);
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

    private SchemaOptions BuildOptions() {
        return new SchemaOptions {
            ExportPath = loadedOptions.ExportPath,
            Connections = [.. Connections.Select(x => x.ToSchemaConnection())],
            ExportProfiles = [.. Profiles.Select(x => x.ToExportProfile())],
            ExportResultOptions = new ExportResultOptions {
                UseTimestamp = loadedOptions.ExportResultOptions.UseTimestamp,
                TimestampFormat = loadedOptions.ExportResultOptions.TimestampFormat,
                OverwriteStrategy = loadedOptions.ExportResultOptions.OverwriteStrategy,
                OpenOutputFolder = loadedOptions.ExportResultOptions.OpenOutputFolder,
                GenerateManifest = loadedOptions.ExportResultOptions.GenerateManifest,
                GenerateJsonSidecar = loadedOptions.ExportResultOptions.GenerateJsonSidecar,
                GenerateMarkdownSidecar = loadedOptions.ExportResultOptions.GenerateMarkdownSidecar,
                GenerateSchemaSummary = loadedOptions.ExportResultOptions.GenerateSchemaSummary,
                GenerateSchemaSnapshot = loadedOptions.ExportResultOptions.GenerateSchemaSnapshot,
                DiffSourceSnapshotPath = loadedOptions.ExportResultOptions.DiffSourceSnapshotPath
            }
        };
    }

    private static SchemaOptions CreateEmptyOptions() {
        return new SchemaOptions {
            ExportPath = "",
            Connections = [],
            ExportProfiles = [],
            ExportResultOptions = new ExportResultOptions()
        };
    }

    private void AttachProfile(EditableExportProfile profile) {
        profile.PropertyChanged += HandleProfilePropertyChanged;
        profileNames[profile] = profile.Name;
    }

    private void DetachProfile(EditableExportProfile profile) {
        profile.PropertyChanged -= HandleProfilePropertyChanged;
        profileNames.Remove(profile);
    }

    private void HandleProfilePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) {
        if (e.PropertyName != nameof(EditableExportProfile.Name) || sender is not EditableExportProfile profile) {
            return;
        }

        string previousName = profileNames.TryGetValue(profile, out string? trackedName)
            ? trackedName
            : profile.Name;
        if (string.Equals(previousName, profile.Name, StringComparison.Ordinal)) {
            return;
        }

        foreach (EditableConnection connection in Connections.Where(x =>
            string.Equals(x.ExportProfileName, previousName, StringComparison.OrdinalIgnoreCase)
        )) {
            connection.ExportProfileName = profile.Name;
        }

        profileNames[profile] = profile.Name;
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
