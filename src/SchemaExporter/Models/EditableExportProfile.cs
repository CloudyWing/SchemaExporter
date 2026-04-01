using System.Collections.ObjectModel;
using CloudyWing.SchemaExporter.Core.Exporting;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CloudyWing.SchemaExporter.Models;

internal sealed class EditableExportProfile : ObservableObject {
    private string name = "Default";
    private bool includeViews = true;

    public string Name {
        get => name;
        set => SetProperty(ref name, value);
    }

    public bool IncludeViews {
        get => includeViews;
        set => SetProperty(ref includeViews, value);
    }

    public ObservableCollection<string> IncludeSchemas { get; } = [];

    public ObservableCollection<string> ExcludeSchemas { get; } = [];

    public ObservableCollection<string> IncludeObjects { get; } = [];

    public ObservableCollection<string> ExcludeObjects { get; } = [];

    public string IncludeSchemasText {
        get => JoinLines(IncludeSchemas);
        set => UpdateCollection(IncludeSchemas, value, nameof(IncludeSchemasText));
    }

    public string ExcludeSchemasText {
        get => JoinLines(ExcludeSchemas);
        set => UpdateCollection(ExcludeSchemas, value, nameof(ExcludeSchemasText));
    }

    public string IncludeObjectsText {
        get => JoinLines(IncludeObjects);
        set => UpdateCollection(IncludeObjects, value, nameof(IncludeObjectsText));
    }

    public string ExcludeObjectsText {
        get => JoinLines(ExcludeObjects);
        set => UpdateCollection(ExcludeObjects, value, nameof(ExcludeObjectsText));
    }

    public static EditableExportProfile FromExportProfile(ExportProfile profile) {
        ArgumentNullException.ThrowIfNull(profile, nameof(profile));

        EditableExportProfile editable = new() {
            Name = profile.Name,
            IncludeViews = profile.IncludeViews
        };
        AddRange(editable.IncludeSchemas, profile.IncludeSchemas);
        AddRange(editable.ExcludeSchemas, profile.ExcludeSchemas);
        AddRange(editable.IncludeObjects, profile.IncludeObjects);
        AddRange(editable.ExcludeObjects, profile.ExcludeObjects);
        return editable;
    }

    public ExportProfile ToExportProfile() {
        return new ExportProfile {
            Name = Name.Trim(),
            IncludeSchemas = [.. IncludeSchemas],
            ExcludeSchemas = [.. ExcludeSchemas],
            IncludeObjects = [.. IncludeObjects],
            ExcludeObjects = [.. ExcludeObjects],
            IncludeViews = IncludeViews
        };
    }

    private static void AddRange(ObservableCollection<string> collection, IEnumerable<string> values) {
        foreach (string value in values.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim())) {
            collection.Add(value);
        }
    }

    private static string JoinLines(IEnumerable<string> values) {
        return string.Join(Environment.NewLine, values);
    }

    private void UpdateCollection(ObservableCollection<string> collection, string? value, string propertyName) {
        List<string> normalized = value?
            .Split(["\r\n", "\n"], StringSplitOptions.None)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];

        if (collection.SequenceEqual(normalized, StringComparer.Ordinal)) {
            return;
        }

        collection.Clear();
        foreach (string item in normalized) {
            collection.Add(item);
        }

        OnPropertyChanged(propertyName);
    }
}