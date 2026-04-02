using CloudyWing.SchemaExporter.Core;

namespace CloudyWing.SchemaExporter.Services;

/// <summary>
/// 定義應用程式設定的讀取、儲存與驗證作業。
/// </summary>
internal interface ISettingsService {
    /// <summary>
    /// 非同步從設定來源載入 Schema 選項。
    /// </summary>
    /// <returns>載入後的 <see cref="SchemaOptions"/> 執行個體。</returns>
    Task<SchemaOptions> LoadAsync();

    /// <summary>
    /// 非同步將 Schema 選項儲存至設定來源。
    /// </summary>
    /// <param name="options">要儲存的 <see cref="SchemaOptions"/> 執行個體。</param>
    /// <returns>代表非同步作業的工作。</returns>
    Task SaveAsync(SchemaOptions options);

    /// <summary>
    /// 非同步驗證 Schema 選項的內容是否合法。
    /// </summary>
    /// <param name="options">要驗證的 <see cref="SchemaOptions"/> 執行個體。</param>
    /// <returns>驗證通過時回傳 <see langword="true"/>；否則擲回例外。</returns>
    Task<bool> ValidateAsync(SchemaOptions options);
}