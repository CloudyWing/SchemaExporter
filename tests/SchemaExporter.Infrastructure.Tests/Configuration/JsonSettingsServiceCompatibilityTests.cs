using CloudyWing.SchemaExporter.Core;
using CloudyWing.SchemaExporter.Core.Exporting;
using CloudyWing.SchemaExporter.Infrastructure.Configuration;

namespace CloudyWing.SchemaExporter.Infrastructure.Tests.Configuration;

[TestFixture]
[NonParallelizable]
public sealed class JsonSettingsServiceCompatibilityTests {
    [Test]
    public async Task LoadAsync_WhenLegacySettingsOmitR4Fields_LoadsSuccessfully() {
        using AppSettingsTestScope scope = new();
        Directory.CreateDirectory(scope.UserConfigDirectory);
        await File.WriteAllTextAsync(scope.UserConfigPath, CreateLegacyAppSettingsJson());
        JsonSettingsService sut = new();

        SchemaOptions result = await sut.LoadAsync();

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.Connections.Select(x => x.Name), Is.EqualTo(new[] { "Primary" }));
            Assert.That(result.ExportProfiles.Select(x => x.Name), Is.EqualTo(new[] { "Default" }));
            Assert.That(result.ExportPath, Is.EqualTo(@"C:\Legacy\Exports"));
            Assert.That(result.ExportResultOptions.GenerateSchemaSummary, Is.False);
            Assert.That(result.ExportResultOptions.GenerateSchemaSnapshot, Is.True);
        }
    }

    [Test]
    public async Task ReadAsync_WhenSettingsFailValidation_ReturnsOptionsWhileLoadAsyncThrows() {
        using AppSettingsTestScope scope = new();
        Directory.CreateDirectory(scope.UserConfigDirectory);
        await File.WriteAllTextAsync(scope.UserConfigPath, """
            {
              "Schema": {
                "ExportPath": "C:\\Exports",
                "Connections": [
                  { "Name": "Dup", "DatabaseType": "SqlServer", "ConnectionString": "Server=a;" },
                  { "Name": "dup", "DatabaseType": "SqlServer", "ConnectionString": "Server=b;" }
                ]
              }
            }
            """);
        JsonSettingsService sut = new();

        SchemaOptions result = await sut.ReadAsync();

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.Connections, Has.Count.EqualTo(2));
            Assert.ThrowsAsync<ExportValidationException>(async () => await sut.LoadAsync());
        }
    }

    [Test]
    public async Task SaveAsync_WhenUserConfigExists_WritesToLocalAppDataWithoutChangingInstallTemplate() {
        using AppSettingsTestScope scope = new();
        string installTemplateJson = CreateLegacyAppSettingsJson();
        await File.WriteAllTextAsync(scope.InstallConfigPath, installTemplateJson);
        Directory.CreateDirectory(scope.UserConfigDirectory);
        await File.WriteAllTextAsync(scope.UserConfigPath, CreateLegacyAppSettingsJson());

        JsonSettingsService settingsService = new();
        SchemaOptions options = await settingsService.LoadAsync();
        options.ExportPath = @"C:\User\Exports";

        await settingsService.SaveAsync(options);

        SchemaOptions reloadedOptions = await settingsService.LoadAsync();
        string installTemplateAfterSave = await File.ReadAllTextAsync(scope.InstallConfigPath);
        using (Assert.EnterMultipleScope()) {
            Assert.That(reloadedOptions.ExportPath, Is.EqualTo(@"C:\User\Exports"));
            Assert.That(File.Exists(scope.UserConfigPath), Is.True);
            Assert.That(installTemplateAfterSave, Is.EqualTo(installTemplateJson));
        }
    }

    [Test]
    public async Task LoadAsync_WhenLegacyGenerateAiContextIsTrue_MapsToSchemaSummary() {
        using AppSettingsTestScope scope = new();
        Directory.CreateDirectory(scope.UserConfigDirectory);
        await File.WriteAllTextAsync(scope.UserConfigPath, CreateLegacyAiContextAppSettingsJson());

        JsonSettingsService settingsService = new();

        SchemaOptions options = await settingsService.LoadAsync();

        Assert.That(options.ExportResultOptions.GenerateSchemaSummary, Is.True);
    }

    [Test]
    public async Task LoadAsync_WhenLegacySettingsOmitRedaction_UsesRedactionDefaults() {
        using AppSettingsTestScope scope = new();
        Directory.CreateDirectory(scope.UserConfigDirectory);
        await File.WriteAllTextAsync(scope.UserConfigPath, CreateLegacyAppSettingsJson());

        JsonSettingsService settingsService = new();

        SchemaOptions options = await settingsService.LoadAsync();

        using (Assert.EnterMultipleScope()) {
            Assert.That(options.Redaction.Enabled, Is.False);
            Assert.That(options.Redaction.ReplacementText, Is.EqualTo("[REDACTED]"));
            Assert.That(options.Redaction.SensitiveNamePatterns, Contains.Item("password"));
            Assert.That(options.Redaction.SensitiveTextPatterns, Is.Empty);
        }
    }

    private static string CreateLegacyAppSettingsJson() {
        return """
            {
              "Schema": {
                "ExportPath": "C:\\Legacy\\Exports",
                "Connections": [
                  {
                    "Name": "Primary",
                    "DatabaseType": "SqlServer",
                    "ConnectionString": "Server=Primary;Database=SchemaExporter;",
                    "ExportProfileName": "Default"
                  }
                ],
                "ExportProfiles": [
                  {
                    "Name": "Default",
                    "IncludeSchemas": [],
                    "ExcludeSchemas": [],
                    "IncludeObjects": [],
                    "ExcludeObjects": [],
                    "IncludeViews": true
                  }
                ],
                "ExportResultOptions": {
                  "UseTimestamp": true,
                  "TimestampFormat": "yyyyMMdd",
                  "OverwriteStrategy": "Overwrite",
                  "OpenOutputFolder": false,
                  "GenerateManifest": true,
                  "GenerateJsonSidecar": false,
                  "GenerateMarkdownSidecar": true,
                  "GenerateSchemaSnapshot": true,
                  "DiffSourceSnapshotPath": null
                }
              }
            }
            """;
    }

    private static string CreateLegacyAiContextAppSettingsJson() {
        return """
            {
              "Schema": {
                "ExportPath": "C:\\Legacy\\Exports",
                "Connections": [
                  {
                    "Name": "Primary",
                    "DatabaseType": "SqlServer",
                    "ConnectionString": "Server=Primary;Database=SchemaExporter;",
                    "ExportProfileName": "Default"
                  }
                ],
                "ExportProfiles": [
                  {
                    "Name": "Default",
                    "IncludeSchemas": [],
                    "ExcludeSchemas": [],
                    "IncludeObjects": [],
                    "ExcludeObjects": [],
                    "IncludeViews": true
                  }
                ],
                "ExportResultOptions": {
                  "UseTimestamp": true,
                  "TimestampFormat": "yyyyMMdd",
                  "OverwriteStrategy": "Overwrite",
                  "OpenOutputFolder": false,
                  "GenerateManifest": true,
                  "GenerateJsonSidecar": false,
                  "GenerateMarkdownSidecar": true,
                  "GenerateAiContext": true,
                  "GenerateSchemaSnapshot": true,
                  "DiffSourceSnapshotPath": null
                }
              }
            }
            """;
    }

}
