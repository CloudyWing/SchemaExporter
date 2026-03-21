using CloudyWing.SchemaExporter.Core.Exporting;

namespace CloudyWing.SchemaExporter.Core;

/// <summary>
/// 結構描述匯出作業的組態選項。
/// </summary>
public class SchemaOptions {
    /// <summary>
    /// 取得組態區段名稱。
    /// </summary>
    public const string OptionsName = "Schema";

    /// <summary>
    /// 取得或設定匯出輸出的基底目錄路徑。
    /// </summary>
    public string ExportPath { get; set; } = "";

    /// <summary>
    /// 取得可用的資料庫連線清單。
    /// </summary>
    public IReadOnlyList<SchemaConnection> Connections { get; init; } = [];

    /// <summary>
    /// 取得定義篩選條件與偏好設定的可用匯出設定檔清單。
    /// </summary>
    public IReadOnlyList<ExportProfile> ExportProfiles { get; init; } = [];

    /// <summary>
    /// 取得或設定預設的匯出結果選項，用於檔案命名與匯出後動作。
    /// </summary>
    public ExportResultOptions ExportResultOptions { get; set; } = new();
}

