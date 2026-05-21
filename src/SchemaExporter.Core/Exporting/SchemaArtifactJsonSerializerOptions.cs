using System.Text.Json;
using System.Text.Json.Serialization;

namespace CloudyWing.SchemaExporter.Core.Exporting;

internal static class SchemaArtifactJsonSerializerOptions {
    internal static readonly JsonSerializerOptions Default = new() {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = {
            new JsonStringEnumConverter()
        }
    };
}
