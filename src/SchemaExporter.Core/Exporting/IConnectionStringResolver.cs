namespace CloudyWing.SchemaExporter.Core.Exporting;

/// <summary>
/// 將設定檔中保存的連線字串值解析為實際連線使用的連線字串，例如展開環境變數參照或解密。
/// </summary>
public interface IConnectionStringResolver {
    /// <summary>
    /// 解析指定連線的連線字串。
    /// </summary>
    /// <param name="connection">設定檔中的連線設定，<see cref="SchemaConnection.ConnectionString"/> 為保存的原始值。</param>
    /// <returns>實際連線使用的連線字串。</returns>
    /// <exception cref="ExportValidationException">保存的值無法解析時，例如環境變數未設定或無法解密。</exception>
    string Resolve(SchemaConnection connection);
}
