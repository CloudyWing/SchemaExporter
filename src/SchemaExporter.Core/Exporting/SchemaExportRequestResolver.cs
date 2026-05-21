namespace CloudyWing.SchemaExporter.Core.Exporting;

/// <summary>
/// 依應用程式設定與執行時覆寫值解析 schema 匯出請求。
/// </summary>
public sealed class SchemaExportRequestResolver {
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
            Connection = connection,
            ExportPath = exportPath,
            Profile = profile,
            ResultOptions = resultOptions,
            Redaction = schemaOptions.Redaction
        };
    }

    private static SchemaConnection ResolveConnection(SchemaOptions schemaOptions, string? connectionName) {
        string? requestedConnectionName = !string.IsNullOrWhiteSpace(connectionName)
            ? connectionName
            : schemaOptions.LastSelectedConnectionName;

        if (!string.IsNullOrWhiteSpace(requestedConnectionName)) {
            SchemaConnection? matchedConnection = schemaOptions.Connections.FirstOrDefault(x =>
                string.Equals(x.Name, requestedConnectionName, StringComparison.OrdinalIgnoreCase)
            );

            if (matchedConnection is not null) {
                return matchedConnection;
            }

            throw new ExportValidationException($"找不到名稱為「{requestedConnectionName}」的連線設定。");
        }

        return schemaOptions.Connections.FirstOrDefault()
            ?? throw new ExportValidationException("請先設定至少一筆資料庫連線。");
    }

    private static ExportProfile ResolveProfile(
        SchemaOptions schemaOptions,
        SchemaConnection connection,
        string? profileName
    ) {
        string? requestedProfileName = !string.IsNullOrWhiteSpace(profileName)
            ? profileName
            : connection.ExportProfileName;

        if (!string.IsNullOrWhiteSpace(requestedProfileName)) {
            ExportProfile? matchedProfile = schemaOptions.ExportProfiles.FirstOrDefault(x =>
                string.Equals(x.Name, requestedProfileName, StringComparison.OrdinalIgnoreCase)
            );

            if (matchedProfile is not null) {
                return matchedProfile;
            }

            throw new ExportValidationException($"找不到名稱為「{requestedProfileName}」的匯出設定檔。");
        }

        return schemaOptions.ExportProfiles.FirstOrDefault()
            ?? new ExportProfile {
                Name = "Default"
            };
    }

    private static ExportResultOptions ResolveResultOptions(
        ExportResultOptions defaults,
        ExportOptionOverrides? overrides
    ) {
        ArgumentNullException.ThrowIfNull(defaults);

        return new ExportResultOptions {
            UseTimestamp = overrides?.UseTimestamp ?? defaults.UseTimestamp,
            TimestampFormat = defaults.TimestampFormat,
            OverwriteStrategy = defaults.OverwriteStrategy,
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
