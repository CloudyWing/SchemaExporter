using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CloudyWing.SchemaExporter.Core;
using CloudyWing.SchemaExporter.Core.Exporting;

namespace CloudyWing.SchemaExporter.Services;

internal sealed class JsonSettingsService : ISettingsService {
    private static readonly JsonSerializerOptions SerializerOptions = new() {
        PropertyNamingPolicy = null,
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    private readonly string appsettingsPath;
    private readonly string backupPath;

    public JsonSettingsService() {
        appsettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        backupPath = Path.Combine(AppContext.BaseDirectory, "appsettings.backup.json");
    }

    public async Task<SchemaOptions> LoadAsync() {
        JsonObject root = await ReadRootObjectAsync().ConfigureAwait(false);
        JsonNode? schemaNode = root[SchemaOptions.OptionsName];
        if (schemaNode is null) {
            throw new InvalidOperationException("appsettings.json 缺少 Schema 設定區段。");
        }

        SchemaOptions options = schemaNode.Deserialize<SchemaOptions>(SerializerOptions)
            ?? throw new InvalidOperationException("無法讀取 Schema 設定區段。");
        await ValidateAsync(options).ConfigureAwait(false);
        return options;
    }

    public async Task SaveAsync(SchemaOptions options) {
        ArgumentNullException.ThrowIfNull(options, nameof(options));
        await ValidateAsync(options).ConfigureAwait(false);

        JsonObject root = await ReadRootObjectAsync().ConfigureAwait(false);
        root[SchemaOptions.OptionsName] = JsonSerializer.SerializeToNode(options, SerializerOptions)
            ?? throw new InvalidOperationException("無法序列化 Schema 設定。");

        string tempPath = appsettingsPath + ".tmp";
        string json = root.ToJsonString(SerializerOptions) + Environment.NewLine;
        await File.WriteAllTextAsync(tempPath, json, new UTF8Encoding(false)).ConfigureAwait(false);

        if (File.Exists(appsettingsPath)) {
            File.Replace(tempPath, appsettingsPath, backupPath, ignoreMetadataErrors: true);
            return;
        }

        File.Move(tempPath, appsettingsPath);
    }

    public Task<bool> ValidateAsync(SchemaOptions options) {
        ArgumentNullException.ThrowIfNull(options, nameof(options));

        if (string.IsNullOrWhiteSpace(options.ExportPath)) {
            throw new ExportValidationException("Schema.ExportPath 不可為空白。");
        }

        ValidateTimestampFormat(options.ExportResultOptions);
        ValidateConnections(options.Connections);
        ValidateProfiles(options.ExportProfiles);
        ValidateConnectionProfileReferences(options.Connections, options.ExportProfiles);
        return Task.FromResult(true);
    }

    private async Task<JsonObject> ReadRootObjectAsync() {
        if (!File.Exists(appsettingsPath)) {
            throw new FileNotFoundException("找不到 appsettings.json。", appsettingsPath);
        }

        string json = await File.ReadAllTextAsync(appsettingsPath, Encoding.UTF8).ConfigureAwait(false);
        JsonNode? node = JsonNode.Parse(json);
        return node as JsonObject ?? throw new InvalidOperationException("appsettings.json 格式無效，根節點必須為 JSON 物件。");
    }

    private static void ValidateTimestampFormat(ExportResultOptions resultOptions) {
        if (!resultOptions.UseTimestamp) {
            return;
        }

        if (string.IsNullOrWhiteSpace(resultOptions.TimestampFormat)) {
            throw new ExportValidationException("啟用時間戳記時，必須提供 TimestampFormat。");
        }

        try {
            _ = DateTimeOffset.Now.ToString(resultOptions.TimestampFormat);
        } catch (FormatException ex) {
            throw new ExportValidationException($"TimestampFormat 無效：{resultOptions.TimestampFormat}", ex);
        }
    }

    private static void ValidateConnections(IReadOnlyList<SchemaConnection> connections) {
        HashSet<string> connectionNames = new(StringComparer.OrdinalIgnoreCase);
        foreach (SchemaConnection connection in connections) {
            if (string.IsNullOrWhiteSpace(connection.Name)) {
                throw new ExportValidationException("連線名稱不可為空白。");
            }

            if (string.IsNullOrWhiteSpace(connection.ConnectionString)) {
                throw new ExportValidationException($"連線「{connection.Name}」的 ConnectionString 不可為空白。");
            }

            if (!connectionNames.Add(connection.Name.Trim())) {
                throw new ExportValidationException($"連線名稱不可重複：{connection.Name}");
            }
        }
    }

    private static void ValidateProfiles(IReadOnlyList<ExportProfile> profiles) {
        HashSet<string> profileNames = new(StringComparer.OrdinalIgnoreCase);
        foreach (ExportProfile profile in profiles) {
            if (string.IsNullOrWhiteSpace(profile.Name)) {
                throw new ExportValidationException("匯出設定檔名稱不可為空白。");
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

        foreach (SchemaConnection connection in connections) {
            if (string.IsNullOrWhiteSpace(connection.ExportProfileName)) {
                continue;
            }

            if (!profileNames.Contains(connection.ExportProfileName.Trim())) {
                throw new ExportValidationException(
                    $"連線「{connection.Name}」指定的匯出設定檔不存在：{connection.ExportProfileName}"
                );
            }
        }
    }
}
