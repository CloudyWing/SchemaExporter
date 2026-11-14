namespace CloudyWing.SchemaExporter.Core.Exporting;

/// <summary>
/// 依應用程式設定與執行時覆寫值解析 schema 匯出請求。
/// </summary>
public sealed class SchemaExportRequestResolver {
    private const string DefaultProfileName = "Default";
    private readonly IConnectionStringResolver? connectionStringResolver;

    /// <summary>
    /// 初始化 <see cref="SchemaExportRequestResolver"/> 類別的新執行個體，連線字串照設定檔原值使用。
    /// </summary>
    public SchemaExportRequestResolver() : this(null) {
    }

    /// <summary>
    /// 初始化 <see cref="SchemaExportRequestResolver"/> 類別的新執行個體，並指定連線字串的解析方式。
    /// </summary>
    /// <param name="connectionStringResolver">連線字串解析器；<see langword="null"/> 表示照設定檔原值使用。</param>
    public SchemaExportRequestResolver(IConnectionStringResolver? connectionStringResolver) {
        this.connectionStringResolver = connectionStringResolver;
    }

    /// <summary>
    /// 解析匯出請求所需的連線、設定檔、輸出路徑與輸出選項。
    /// </summary>
    /// <param name="schemaOptions">Schema 匯出設定。</param>
    /// <param name="connectionName">指定的連線名稱；空白時使用上次選取或第一筆連線。</param>
    /// <param name="profileName">指定的匯出設定檔名稱；空白時使用連線預設或第一筆設定檔。</param>
    /// <param name="overrides">執行時輸出選項覆寫值。</param>
    /// <returns>完整的匯出請求。</returns>
    public SchemaExportRequest Resolve(
        SchemaOptions schemaOptions,
        string? connectionName,
        string? profileName,
        ExportOptionOverrides? overrides = null
    ) {
        ArgumentNullException.ThrowIfNull(schemaOptions);

        SchemaConnection connection = ResolveConnection(schemaOptions, connectionName);
        ExportProfile profile = ResolveProfile(schemaOptions, connection, profileName);
        ExportResultOptions resultOptions = ResolveResultOptions(schemaOptions.ExportResultOptions, overrides);
        string exportPath = string.IsNullOrWhiteSpace(overrides?.OutputPath)
            ? schemaOptions.ExportPath
            : overrides.OutputPath.Trim();

        return new SchemaExportRequest {
            Connection = ResolveConnectionString(connection),
            ExportPath = exportPath,
            Profile = profile,
            ResultOptions = resultOptions,
            Redaction = schemaOptions.Redaction
        };
    }

    /// <summary>
    /// 依名稱（不分大小寫）尋找連線設定。
    /// </summary>
    /// <param name="schemaOptions">Schema 設定。</param>
    /// <param name="connectionName">連線名稱；空白時回傳 <see langword="null"/>。</param>
    /// <returns>符合名稱的連線設定；找不到時回傳 <see langword="null"/>。</returns>
    public static SchemaConnection? FindConnection(SchemaOptions schemaOptions, string? connectionName) {
        ArgumentNullException.ThrowIfNull(schemaOptions);

        if (string.IsNullOrWhiteSpace(connectionName)) {
            return null;
        }

        return schemaOptions.Connections.FirstOrDefault(x =>
            string.Equals(x.Name, connectionName, StringComparison.OrdinalIgnoreCase)
        );
    }

    /// <summary>
    /// 取得可供選擇的匯出設定檔；設定檔沒有任何 Profile 時提供一個不篩選的 <c>Default</c>。
    /// </summary>
    /// <param name="schemaOptions">Schema 設定。</param>
    /// <returns>可供選擇的匯出設定檔，至少一筆。</returns>
    public static IReadOnlyList<ExportProfile> GetAvailableProfiles(SchemaOptions schemaOptions) {
        ArgumentNullException.ThrowIfNull(schemaOptions);

        return schemaOptions.ExportProfiles.Count > 0
            ? schemaOptions.ExportProfiles
            : [new ExportProfile { Name = DefaultProfileName }];
    }

    /// <summary>
    /// 決定要套用的匯出設定檔名稱：明確指定的名稱優先，其次是連線設定的 <see cref="SchemaConnection.ExportProfileName"/>。
    /// </summary>
    /// <param name="requestedProfileName">明確指定的設定檔名稱。</param>
    /// <param name="connection">目前的連線設定。</param>
    /// <returns>要套用的設定檔名稱；兩者皆未指定時回傳 <see langword="null"/>，表示使用第一筆設定檔。</returns>
    public static string? SelectProfileName(string? requestedProfileName, SchemaConnection? connection) {
        return !string.IsNullOrWhiteSpace(requestedProfileName)
            ? requestedProfileName
            : connection?.ExportProfileName;
    }

    /// <summary>
    /// 依名稱（不分大小寫）尋找匯出設定檔。
    /// </summary>
    /// <param name="profiles">可供選擇的匯出設定檔。</param>
    /// <param name="profileName">設定檔名稱；空白時回傳 <see langword="null"/>。</param>
    /// <returns>符合名稱的設定檔；找不到時回傳 <see langword="null"/>。</returns>
    public static ExportProfile? FindProfile(IReadOnlyList<ExportProfile> profiles, string? profileName) {
        ArgumentNullException.ThrowIfNull(profiles);

        if (string.IsNullOrWhiteSpace(profileName)) {
            return null;
        }

        return profiles.FirstOrDefault(x =>
            string.Equals(x.Name, profileName, StringComparison.OrdinalIgnoreCase)
        );
    }

    private SchemaConnection ResolveConnectionString(SchemaConnection connection) {
        if (connectionStringResolver is null) {
            return connection;
        }

        // 回傳複本，避免把解析後的實際連線字串寫回設定物件，之後儲存設定時外洩成明文。
        return new SchemaConnection {
            Name = connection.Name,
            DatabaseType = connection.DatabaseType,
            ConnectionString = connectionStringResolver.Resolve(connection),
            ExportProfileName = connection.ExportProfileName
        };
    }

    private static SchemaConnection ResolveConnection(SchemaOptions schemaOptions, string? connectionName) {
        string? requestedConnectionName = !string.IsNullOrWhiteSpace(connectionName)
            ? connectionName
            : schemaOptions.LastSelectedConnectionName;

        if (!string.IsNullOrWhiteSpace(requestedConnectionName)) {
            return FindConnection(schemaOptions, requestedConnectionName)
                ?? throw new ExportValidationException($"找不到名稱為「{requestedConnectionName}」的連線設定。");
        }

        return schemaOptions.Connections.FirstOrDefault()
            ?? throw new ExportValidationException("請先設定至少一筆資料庫連線。");
    }

    private static ExportProfile ResolveProfile(
        SchemaOptions schemaOptions,
        SchemaConnection connection,
        string? profileName
    ) {
        IReadOnlyList<ExportProfile> profiles = GetAvailableProfiles(schemaOptions);
        string? requestedProfileName = SelectProfileName(profileName, connection);
        if (string.IsNullOrWhiteSpace(requestedProfileName)) {
            return profiles[0];
        }

        return FindProfile(profiles, requestedProfileName)
            ?? throw new ExportValidationException($"找不到名稱為「{requestedProfileName}」的匯出設定檔。");
    }

    private static ExportResultOptions ResolveResultOptions(
        ExportResultOptions defaults,
        ExportOptionOverrides? overrides
    ) {
        ArgumentNullException.ThrowIfNull(defaults);

        return new ExportResultOptions {
            UseTimestamp = overrides?.UseTimestamp ?? defaults.UseTimestamp,
            TimestampFormat = string.IsNullOrWhiteSpace(overrides?.TimestampFormat)
                ? defaults.TimestampFormat
                : overrides.TimestampFormat.Trim(),
            OverwriteStrategy = overrides?.OverwriteStrategy ?? defaults.OverwriteStrategy,
            OpenOutputFolder = overrides?.OpenOutputFolder ?? defaults.OpenOutputFolder,
            GenerateManifest = overrides?.GenerateManifest ?? defaults.GenerateManifest,
            GenerateJsonSidecar = overrides?.GenerateJsonSidecar ?? defaults.GenerateJsonSidecar,
            GenerateMarkdownSidecar = overrides?.GenerateMarkdownSidecar ?? defaults.GenerateMarkdownSidecar,
            GenerateSchemaSummary = overrides?.GenerateSchemaSummary ?? defaults.GenerateSchemaSummary,
            GenerateSchemaSnapshot = overrides?.GenerateSchemaSnapshot ?? defaults.GenerateSchemaSnapshot,
            DiffSourceSnapshotPath = ResolveDiffSourceSnapshotPath(defaults, overrides)
        };
    }

    private static string? ResolveDiffSourceSnapshotPath(
        ExportResultOptions defaults,
        ExportOptionOverrides? overrides
    ) {
        if (overrides?.OverrideDiffSourceSnapshotPath == true) {
            return string.IsNullOrWhiteSpace(overrides.DiffSourceSnapshotPath)
                ? null
                : overrides.DiffSourceSnapshotPath.Trim();
        }

        return defaults.DiffSourceSnapshotPath;
    }
}
