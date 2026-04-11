using System.IO;
using CloudyWing.SchemaExporter.Core.Exporting;
using CloudyWing.SchemaExporter.Core.SchemaProviders;
using CloudyWing.SchemaExporter.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CloudyWing.SchemaExporter.Tests;

[TestFixture]
[NonParallelizable]
public sealed class JsonSettingsServiceCompatibilityTests {
    [Test]
    public async Task ReloadSettingsAsync_WhenLegacySettingsOmitR4Fields_LoadsSuccessfully() {
        using AppSettingsScope scope = new();
        await File.WriteAllTextAsync(scope.AppSettingsPath, CreateLegacyAppSettingsJson());

        JsonSettingsService settingsService = new();
        ViewModel sut = CreateViewModel(settingsService);

        await sut.ReloadSettingsAsync();

        using (Assert.EnterMultipleScope()) {
            Assert.That(sut.Connection?.Name, Is.EqualTo("Primary"));
            Assert.That(sut.SelectedProfile?.Name, Is.EqualTo("Default"));
            Assert.That(sut.OutputPath, Is.EqualTo(@"C:\Legacy\Exports"));
            Assert.That(sut.GenerateSchemaSnapshot, Is.True);
            Assert.That(sut.DiffSourceSnapshotPath, Is.Null);
        }
    }

    private static ViewModel CreateViewModel(ISettingsService settingsService) {
        SchemaExportOrchestrator exportOrchestrator = new(
            Substitute.For<IDatabaseSchemaProviderFactory>(),
            Substitute.For<ILogger<SchemaExportOrchestrator>>()
        );
        return new ViewModel(settingsService, exportOrchestrator);
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

    private sealed class AppSettingsScope : IDisposable {
        private readonly byte[]? originalAppSettings;
        private readonly byte[]? originalBackupSettings;

        public AppSettingsScope() {
            AppSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            BackupSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.backup.json");
            originalAppSettings = ReadBytesIfExists(AppSettingsPath);
            originalBackupSettings = ReadBytesIfExists(BackupSettingsPath);
        }

        public string AppSettingsPath { get; }

        private string BackupSettingsPath { get; }

        public void Dispose() {
            RestoreFile(AppSettingsPath, originalAppSettings);
            RestoreFile(BackupSettingsPath, originalBackupSettings);
        }

        private static byte[]? ReadBytesIfExists(string path) {
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }

        private static void RestoreFile(string path, byte[]? content) {
            if (content is null) {
                if (File.Exists(path)) {
                    File.Delete(path);
                }

                return;
            }

            File.WriteAllBytes(path, content);
        }
    }
}
