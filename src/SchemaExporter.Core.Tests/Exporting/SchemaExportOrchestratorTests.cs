using CloudyWing.SchemaExporter.Core.Tests.Infrastructure;
using CloudyWing.SchemaExporter.Exporting;
using CloudyWing.SchemaExporter.SchemaProviders;
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
        // Arrange
        using TempDirectoryScope directory = new();
        IDatabaseSchemaProviderFactory providerFactory = Substitute.For<IDatabaseSchemaProviderFactory>();
        SchemaExportOrchestrator sut = CreateSubject(providerFactory);
        SchemaConnection connection = new() {
            Name = "   ",
            DatabaseType = DatabaseType.SqlServer
        };

        // Act
        ExportValidationException? exception = Assert.ThrowsAsync<ExportValidationException>(
            async () => await sut.ExportAsync(connection, directory.Path, CreateProfile(), new ExportResultOptions())
        );

        // Assert
        Assert.That(exception, Is.Not.Null);
        Assert.That(exception!.Message, Does.Contain("連線名稱"));
        providerFactory.DidNotReceiveWithAnyArgs().LoadSchemaAsync(default, default!, default);
    }

    [Test]
    public void ExportAsync_WhenConnectionStringIsMissing_ThrowsValidationException() {
        // Arrange
        using TempDirectoryScope directory = new();
        IDatabaseSchemaProviderFactory providerFactory = Substitute.For<IDatabaseSchemaProviderFactory>();
        SchemaExportOrchestrator sut = CreateSubject(providerFactory);
        SchemaConnection connection = new() {
            Name = "Primary",
            DatabaseType = DatabaseType.SqlServer,
            ConnectionString = "   "
        };

        // Act
        ExportValidationException? exception = Assert.ThrowsAsync<ExportValidationException>(
            async () => await sut.ExportAsync(connection, directory.Path, CreateProfile(), new ExportResultOptions())
        );

        // Assert
        Assert.That(exception, Is.Not.Null);
        Assert.That(exception!.Message, Does.Contain("ConnectionString"));
        providerFactory.DidNotReceiveWithAnyArgs().LoadSchemaAsync(default, default!, default);
    }

    [Test]
    public void ExportAsync_WhenSchemaLoadingFails_WrapsKnownProviderExceptions() {
        // Arrange
        using TempDirectoryScope directory = new();
        IDatabaseSchemaProviderFactory providerFactory = Substitute.For<IDatabaseSchemaProviderFactory>();
        SchemaExportOrchestrator sut = CreateSubject(providerFactory);
        SchemaConnection connection = CreateConnection();

        providerFactory.LoadSchemaAsync(connection.DatabaseType, connection.ConnectionString, Arg.Any<CancellationToken>())
            .Returns<Task<DatabaseSchemaExport>>(_ => throw new TimeoutException("database timed out"));

        // Act
        ExportConnectionException? exception = Assert.ThrowsAsync<ExportConnectionException>(
            async () => await sut.ExportAsync(connection, directory.Path, CreateProfile(), new ExportResultOptions())
        );

        // Assert
        Assert.That(exception, Is.Not.Null);
        Assert.That(exception!.InnerException, Is.TypeOf<TimeoutException>());
        Assert.That(exception.Message, Does.Contain("無法載入"));
    }

    [Test]
    public void ExportAsync_WhenOutputAlreadyExistsAndOverwriteStrategyIsFail_ThrowsOutputException() {
        // Arrange
        using TempDirectoryScope directory = new();
        IDatabaseSchemaProviderFactory providerFactory = Substitute.For<IDatabaseSchemaProviderFactory>();
        SchemaExportOrchestrator sut = CreateSubject(providerFactory);
        SchemaConnection connection = CreateConnection();
        string outputPath = System.IO.Path.Combine(
            directory.Path,
            $"TableSchema_{connection.Name}{SpreadsheetManager.CreateExporter().FileNameExtension}"
        );

        File.WriteAllText(outputPath, "existing output");
        providerFactory.LoadSchemaAsync(connection.DatabaseType, connection.ConnectionString, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(SchemaTestData.CreateSchemaExport()));

        ExportResultOptions resultOptions = new() {
            OverwriteStrategy = OverwriteStrategy.Fail
        };

        // Act
        ExportOutputException? exception = Assert.ThrowsAsync<ExportOutputException>(
            async () => await sut.ExportAsync(connection, directory.Path, CreateProfile(), resultOptions)
        );

        // Assert
        Assert.That(exception, Is.Not.Null);
        Assert.That(exception!.Message, Does.Contain("輸出檔案已存在"));
    }

    [Test]
    public async Task ExportAsync_WhenArtifactsAreEnabled_CreatesExpectedFilesAndDiffOutput() {
        // Arrange
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

        providerFactory.LoadSchemaAsync(connection.DatabaseType, connection.ConnectionString, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(SchemaTestData.CreateSchemaExport()));

        ExportResultOptions resultOptions = new() {
            GenerateManifest = true,
            GenerateJsonSidecar = true,
            GenerateMarkdownSidecar = true,
            GenerateSchemaSnapshot = true,
            DiffSourceSnapshotPath = baselineSnapshotPath
        };

        // Act
        ExportResult result = await sut.ExportAsync(connection, directory.Path, CreateProfile(), resultOptions);

        // Assert
        Assert.That(result.OutputFilePath, Does.EndWith(SpreadsheetManager.CreateExporter().FileNameExtension));
        Assert.That(File.Exists(result.OutputFilePath), Is.True);
        Assert.That(result.ManifestFilePath, Is.Not.Null);
        Assert.That(result.JsonSidecarFilePath, Is.Not.Null);
        Assert.That(result.MarkdownSidecarFilePath, Is.Not.Null);
        Assert.That(result.SnapshotFilePath, Is.Not.Null);
        Assert.That(result.DiffFilePath, Is.Not.Null);
        Assert.That(File.Exists(result.ManifestFilePath!), Is.True);
        Assert.That(File.Exists(result.JsonSidecarFilePath!), Is.True);
        Assert.That(File.Exists(result.MarkdownSidecarFilePath!), Is.True);
        Assert.That(File.Exists(result.SnapshotFilePath!), Is.True);
        Assert.That(File.Exists(result.DiffFilePath!), Is.True);
        Assert.That(result.Diagnostics.Any(x => x.Category == ExportDiagnosticCategory.Execution), Is.True);

        string diffJson = await File.ReadAllTextAsync(result.DiffFilePath!);
        string markdownSidecar = await File.ReadAllTextAsync(result.MarkdownSidecarFilePath!);

        Assert.That(diffJson, Does.Contain("\"AddedColumns\": 1"));
        Assert.That(diffJson, Does.Contain("\"ModifiedObjects\": 1"));
        Assert.That(markdownSidecar, Does.Contain("## Snapshot Diff"));
        Assert.That(markdownSidecar, Does.Contain("dbo.Users (TABLE)"));
        await providerFactory.Received(1).LoadSchemaAsync(
            connection.DatabaseType,
            connection.ConnectionString,
            Arg.Any<CancellationToken>()
        );
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
