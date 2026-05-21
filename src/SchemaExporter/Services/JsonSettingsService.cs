using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CloudyWing.SchemaExporter.Core;
using CloudyWing.SchemaExporter.Core.Exporting;

namespace CloudyWing.SchemaExporter.Services;

/// <summary>
/// 以 JSON 格式（appsettings.json）實作 <see cref="ISettingsService"/> 的設定存取服務。
/// </summary>
internal sealed class JsonSettingsService : ISettingsService {
    private static readonly JsonSerializerOptions SerializerOptions = new() {
        PropertyNamingPolicy = null,
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    private readonly string appsettingsPath;
    private readonly string backupPath;

    /// <summary>
    /// 初始化 <see cref="JsonSettingsService"/> 類別的新執行個體，並設定 appsettings.json 與備份檔案路徑。
    /// </summary>
    public JsonSettingsService() {
        appsettingsPath = AppPaths.UserConfigFile;
        backupPath = Path.Combine(AppPaths.UserConfigDirectory, "appsettings.backup.json");
    }

    /// <inheritdoc/>
    public async Task<SchemaOptions> LoadAsync() {
        JsonObject root = await ReadRootObjectAsync().ConfigureAwait(false);
        JsonNode? schemaNode = root[SchemaOptions.OptionsName]
            ?? throw new InvalidOperationException("appsettings.json 缺少 Schema 設定區段。");

        SchemaOptions options = schemaNode.Deserialize<SchemaOptions>(SerializerOptions)
            ?? throw new InvalidOperationException("無法讀取 Schema 設定區段。");
        ApplyCompatibilityDefaults(options, schemaNode);
        await ValidateAsync(options).ConfigureAwait(false);
        return options;
    }

    /// <inheritdoc/>
    public async Task SaveAsync(SchemaOptions options) {
        ArgumentNullException.ThrowIfNull(options);
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

    /// <inheritdoc/>
    public Task<bool> ValidateAsync(SchemaOptions options) {
        SchemaOptionsValidator.Validate(options);
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

    private static void ApplyCompatibilityDefaults(SchemaOptions options, JsonNode schemaNode) {
        options.Redaction ??= new SchemaRedactionOptions();

        if (schemaNode["ExportResultOptions"] is not JsonObject exportResultOptionsNode) {
            return;
        }

        if (exportResultOptionsNode.ContainsKey(nameof(ExportResultOptions.GenerateSchemaSummary))) {
            return;
        }

        if (TryGetBoolean(exportResultOptionsNode, "GenerateAiContext", out bool generateSchemaSummary)) {
            options.ExportResultOptions.GenerateSchemaSummary = generateSchemaSummary;
        }
    }

    private static bool TryGetBoolean(JsonObject jsonObject, string propertyName, out bool value) {
        value = false;
        if (!jsonObject.TryGetPropertyValue(propertyName, out JsonNode? node)) {
            return false;
        }

        if (node is not JsonValue jsonValue) {
            return false;
        }

        return jsonValue.TryGetValue(out value);
    }
}
