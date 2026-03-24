using CloudyWing.SchemaExporter.Core.Exporting;
using CloudyWing.SchemaExporter.Core.SchemaProviders;
using CloudyWing.SchemaExporter.Core.Tests.Infrastructure;
using CloudyWing.SpreadsheetExporter;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CloudyWing.SchemaExporter.Core.Tests.Exporting;

[TestFixture]
public sealed class SchemaExportOrchestratorTests {
    [OneTimeSetUp]
    public void OneTimeSetUp() {
        SpreadsheetExporterBootstrapper.Configure();
    }

    [Test]
    public void ExportAsync_WhenConnectionNameIsMissing_ThrowsValidationException() {
        using TempDirectoryScope directory = new();
        IDatabaseSchemaProviderFactory providerFactory = Substitute.For<IDatabaseSchemaProviderFactory>();
        SchemaExportOrchestrator sut = CreateSubject(providerFactory);
        SchemaConnection connection = new() {
            Name = "   ",
            DatabaseType = DatabaseType.SqlServer
        };

        ExportValidationException? exception = Assert.ThrowsAsync<ExportValidationException>(
            async () => await sut.ExportAsync(connection, directory.Path, CreateProfile(), new ExportResultOptions())
        );

        Assert.That(exception, Is.Not.Null);
        ExportValidationException assertedException = exception ?? throw new AssertionException("Expected an ExportValidationException.");
        Assert.That(assertedException.Message, Does.Contain("連線名稱"));
        providerFactory.DidNotReceiveWithAnyArgs().LoadObjectsAsync(Arg.Any<DatabaseType>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public void ExportAsync_WhenConnectionStringIsMissing_ThrowsValidationException() {
        using TempDirectoryScope directory = new();
        IDatabaseSchemaProviderFactory providerFactory = Substitute.For<IDatabaseSchemaProviderFactory>();
        SchemaExportOrchestrator sut = CreateSubject(providerFactory);
        SchemaConnection connection = new() {
            Name = "Primary",
            DatabaseType = DatabaseType.SqlServer,
            ConnectionString = "   "
        };

        ExportValidationException? exception = Assert.ThrowsAsync<ExportValidationException>(
            async () => await sut.ExportAsync(connection, directory.Path, CreateProfile(), new ExportResultOptions())
        );

        Assert.That(exception, Is.Not.Null);
        ExportValidationException assertedException = exception ?? throw new AssertionException("Expected an ExportValidationException.");
        Assert.That(assertedException.Message, Does.Contain("ConnectionString"));
        providerFactory.DidNotReceiveWithAnyArgs().LoadObjectsAsync(Arg.Any<DatabaseType>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public void ExportAsync_WhenSchemaLoadingFails_WrapsKnownProviderExceptions() {
        using TempDirectoryScope directory = new();
        IDatabaseSchemaProviderFactory providerFactory = Substitute.For<IDatabaseSchemaProviderFactory>();
        SchemaExportOrchestrator sut = CreateSubject(providerFactory);
        SchemaConnection connection = CreateConnection();

        providerFactory.LoadObjectsAsync(connection.DatabaseType, connection.ConnectionString, Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<DatabaseObjectSchema>>>(_ => throw new TimeoutException("database timed out"));

        ExportConnectionException? exception = Assert.ThrowsAsync<ExportConnectionException>(
            async () => await sut.ExportAsync(connection, directory.Path, CreateProfile(), new ExportResultOptions())
        );

        Assert.That(exception, Is.Not.Null);
        ExportConnectionException assertedException = exception ?? throw new AssertionException("Expected an ExportConnectionException.");
        Assert.That(assertedException.InnerException, Is.TypeOf<TimeoutException>());
        Assert.That(assertedException.Message, Does.Contain("無法載入"));
    }

    [Test]
    public void ExportAsync_WhenOutputAlreadyExistsAndOverwriteStrategyIsFail_ThrowsOutputException() {
        using TempDirectoryScope directory = new();
        IDatabaseSchemaProviderFactory providerFactory = Substitute.For<IDatabaseSchemaProviderFactory>();
        SchemaExportOrchestrator sut = CreateSubject(providerFactory);
        SchemaConnection connection = CreateConnection();
        string outputPath = Path.Combine(
            directory.Path,
            $"TableSchema_{connection.Name}{SpreadsheetManager.CreateExporter().FileNameExtension}"
        );

        File.WriteAllText(outputPath, "existing output");
        DatabaseSchemaExport schemaExport = SchemaTestData.CreateSchemaExport();
        SetupProviderFactory(providerFactory, connection, schemaExport);

        ExportResultOptions resultOptions = new() {
            OverwriteStrategy = OverwriteStrategy.Fail
        };

        ExportOutputException? exception = Assert.ThrowsAsync<ExportOutputException>(
            async () => await sut.ExportAsync(connection, directory.Path, CreateProfile(), resultOptions)
        );

        Assert.That(exception, Is.Not.Null);
        ExportOutputException assertedException = exception ?? throw new AssertionException("Expected an ExportOutputException.");
        Assert.That(assertedException.Message, Does.Contain("輸出檔案已存在"));
    }

    [Test]
    public async Task ExportAsync_WhenArtifactsAreEnabled_CreatesExpectedFilesAndDiffOutput() {
        using TempDirectoryScope directory = new();
        IDatabaseSchemaProviderFactory providerFactory = Substitute.For<IDatabaseSchemaProviderFactory>();
        SchemaExportOrchestrator sut = CreateSubject(providerFactory);
        SchemaConnection connection = CreateConnection();

        string baselineSnapshotPath = System.IO.Path.Combine(directory.Path, "baseline.snapshot.json");
        await SchemaTestData.WriteSnapshotAsync(
            baselineSnapshotPath,
            objectDescription: "Previous user table",
            includeNameColumn: false
        );

        DatabaseSchemaExport schemaExport = SchemaTestData.CreateSchemaExport();
        SetupProviderFactory(providerFactory, connection, schemaExport);

        ExportResultOptions resultOptions = new() {
            GenerateManifest = true,
            GenerateJsonSidecar = true,
            GenerateMarkdownSidecar = true,
            GenerateSchemaSnapshot = true,
            DiffSourceSnapshotPath = baselineSnapshotPath
        };

        ExportResult result = await sut.ExportAsync(connection, directory.Path, CreateProfile(), resultOptions);

        Assert.That(result.OutputFilePath, Does.EndWith(SpreadsheetManager.CreateExporter().FileNameExtension));
        Assert.That(File.Exists(result.OutputFilePath), Is.True);
        Assert.That(result.ManifestFilePath, Is.Not.Null);
        Assert.That(result.JsonSidecarFilePath, Is.Not.Null);
        Assert.That(result.MarkdownSidecarFilePath, Is.Not.Null);
        Assert.That(result.SnapshotFilePath, Is.Not.Null);
        Assert.That(result.DiffFilePath, Is.Not.Null);
        string manifestFilePath = result.ManifestFilePath ?? throw new AssertionException("Expected a manifest file path.");
        string jsonSidecarFilePath = result.JsonSidecarFilePath ?? throw new AssertionException("Expected a JSON sidecar file path.");
        string markdownSidecarFilePath = result.MarkdownSidecarFilePath ?? throw new AssertionException("Expected a Markdown sidecar file path.");
        string snapshotFilePath = result.SnapshotFilePath ?? throw new AssertionException("Expected a snapshot file path.");
        string diffFilePath = result.DiffFilePath ?? throw new AssertionException("Expected a diff file path.");
        Assert.That(File.Exists(manifestFilePath), Is.True);
        Assert.That(File.Exists(jsonSidecarFilePath), Is.True);
        Assert.That(File.Exists(markdownSidecarFilePath), Is.True);
        Assert.That(File.Exists(snapshotFilePath), Is.True);
        Assert.That(File.Exists(diffFilePath), Is.True);
        Assert.That(result.Diagnostics.Any(x => x.Category == ExportDiagnosticCategory.Execution), Is.True);

        string diffJson = await File.ReadAllTextAsync(diffFilePath);
        string markdownSidecar = await File.ReadAllTextAsync(markdownSidecarFilePath);

        Assert.That(diffJson, Does.Contain("\"AddedColumns\": 1"));
        Assert.That(diffJson, Does.Contain("\"ModifiedObjects\": 1"));
        Assert.That(markdownSidecar, Does.Contain("## Snapshot Diff"));
        Assert.That(markdownSidecar, Does.Contain("dbo.Users (TABLE)"));
        await providerFactory.Received(1).LoadObjectsAsync(
            connection.DatabaseType,
            connection.ConnectionString,
            Arg.Any<CancellationToken>()
        );
    }

    private static void SetupProviderFactory(
        IDatabaseSchemaProviderFactory providerFactory,
        SchemaConnection connection,
        DatabaseSchemaExport schemaExport
    ) {
        providerFactory.LoadObjectsAsync(connection.DatabaseType, connection.ConnectionString, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(schemaExport.Objects));

        providerFactory.LoadDetailsAsync(
            connection.DatabaseType,
            connection.ConnectionString,
            Arg.Any<IReadOnlyList<DatabaseObjectSchema>>(),
            Arg.Any<CancellationToken>()
        ).Returns(Task.FromResult(new DatabaseSchemaDetails {
            Columns = schemaExport.Columns,
            Indexes = schemaExport.Indexes,
            Routines = schemaExport.Routines
        }));
    }

    private static SchemaExportOrchestrator CreateSubject(IDatabaseSchemaProviderFactory providerFactory) {
        return new SchemaExportOrchestrator(
            providerFactory,
            Substitute.For<ILogger<SchemaExportOrchestrator>>()
        );
    }

    private static SchemaConnection CreateConnection() {
        return new SchemaConnection {
            Name = "Primary",
            DatabaseType = DatabaseType.SqlServer,
            ConnectionString = "Server=.;Database=Inline;"
        };
    }

    private static ExportProfile CreateProfile() {
        return new ExportProfile {
            Name = "Default"
        };
    }
}
