using System.Collections.ObjectModel;
using CloudyWing.SchemaExporter.Core;
using CloudyWing.SchemaExporter.Core.Exporting;
using CloudyWing.SchemaExporter.Models;
using CloudyWing.SchemaExporter.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CloudyWing.SchemaExporter.ViewModels;

internal sealed class SettingsViewModel : ObservableObject {
    private readonly ISettingsService settingsService;
    private SchemaOptions loadedOptions = new();
    private EditableConnection? selectedConnection;
    private EditableExportProfile? selectedProfile;
    private readonly Dictionary<EditableExportProfile, string> profileNames = [];

    public SettingsViewModel(ISettingsService settingsService) {
        ArgumentNullException.ThrowIfNull(settingsService, nameof(settingsService));
        this.settingsService = settingsService;
    }

    public ObservableCollection<EditableConnection> Connections { get; } = [];

    public ObservableCollection<EditableExportProfile> Profiles { get; } = [];

    public IReadOnlyList<DatabaseType> DatabaseTypes { get; } = Enum.GetValues<DatabaseType>();

    public EditableConnection? SelectedConnection {
        get => selectedConnection;
        set => SetProperty(ref selectedConnection, value);
    }

    public EditableExportProfile? SelectedProfile {
        get => selectedProfile;
        set => SetProperty(ref selectedProfile, value);
    }

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

    public async Task SaveAsync() {
        SchemaOptions options = BuildOptions();
        await settingsService.SaveAsync(options);
        loadedOptions = options;
        profileNames.Clear();
        foreach (EditableExportProfile profile in Profiles) {
            profileNames[profile] = profile.Name;
        }
    }

    public void AddConnection() {
        EditableConnection connection = new() {
            Name = CreateUniqueName("New Connection", Connections.Select(x => x.Name))
        };
        Connections.Add(connection);
        SelectedConnection = connection;
    }

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

    public void AddProfile() {
        EditableExportProfile profile = new() {
            Name = CreateUniqueName("New Profile", Profiles.Select(x => x.Name)),
            IncludeViews = true
        };
        AttachProfile(profile);
        Profiles.Add(profile);
        SelectedProfile = profile;
    }

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
                GenerateSchemaSnapshot = loadedOptions.ExportResultOptions.GenerateSchemaSnapshot,
                DiffSourceSnapshotPath = loadedOptions.ExportResultOptions.DiffSourceSnapshotPath
            }
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