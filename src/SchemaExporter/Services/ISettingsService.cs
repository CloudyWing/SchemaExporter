using CloudyWing.SchemaExporter.Core;

namespace CloudyWing.SchemaExporter.Services;

internal interface ISettingsService {
    Task<SchemaOptions> LoadAsync();

    Task SaveAsync(SchemaOptions options);

    Task<bool> ValidateAsync(SchemaOptions options);
}