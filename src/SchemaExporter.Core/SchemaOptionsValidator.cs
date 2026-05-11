using CloudyWing.SchemaExporter.Core.Exporting;

namespace CloudyWing.SchemaExporter.Core;

/// <summary>
/// 驗證 schema 匯出設定內容是否可供 WPF、CLI 與 Core 共用流程使用。
/// </summary>
public static class SchemaOptionsValidator {
    /// <summary>
    /// 驗證指定的 schema 匯出設定。
    /// </summary>
    /// <param name="options">要驗證的 schema 匯出設定。</param>
    public static void Validate(SchemaOptions options) {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.ExportPath)) {
            throw new ExportValidationException("Schema.ExportPath 不可為空白。");
        }

        ValidateTimestampFormat(options.ExportResultOptions);
        ValidateDiffSourceSnapshotPath(options.ExportResultOptions);
        ValidateConnections(options.Connections);
        ValidateProfiles(options.ExportProfiles);
        ValidateConnectionProfileReferences(options.Connections, options.ExportProfiles);
    }

    private static void ValidateTimestampFormat(ExportResultOptions resultOptions) {
        ArgumentNullException.ThrowIfNull(resultOptions);

        if (!resultOptions.UseTimestamp) {
            return;
        }

        if (string.IsNullOrWhiteSpace(resultOptions.TimestampFormat)) {
            throw new ExportValidationException("Schema.ExportResultOptions.TimestampFormat 不可為空白。");
        }

        try {
            _ = DateTimeOffset.Now.ToString(resultOptions.TimestampFormat);
        } catch (FormatException ex) {
            throw new ExportValidationException($"Schema.ExportResultOptions.TimestampFormat 無效：{resultOptions.TimestampFormat}", ex);
        }
    }

    private static void ValidateDiffSourceSnapshotPath(ExportResultOptions resultOptions) {
        if (string.IsNullOrWhiteSpace(resultOptions.DiffSourceSnapshotPath)) {
            return;
        }

        string trimmedPath = resultOptions.DiffSourceSnapshotPath.Trim();
        if (!Path.IsPathFullyQualified(trimmedPath)) {
            throw new ExportValidationException($"Schema.ExportResultOptions.DiffSourceSnapshotPath 必須使用絕對路徑：{trimmedPath}");
        }
    }

    private static void ValidateConnections(IReadOnlyList<SchemaConnection> connections) {
        HashSet<string> connectionNames = new(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < connections.Count; index++) {
            SchemaConnection connection = connections[index];
            string path = $"Schema.Connections[{index}]";

            if (string.IsNullOrWhiteSpace(connection.Name)) {
                throw new ExportValidationException($"{path}.Name 不可為空白。");
            }

            if (string.IsNullOrWhiteSpace(connection.ConnectionString)) {
                throw new ExportValidationException($"{path}.ConnectionString 不可為空白。");
            }

            if (!connectionNames.Add(connection.Name.Trim())) {
                throw new ExportValidationException($"連線名稱不可重複：{connection.Name}");
            }
        }
    }

    private static void ValidateProfiles(IReadOnlyList<ExportProfile> profiles) {
        HashSet<string> profileNames = new(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < profiles.Count; index++) {
            ExportProfile profile = profiles[index];
            string path = $"Schema.ExportProfiles[{index}]";

            if (string.IsNullOrWhiteSpace(profile.Name)) {
                throw new ExportValidationException($"{path}.Name 不可為空白。");
            }

            if (!profileNames.Add(profile.Name.Trim())) {
                throw new ExportValidationException($"匯出設定檔名稱不可重複：{profile.Name}");
            }
        }
    }

    private static void ValidateConnectionProfileReferences(
        IReadOnlyList<SchemaConnection> connections,
        IReadOnlyList<ExportProfile> profiles
    ) {
        HashSet<string> profileNames = profiles
            .Select(x => x.Name.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < connections.Count; index++) {
            SchemaConnection connection = connections[index];
            if (string.IsNullOrWhiteSpace(connection.ExportProfileName)) {
                continue;
            }

            if (!profileNames.Contains(connection.ExportProfileName.Trim())) {
                throw new ExportValidationException(
                    $"Schema.Connections[{index}].ExportProfileName 指定的匯出設定檔不存在：{connection.ExportProfileName}"
                );
            }
        }
    }
}
