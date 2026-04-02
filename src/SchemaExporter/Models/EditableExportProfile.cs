using System.Collections.ObjectModel;
using CloudyWing.SchemaExporter.Core.Exporting;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CloudyWing.SchemaExporter.Models;

/// <summary>
/// 提供可於設定 UI 中編輯的匯出設定檔模型，支援屬性變更通知。
/// </summary>
internal sealed class EditableExportProfile : ObservableObject {
    /// <summary>
    /// 取得或設定設定檔名稱。
    /// </summary>
    public string Name {
        get;
        set => SetProperty(ref field, value);
    } = "Default";

    /// <summary>
    /// 取得或設定一個值，用以指出是否包含資料庫檢視表（View）。
    /// </summary>
    public bool IncludeViews {
        get;
        set => SetProperty(ref field, value);
    } = true;

    /// <summary>
    /// 取得要納入匯出範圍的 Schema 名稱集合。
    /// </summary>
    public ObservableCollection<string> IncludeSchemas { get; } = [];

    /// <summary>
    /// 取得要排除於匯出範圍的 Schema 名稱集合。
    /// </summary>
    public ObservableCollection<string> ExcludeSchemas { get; } = [];

    /// <summary>
    /// 取得要納入匯出範圍的物件名稱集合。
    /// </summary>
    public ObservableCollection<string> IncludeObjects { get; } = [];

    /// <summary>
    /// 取得要排除於匯出範圍的物件名稱集合。
    /// </summary>
    public ObservableCollection<string> ExcludeObjects { get; } = [];

    /// <summary>
    /// 取得或設定 <see cref="IncludeSchemas"/> 的多行文字表示，每行一個 Schema 名稱。
    /// </summary>
    public string IncludeSchemasText {
        get => JoinLines(IncludeSchemas);
        set => UpdateCollection(IncludeSchemas, value, nameof(IncludeSchemasText));
    }

    /// <summary>
    /// 取得或設定 <see cref="ExcludeSchemas"/> 的多行文字表示，每行一個 Schema 名稱。
    /// </summary>
    public string ExcludeSchemasText {
        get => JoinLines(ExcludeSchemas);
        set => UpdateCollection(ExcludeSchemas, value, nameof(ExcludeSchemasText));
    }

    /// <summary>
    /// 取得或設定 <see cref="IncludeObjects"/> 的多行文字表示，每行一個物件名稱。
    /// </summary>
    public string IncludeObjectsText {
        get => JoinLines(IncludeObjects);
        set => UpdateCollection(IncludeObjects, value, nameof(IncludeObjectsText));
    }

    /// <summary>
    /// 取得或設定 <see cref="ExcludeObjects"/> 的多行文字表示，每行一個物件名稱。
    /// </summary>
    public string ExcludeObjectsText {
        get => JoinLines(ExcludeObjects);
        set => UpdateCollection(ExcludeObjects, value, nameof(ExcludeObjectsText));
    }

    /// <summary>
    /// 從 <see cref="ExportProfile"/> 建立對應的 <see cref="EditableExportProfile"/> 執行個體。
    /// </summary>
    /// <param name="profile">來源匯出設定檔。</param>
    /// <returns>對應的可編輯匯出設定檔執行個體。</returns>
    public static EditableExportProfile FromExportProfile(ExportProfile profile) {
        ArgumentNullException.ThrowIfNull(profile);

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

    /// <summary>
    /// 將目前的可編輯匯出設定檔轉換為 <see cref="ExportProfile"/> 執行個體。
    /// </summary>
    /// <returns>包含已套用修剪處理的 <see cref="ExportProfile"/> 執行個體。</returns>
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
