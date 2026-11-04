using CloudyWing.SchemaExporter.Core;
using CloudyWing.SchemaExporter.Core.Exporting;
using CloudyWing.SchemaExporter.Core.Exporting.Snapshots;
using CloudyWing.SchemaExporter.Core.SchemaProviders;
using CloudyWing.SchemaExporter.Desktop.Services;
using CloudyWing.SchemaExporter.Desktop.State;
using CloudyWing.SchemaExporter.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CloudyWing.SchemaExporter.Desktop.Tests.State;

[TestFixture]
public sealed class ExportWorkspaceTests {
    [OneTimeSetUp]
    public void OneTimeSetUp() {
        SpreadsheetExporterBootstrapper.Configure();
    }

    [Test]
    public async Task SaveDefaultsAsync_WhenExecuted_PersistsCurrentUiStateAndKeepsOtherSections() {
        SchemaOptions loadedOptions = CreateSchemaOptions(
            exportPath: @"C:\Existing",
            lastSelectedConnectionName: "Primary",
            lastSelectedProfileName: "Default",
            exportResultOptions: new ExportResultOptions {
                UseTimestamp = false,
                TimestampFormat = "yyyy-MM",
                OverwriteStrategy = OverwriteStrategy.AppendSuffix,
                GenerateMarkdownSidecar = true,
                DiffSourceSnapshotPath = @"C:\baseline.snapshot.json"
            }
        );
        loadedOptions.Redaction = new SchemaRedactionOptions {
            Enabled = true,
            ReplacementText = "[MASKED]"
        };
        ISettingsService settingsService = Substitute.For<ISettingsService>();
        settingsService.LoadAsync().Returns(Task.FromResult(loadedOptions));
        SchemaOptions? savedOptions = null;
        settingsService.SaveAsync(Arg.Do<SchemaOptions>(options => savedOptions = options)).Returns(Task.CompletedTask);
        ExportWorkspace sut = CreateWorkspace(settingsService);
        await sut.ReloadSettingsAsync();

        sut.OutputPath = "  C:\\Exports\\Today  ";
        sut.GenerateManifest = true;
        sut.GenerateJsonSidecar = true;
        sut.GenerateMarkdownSidecar = false;
        sut.GenerateSchemaSummary = true;
        sut.GenerateSchemaSnapshot = true;
        sut.UseTimestamp = true;
        sut.TimestampFormat = "yyyyMMdd";
        sut.OverwriteStrategy = OverwriteStrategy.Fail;
        sut.OpenOutputFolderAfterExport = true;
        sut.DiffSourceSnapshotPath = @"C:\ignore.snapshot.json";
        sut.SelectConnection("Analytics");
        sut.SelectProfile("Compact");

        bool result = await sut.SaveDefaultsAsync();

        Assert.That(savedOptions, Is.Not.Null);
        using (Assert.EnterMultipleScope()) {
            Assert.That(result, Is.True);
            Assert.That(savedOptions!.ExportPath, Is.EqualTo(@"C:\Exports\Today"));
            Assert.That(savedOptions.ExportResultOptions.GenerateManifest, Is.True);
            Assert.That(savedOptions.ExportResultOptions.GenerateJsonSidecar, Is.True);
            Assert.That(savedOptions.ExportResultOptions.GenerateMarkdownSidecar, Is.False);
            Assert.That(savedOptions.ExportResultOptions.GenerateSchemaSummary, Is.True);
            Assert.That(savedOptions.ExportResultOptions.GenerateSchemaSnapshot, Is.True);
            Assert.That(savedOptions.ExportResultOptions.UseTimestamp, Is.True);
            Assert.That(savedOptions.ExportResultOptions.TimestampFormat, Is.EqualTo("yyyyMMdd"));
            Assert.That(savedOptions.ExportResultOptions.OverwriteStrategy, Is.EqualTo(OverwriteStrategy.Fail));
            Assert.That(savedOptions.ExportResultOptions.OpenOutputFolder, Is.True);
            Assert.That(savedOptions.ExportResultOptions.DiffSourceSnapshotPath, Is.EqualTo(@"C:\baseline.snapshot.json"));
            Assert.That(savedOptions.LastSelectedConnectionName, Is.EqualTo("Analytics"));
            Assert.That(savedOptions.LastSelectedProfileName, Is.EqualTo("Compact"));
            Assert.That(savedOptions.Redaction.Enabled, Is.True);
            Assert.That(savedOptions.Redaction.ReplacementText, Is.EqualTo("[MASKED]"));
            Assert.That(sut.StatusKind, Is.EqualTo(StatusKind.Success));
        }
    }

    [Test]
    public async Task SaveDefaultsAsync_WhenSettingsChangedAfterLoad_KeepsLatestConnections() {
        SchemaOptions initialOptions = CreateSchemaOptions(@"C:\Exports", null, null, new ExportResultOptions());
        SchemaOptions latestOptions = CreateSchemaOptions(
            @"C:\Exports",
            null,
            null,
            new ExportResultOptions(),
            connections: [CreateConnection("Primary", null), CreateConnection("Added Later", null)]
        );
        ISettingsService settingsService = Substitute.For<ISettingsService>();
        settingsService.LoadAsync().Returns(Task.FromResult(initialOptions), Task.FromResult(latestOptions));
        SchemaOptions? savedOptions = null;
        settingsService.SaveAsync(Arg.Do<SchemaOptions>(options => savedOptions = options)).Returns(Task.CompletedTask);
        ExportWorkspace sut = CreateWorkspace(settingsService);
        await sut.ReloadSettingsAsync();

        await sut.SaveDefaultsAsync();

        Assert.That(savedOptions?.Connections.Select(x => x.Name), Is.EqualTo(new[] { "Primary", "Added Later" }));
    }

    [Test]
    public async Task SaveDefaultsAsync_WhenOutputPathIsWhitespace_DoesNotSaveAndWarns() {
        ISettingsService settingsService = Substitute.For<ISettingsService>();
        settingsService.LoadAsync().Returns(Task.FromResult(CreateSchemaOptions(@"C:\Exports", null, null, new ExportResultOptions())));
        IDialogService dialogService = Substitute.For<IDialogService>();
        ExportWorkspace sut = CreateWorkspace(settingsService, dialogService: dialogService);
        await sut.ReloadSettingsAsync();
        sut.OutputPath = "   ";

        bool result = await sut.SaveDefaultsAsync();

        using (Assert.EnterMultipleScope()) {
            Assert.That(result, Is.False);
            await settingsService.DidNotReceive().SaveAsync(Arg.Any<SchemaOptions>());
            dialogService.Received(1).ShowWarning(Arg.Any<string>(), Arg.Any<string>());
        }
    }

    [Test]
    public async Task ReloadSettingsAsync_WhenLastSelectedNamesExist_RestoresMatchingSelections() {
        SchemaOptions loadedOptions = CreateSchemaOptions(
            exportPath: @"C:\Exports",
            lastSelectedConnectionName: "Analytics",
            lastSelectedProfileName: "Compact",
            exportResultOptions: new ExportResultOptions(),
            exportProfiles: [CreateProfile("Default"), CreateProfile("Full"), CreateProfile("Compact")]
        );
        ISettingsService settingsService = Substitute.For<ISettingsService>();
        settingsService.LoadAsync().Returns(Task.FromResult(loadedOptions));
        ExportWorkspace sut = CreateWorkspace(settingsService);

        await sut.ReloadSettingsAsync();

        using (Assert.EnterMultipleScope()) {
            Assert.That(sut.SelectedConnection?.Name, Is.EqualTo("Analytics"));
            Assert.That(sut.SelectedProfile?.Name, Is.EqualTo("Compact"));
        }
    }

    [Test]
    public async Task ReloadSettingsAsync_DiffSourceSnapshotPathInSettings_IsClearedOnLoad() {
        SchemaOptions loadedOptions = CreateSchemaOptions(
            @"C:\Exports",
            null,
            null,
            new ExportResultOptions { DiffSourceSnapshotPath = @"C:\baseline.snapshot.json" }
        );
        ISettingsService settingsService = Substitute.For<ISettingsService>();
        settingsService.LoadAsync().Returns(Task.FromResult(loadedOptions));
        ExportWorkspace sut = CreateWorkspace(settingsService);

        await sut.ReloadSettingsAsync();

        Assert.That(sut.DiffSourceSnapshotPath, Is.Null);
    }

    [Test]
    public async Task ReloadSettingsAsync_WhenSettingsCannotBeLoaded_ExposesLoadErrorAndBlocksExport() {
        ISettingsService settingsService = Substitute.For<ISettingsService>();
        settingsService.LoadAsync().Returns(Task.FromException<SchemaOptions>(new InvalidOperationException("appsettings.json 格式無效")));
        ExportWorkspace sut = CreateWorkspace(settingsService);

        await sut.ReloadSettingsAsync();

        using (Assert.EnterMultipleScope()) {
            Assert.That(sut.LoadError, Does.Contain("appsettings.json 格式無效"));
            Assert.That(sut.StatusKind, Is.EqualTo(StatusKind.Error));
            Assert.That(sut.CanExport, Is.False);
        }
    }

    [Test]
    public async Task SelectConnection_WhenConnectionProfileIsMissing_FallsBackToFirstProfileWithWarning() {
        SchemaOptions loadedOptions = CreateSchemaOptions(
            @"C:\Exports",
            null,
            null,
            new ExportResultOptions(),
            connections: [CreateConnection("Primary", "Default"), CreateConnection("Broken", "Removed")]
        );
        ISettingsService settingsService = Substitute.For<ISettingsService>();
        settingsService.LoadAsync().Returns(Task.FromResult(loadedOptions));
        ExportWorkspace sut = CreateWorkspace(settingsService);
        await sut.ReloadSettingsAsync();

        sut.SelectConnection("Broken");

        using (Assert.EnterMultipleScope()) {
            Assert.That(sut.SelectedProfile?.Name, Is.EqualTo("Default"));
            Assert.That(sut.StatusKind, Is.EqualTo(StatusKind.Warning));
            Assert.That(sut.StatusMessage, Does.Contain("Removed"));
        }
    }

    [Test]
    public async Task ExportAsync_WhenExportSucceeds_ExposesResultAndRequestedOverrides() {
        using TemporaryDirectory directory = new();
        SchemaConnection connection = CreateConnection("Primary", null);
        SchemaOptions loadedOptions = CreateSchemaOptions(directory.Path, null, null, new ExportResultOptions(), connections: [connection]);
        ISettingsService settingsService = Substitute.For<ISettingsService>();
        settingsService.LoadAsync().Returns(Task.FromResult(loadedOptions));
        IDatabaseSchemaProviderFactory providerFactory = CreateProviderFactory(connection);
        IOutputFolderLauncher launcher = Substitute.For<IOutputFolderLauncher>();
        ExportWorkspace sut = CreateWorkspace(settingsService, providerFactory, launcher: launcher);
        await sut.ReloadSettingsAsync();
        sut.GenerateSchemaSummary = true;
        sut.UseTimestamp = false;
        sut.OpenOutputFolderAfterExport = true;

        await sut.ExportAsync();

        using (Assert.EnterMultipleScope()) {
            Assert.That(sut.LastResult, Is.Not.Null);
            Assert.That(File.Exists(sut.LastResult!.OutputFilePath), Is.True);
            Assert.That(sut.LastResult.SchemaSummaryFilePath, Is.Not.Null);
            Assert.That(sut.ProgressPercent, Is.EqualTo(100));
            Assert.That(sut.IsExporting, Is.False);
            Assert.That(sut.StatusMessage, Does.StartWith("匯出完成"));
            launcher.Received(1).Open(sut.LastResult.OutputDirectoryPath);
        }
    }

    [Test]
    public async Task ExportAsync_WhenConnectionFails_ShowsRedactedErrorDialog() {
        using TemporaryDirectory directory = new();
        SchemaConnection connection = new() {
            Name = "Primary",
            ConnectionString = "Server=db;Database=App;Password=Secret123;"
        };
        SchemaOptions loadedOptions = CreateSchemaOptions(directory.Path, null, null, new ExportResultOptions(), connections: [connection]);
        ISettingsService settingsService = Substitute.For<ISettingsService>();
        settingsService.LoadAsync().Returns(Task.FromResult(loadedOptions));
        IDatabaseSchemaProviderFactory providerFactory = Substitute.For<IDatabaseSchemaProviderFactory>();
        providerFactory.LoadObjectsAsync(connection.DatabaseType, connection.ConnectionString, Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<DatabaseObjectSchema>>>(_ => throw new TimeoutException($"timeout for {connection.ConnectionString}"));
        IDialogService dialogService = Substitute.For<IDialogService>();
        string? shownMessage = null;
        dialogService.ShowError(Arg.Any<string>(), Arg.Do<string>(message => shownMessage = message));
        ExportWorkspace sut = CreateWorkspace(settingsService, providerFactory, dialogService);
        await sut.ReloadSettingsAsync();

        await sut.ExportAsync();

        using (Assert.EnterMultipleScope()) {
            Assert.That(shownMessage, Is.Not.Null);
            Assert.That(shownMessage, Does.Not.Contain("Secret123"));
            Assert.That(sut.StatusKind, Is.EqualTo(StatusKind.Error));
            Assert.That(sut.IsExporting, Is.False);
            Assert.That(sut.LastResult, Is.Null);
        }
    }

    [Test]
    public async Task CancelExport_WhileProviderIsLoading_EndsWithCanceledStatus() {
        using TemporaryDirectory directory = new();
        SchemaConnection connection = CreateConnection("Primary", null);
        SchemaOptions loadedOptions = CreateSchemaOptions(directory.Path, null, null, new ExportResultOptions(), connections: [connection]);
        ISettingsService settingsService = Substitute.For<ISettingsService>();
        settingsService.LoadAsync().Returns(Task.FromResult(loadedOptions));
        IDatabaseSchemaProviderFactory providerFactory = Substitute.For<IDatabaseSchemaProviderFactory>();
        TaskCompletionSource loadingStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        providerFactory.LoadObjectsAsync(connection.DatabaseType, connection.ConnectionString, Arg.Any<CancellationToken>())
            .Returns(async callInfo => {
                loadingStarted.SetResult();
                await Task.Delay(Timeout.Infinite, callInfo.Arg<CancellationToken>());
                return (IReadOnlyList<DatabaseObjectSchema>)[];
            });
        ExportWorkspace sut = CreateWorkspace(settingsService, providerFactory);
        await sut.ReloadSettingsAsync();

        Task exportTask = sut.ExportAsync();
        await loadingStarted.Task;
        sut.CancelExport();
        await exportTask;

        using (Assert.EnterMultipleScope()) {
            Assert.That(sut.IsExporting, Is.False);
            Assert.That(sut.StatusKind, Is.EqualTo(StatusKind.Warning));
            Assert.That(sut.StatusMessage, Does.StartWith("匯出已取消"));
        }
    }

    [Test]
    public async Task CanExport_WhenOutputPathIsBlank_ReturnsFalse() {
        ISettingsService settingsService = Substitute.For<ISettingsService>();
        settingsService.LoadAsync().Returns(Task.FromResult(CreateSchemaOptions(@"C:\Exports", null, null, new ExportResultOptions())));
        ExportWorkspace sut = CreateWorkspace(settingsService);
        await sut.ReloadSettingsAsync();

        sut.OutputPath = " ";

        Assert.That(sut.CanExport, Is.False);
    }

    private static ExportWorkspace CreateWorkspace(
        ISettingsService settingsService,
        IDatabaseSchemaProviderFactory? providerFactory = null,
        IDialogService? dialogService = null,
        IOutputFolderLauncher? launcher = null
    ) {
        IOutputFolderLauncher resolvedLauncher = launcher ?? Substitute.For<IOutputFolderLauncher>();
        SchemaExportOrchestrator exportOrchestrator = new(
            providerFactory ?? Substitute.For<IDatabaseSchemaProviderFactory>(),
            Substitute.For<ILogger<SchemaExportOrchestrator>>(),
            new SchemaSnapshotBuilder(),
            new SchemaSnapshotDiffService(),
            resolvedLauncher
        );
        return new ExportWorkspace(
            settingsService,
            exportOrchestrator,
            new SchemaExportRequestResolver(),
            dialogService ?? Substitute.For<IDialogService>(),
            Substitute.For<IFileDialogService>(),
            resolvedLauncher
        );
    }

    private static IDatabaseSchemaProviderFactory CreateProviderFactory(SchemaConnection connection) {
        IDatabaseSchemaProviderFactory providerFactory = Substitute.For<IDatabaseSchemaProviderFactory>();
        IReadOnlyList<DatabaseObjectSchema> objects = [
            new DatabaseObjectSchema {
                SchemaName = "dbo",
                ObjectName = "Users",
                ObjectType = "TABLE",
                ObjectDescription = "User table"
            }
        ];
        providerFactory.LoadObjectsAsync(connection.DatabaseType, connection.ConnectionString, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(objects));
        providerFactory.LoadDetailsAsync(
            connection.DatabaseType,
            connection.ConnectionString,
            Arg.Any<IReadOnlyList<DatabaseObjectSchema>>(),
            Arg.Any<CancellationToken>()
        ).Returns(Task.FromResult(new DatabaseSchemaDetails {
            Columns = [
                new DatabaseColumnSchema {
                    SchemaName = "dbo",
                    ObjectName = "Users",
                    ObjectType = "TABLE",
                    ColumnName = "Id",
                    ColumnType = "int",
                    IsNullable = "NO",
                    IsPrimaryKey = "YES",
                    IsIdentity = "YES",
                    ColumnOrder = 1
                }
            ],
            Indexes = [],
            Routines = [],
            Relationships = []
        }));
        return providerFactory;
    }

    private static SchemaOptions CreateSchemaOptions(
        string exportPath,
        string? lastSelectedConnectionName,
        string? lastSelectedProfileName,
        ExportResultOptions exportResultOptions,
        IReadOnlyList<SchemaConnection>? connections = null,
        IReadOnlyList<ExportProfile>? exportProfiles = null
    ) {
        return new SchemaOptions {
            ExportPath = exportPath,
            Connections = connections ?? [
                CreateConnection("Primary", "Default"),
                CreateConnection("Analytics", "Compact")
            ],
            LastSelectedConnectionName = lastSelectedConnectionName,
            ExportProfiles = exportProfiles ?? [
                CreateProfile("Default"),
                CreateProfile("Compact")
            ],
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
