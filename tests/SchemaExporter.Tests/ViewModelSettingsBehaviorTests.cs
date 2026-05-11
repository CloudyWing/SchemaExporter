using CloudyWing.SchemaExporter.Core;
using CloudyWing.SchemaExporter.Core.Exporting;
using CloudyWing.SchemaExporter.Core.Exporting.Snapshots;
using CloudyWing.SchemaExporter.Core.SchemaProviders;
using CloudyWing.SchemaExporter.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CloudyWing.SchemaExporter.Tests;

[TestFixture]
public sealed class ViewModelSettingsBehaviorTests {
    [Test]
    public async Task SaveSettingsCommand_WhenExecuted_PersistsCurrentUiState() {
        SchemaOptions loadedOptions = CreateSchemaOptions(
            exportPath: @"C:\Existing",
            lastSelectedConnectionName: "Primary",
            lastSelectedProfileName: "Default",
            exportResultOptions: new ExportResultOptions {
                UseTimestamp = false,
                TimestampFormat = "yyyy-MM",
                OverwriteStrategy = OverwriteStrategy.AppendSuffix,
                OpenOutputFolder = false,
                GenerateManifest = false,
                GenerateJsonSidecar = false,
                GenerateMarkdownSidecar = true,
                GenerateSchemaSummary = false,
                GenerateSchemaSnapshot = false,
                DiffSourceSnapshotPath = @"C:\baseline.snapshot.json"
            }
        );
        ISettingsService settingsService = Substitute.For<ISettingsService>();
        settingsService.LoadAsync().Returns(Task.FromResult(loadedOptions));

        SchemaOptions? savedOptions = null;
        settingsService.SaveAsync(Arg.Do<SchemaOptions>(options => savedOptions = options))
            .Returns(Task.CompletedTask);

        ViewModel sut = CreateViewModel(settingsService);
        await sut.ReloadSettingsAsync();

        sut.OutputPath = "  C:\\Exports\\Today  ";
        sut.GenerateManifest = true;
        sut.GenerateJsonSidecar = true;
        sut.GenerateMarkdownSidecar = false;
        sut.GenerateSchemaSummary = true;
        sut.GenerateSchemaSnapshot = true;
        sut.UseTimestamp = true;
        sut.AutoOpenOutputFolder = true;
        sut.DiffSourceSnapshotPath = @"C:\ignore.snapshot.json";
        sut.Connection = sut.Connections.Single(x => x.Name == "Analytics");
        sut.SelectedProfile = sut.ExportProfiles.Single(x => x.Name == "Compact");

        await sut.SaveSettingsCommand.ExecuteAsync(null);

        await settingsService.Received(1).SaveAsync(Arg.Any<SchemaOptions>());

        Assert.That(savedOptions, Is.Not.Null);

        using (Assert.EnterMultipleScope()) {
            Assert.That(savedOptions!.ExportPath, Is.EqualTo(@"C:\Exports\Today"));
            Assert.That(savedOptions.ExportResultOptions.GenerateManifest, Is.True);
            Assert.That(savedOptions.ExportResultOptions.GenerateJsonSidecar, Is.True);
            Assert.That(savedOptions.ExportResultOptions.GenerateMarkdownSidecar, Is.False);
            Assert.That(savedOptions.ExportResultOptions.GenerateSchemaSummary, Is.True);
            Assert.That(savedOptions.ExportResultOptions.GenerateSchemaSnapshot, Is.True);
            Assert.That(savedOptions.ExportResultOptions.UseTimestamp, Is.True);
            Assert.That(savedOptions.ExportResultOptions.OpenOutputFolder, Is.True);
            Assert.That(savedOptions.LastSelectedConnectionName, Is.EqualTo("Analytics"));
            Assert.That(savedOptions.LastSelectedProfileName, Is.EqualTo("Compact"));
            Assert.That(savedOptions.ExportResultOptions.TimestampFormat, Is.EqualTo("yyyy-MM"));
            Assert.That(savedOptions.ExportResultOptions.OverwriteStrategy, Is.EqualTo(OverwriteStrategy.AppendSuffix));
            Assert.That(savedOptions.ExportResultOptions.DiffSourceSnapshotPath, Is.Null);
            Assert.That(sut.StatusMessage, Is.EqualTo("設定已儲存。"));
        }
    }

    [Test]
    public async Task ReloadSettingsAsync_WhenLastSelectedNamesExist_RestoresMatchingSelections() {
        SchemaOptions previousOptions = CreateSchemaOptions(
            exportPath: @"C:\Exports",
            lastSelectedConnectionName: "Primary",
            lastSelectedProfileName: "Full",
            exportResultOptions: new ExportResultOptions(),
            connections: [
                CreateConnection("Primary", "Default"),
                CreateConnection("Analytics", "Full")
            ],
            exportProfiles: [
                CreateProfile("Default"),
                CreateProfile("Full"),
                CreateProfile("Compact")
            ]
        );
        SchemaOptions loadedOptions = CreateSchemaOptions(
            exportPath: @"C:\Exports",
            lastSelectedConnectionName: "Analytics",
            lastSelectedProfileName: "Compact",
            exportResultOptions: new ExportResultOptions(),
            connections: [
                CreateConnection("Primary", "Default"),
                CreateConnection("Analytics", "Full")
            ],
            exportProfiles: [
                CreateProfile("Default"),
                CreateProfile("Full"),
                CreateProfile("Compact")
            ]
        );
        ISettingsService settingsService = Substitute.For<ISettingsService>();
        settingsService.LoadAsync().Returns(Task.FromResult(previousOptions), Task.FromResult(loadedOptions));

        ViewModel sut = CreateViewModel(settingsService);
        await sut.ReloadSettingsAsync();

        await sut.ReloadSettingsAsync();

        using (Assert.EnterMultipleScope()) {
            Assert.That(sut.Connection?.Name, Is.EqualTo("Analytics"));
            Assert.That(sut.SelectedProfile?.Name, Is.EqualTo("Compact"));
        }
    }

    [Test]
    public async Task ReloadSettingsAsync_DiffSourceSnapshotPathInSettings_IsClearedOnLoad() {
        SchemaOptions loadedOptions = CreateSchemaOptions(
            exportPath: @"C:\Exports",
            lastSelectedConnectionName: null,
            lastSelectedProfileName: null,
            exportResultOptions: new ExportResultOptions {
                DiffSourceSnapshotPath = @"C:\baseline.snapshot.json"
            }
        );
        ISettingsService settingsService = Substitute.For<ISettingsService>();
        settingsService.LoadAsync().Returns(Task.FromResult(loadedOptions));

        ViewModel sut = CreateViewModel(settingsService);

        await sut.ReloadSettingsAsync();

        Assert.That(sut.DiffSourceSnapshotPath, Is.Null);
    }

    [Test]
    public void SaveSettingsCommand_WhenOutputPathIsWhitespace_CannotExecute() {
        ViewModel sut = CreateViewModel(Substitute.For<ISettingsService>());
        sut.OutputPath = "   ";
        sut.IsExporting = false;

        Assert.That(sut.SaveSettingsCommand.CanExecute(null), Is.False);
    }

    [Test]
    public void SaveSettingsCommand_WhenExportIsInProgress_CannotExecute() {
        ViewModel sut = CreateViewModel(Substitute.For<ISettingsService>());
        sut.OutputPath = @"C:\Exports";
        sut.IsExporting = true;

        Assert.That(sut.SaveSettingsCommand.CanExecute(null), Is.False);
    }

    private static ViewModel CreateViewModel(ISettingsService settingsService) {
        SchemaExportOrchestrator exportOrchestrator = new(
            Substitute.For<IDatabaseSchemaProviderFactory>(),
            Substitute.For<ILogger<SchemaExportOrchestrator>>(),
            new SchemaSnapshotBuilder(),
            new SchemaSnapshotDiffService()
        );
        return new ViewModel(settingsService, exportOrchestrator, new SchemaExportRequestResolver());
    }

    private static SchemaOptions CreateSchemaOptions(
        string exportPath,
        string? lastSelectedConnectionName,
        string? lastSelectedProfileName,
        ExportResultOptions exportResultOptions,
        IReadOnlyList<SchemaConnection>? connections = null,
        IReadOnlyList<ExportProfile>? exportProfiles = null
    ) {
        IReadOnlyList<SchemaConnection> resolvedConnections = connections ?? [
            CreateConnection("Primary", "Default"),
            CreateConnection("Analytics", "Compact")
        ];
        IReadOnlyList<ExportProfile> resolvedProfiles = exportProfiles ?? [
            CreateProfile("Default"),
            CreateProfile("Compact")
        ];

        return new SchemaOptions {
            ExportPath = exportPath,
            Connections = resolvedConnections,
            LastSelectedConnectionName = lastSelectedConnectionName,
            ExportProfiles = resolvedProfiles,
            LastSelectedProfileName = lastSelectedProfileName,
            ExportResultOptions = exportResultOptions
        };
    }

    private static SchemaConnection CreateConnection(string name, string? exportProfileName) {
        return new SchemaConnection {
            Name = name,
            ConnectionString = $"Server={name};Database=SchemaExporter;",
            ExportProfileName = exportProfileName
        };
    }

    private static ExportProfile CreateProfile(string name) {
        return new ExportProfile {
            Name = name
        };
    }
}
