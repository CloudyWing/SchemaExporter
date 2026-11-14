using CloudyWing.SchemaExporter.Core.Exporting;

namespace CloudyWing.SchemaExporter.Desktop.Models;

/// <summary>
/// 提供可於設定頁編輯的匯出設定檔模型。
/// </summary>
internal sealed class EditableExportProfile {
    /// <summary>
    /// 取得編輯期間的穩定識別碼，供畫面在名稱變更時仍能辨識同一筆設定檔。
    /// </summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>
    /// 取得或設定設定檔名稱。名稱變更須透過 <see cref="State.SettingsWorkspace.RenameProfile"/>，才會同步更新連線的參照。
    /// </summary>
    public string Name { get; set; } = "Default";

    /// <summary>
    /// 取得或設定一個值，用以指出是否包含資料庫檢視表（View）。
    /// </summary>
    public bool IncludeViews { get; set; } = true;

    /// <summary>
    /// 取得要納入匯出範圍的 Schema 名稱集合。
    /// </summary>
    public List<string> IncludeSchemas { get; } = [];

    /// <summary>
    /// 取得要排除於匯出範圍的 Schema 名稱集合。
    /// </summary>
    public List<string> ExcludeSchemas { get; } = [];

    /// <summary>
    /// 取得要納入匯出範圍的物件名稱集合。
    /// </summary>
    public List<string> IncludeObjects { get; } = [];

    /// <summary>
    /// 取得要排除於匯出範圍的物件名稱集合。
    /// </summary>
    public List<string> ExcludeObjects { get; } = [];

    /// <summary>
    /// 取得或設定 <see cref="IncludeSchemas"/> 的多行文字表示，每行一個 Schema 名稱。
    /// </summary>
    public string IncludeSchemasText {
        get => MultilineText.Join(IncludeSchemas);
        set => MultilineText.ReplaceWith(IncludeSchemas, value);
    }

    /// <summary>
    /// 取得或設定 <see cref="ExcludeSchemas"/> 的多行文字表示，每行一個 Schema 名稱。
    /// </summary>
    public string ExcludeSchemasText {
        get => MultilineText.Join(ExcludeSchemas);
        set => MultilineText.ReplaceWith(ExcludeSchemas, value);
    }

    /// <summary>
    /// 取得或設定 <see cref="IncludeObjects"/> 的多行文字表示，每行一個物件名稱。
    /// </summary>
    public string IncludeObjectsText {
        get => MultilineText.Join(IncludeObjects);
        set => MultilineText.ReplaceWith(IncludeObjects, value);
    }

    /// <summary>
    /// 取得或設定 <see cref="ExcludeObjects"/> 的多行文字表示，每行一個物件名稱。
    /// </summary>
    public string ExcludeObjectsText {
        get => MultilineText.Join(ExcludeObjects);
        set => MultilineText.ReplaceWith(ExcludeObjects, value);
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
        MultilineText.ReplaceWith(editable.IncludeSchemas, profile.IncludeSchemas);
        MultilineText.ReplaceWith(editable.ExcludeSchemas, profile.ExcludeSchemas);
        MultilineText.ReplaceWith(editable.IncludeObjects, profile.IncludeObjects);
        MultilineText.ReplaceWith(editable.ExcludeObjects, profile.ExcludeObjects);
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
}
