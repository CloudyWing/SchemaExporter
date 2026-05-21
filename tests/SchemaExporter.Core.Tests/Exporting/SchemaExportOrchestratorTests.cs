using System.Text.Json;
using CloudyWing.SchemaExporter.Core.Exporting;
using CloudyWing.SchemaExporter.Core.Exporting.Snapshots;
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
    public void Constructor_WhenLegacySignatureIsUsed_CreatesInstance() {
        SchemaExportOrchestrator sut = new(
            Substitute.For<IDatabaseSchemaProviderFactory>(),
            Substitute.For<ILogger<SchemaExportOrchestrator>>()
        );

        Assert.That(sut, Is.Not.Null);
    }

    [Test]
    public void ExportAsync_WhenConnectionNameIsMissing_ThrowsValidationException() {
        using TempDirectoryScope directory = new();
        IDatabaseSchemaProviderFactory providerFactory = Substitute.For<IDatabaseSchemaProviderFactory>();
        SchemaExportOrchestrator sut = CreateSubject(providerFactory);
        SchemaConnection connection = new() {
            Name = "   ",
            DatabaseType = DatabaseType.SqlServer,
            ConnectionString = ""
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
            $"TableSchema_{connection.Name}{SpreadsheetManager.CreateDocument().FileNameExtension}"
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
            GenerateSchemaSummary = true,
            GenerateSchemaSnapshot = true,
            DiffSourceSnapshotPath = baselineSnapshotPath
        };

        ExportResult result = await sut.ExportAsync(connection, directory.Path, CreateProfile(), resultOptions);

        Assert.That(result.OutputFilePath, Does.EndWith(SpreadsheetManager.CreateDocument().FileNameExtension));
        Assert.That(File.Exists(result.OutputFilePath), Is.True);
        Assert.That(result.ManifestFilePath, Is.Not.Null);
        Assert.That(result.JsonSidecarFilePath, Is.Not.Null);
        Assert.That(result.MarkdownSidecarFilePath, Is.Not.Null);
        Assert.That(result.SchemaSummaryFilePath, Is.Not.Null);
        Assert.That(result.SnapshotFilePath, Is.Not.Null);
        Assert.That(result.DiffFilePath, Is.Not.Null);
        string manifestFilePath = result.ManifestFilePath ?? throw new AssertionException("Expected a manifest file path.");
        string jsonSidecarFilePath = result.JsonSidecarFilePath ?? throw new AssertionException("Expected a JSON sidecar file path.");
        string markdownSidecarFilePath = result.MarkdownSidecarFilePath ?? throw new AssertionException("Expected a Markdown sidecar file path.");
        string schemaSummaryFilePath = result.SchemaSummaryFilePath ?? throw new AssertionException("Expected a schema summary file path.");
        string snapshotFilePath = result.SnapshotFilePath ?? throw new AssertionException("Expected a snapshot file path.");
        string diffFilePath = result.DiffFilePath ?? throw new AssertionException("Expected a diff file path.");
        Assert.That(File.Exists(manifestFilePath), Is.True);
        Assert.That(File.Exists(jsonSidecarFilePath), Is.True);
        Assert.That(File.Exists(markdownSidecarFilePath), Is.True);
        Assert.That(File.Exists(schemaSummaryFilePath), Is.True);
        Assert.That(File.Exists(snapshotFilePath), Is.True);
        Assert.That(File.Exists(diffFilePath), Is.True);
        Assert.That(result.Diagnostics.Any(x => x.Category == ExportDiagnosticCategory.Execution), Is.True);

        string diffJson = await File.ReadAllTextAsync(diffFilePath);
        string markdownSidecar = await File.ReadAllTextAsync(markdownSidecarFilePath);
        string schemaSummary = await File.ReadAllTextAsync(schemaSummaryFilePath);
        using JsonDocument manifestDocument = JsonDocument.Parse(await File.ReadAllTextAsync(manifestFilePath));
        using JsonDocument jsonSidecarDocument = JsonDocument.Parse(await File.ReadAllTextAsync(jsonSidecarFilePath));
        using JsonDocument snapshotDocument = JsonDocument.Parse(await File.ReadAllTextAsync(snapshotFilePath));
        using JsonDocument diffDocument = JsonDocument.Parse(diffJson);

        using (Assert.EnterMultipleScope()) {
            Assert.That(manifestDocument.RootElement.TryGetProperty("resultOptions", out JsonElement manifestResultOptions), Is.True);
            Assert.That(manifestDocument.RootElement.TryGetProperty("ResultOptions", out _), Is.False);
            Assert.That(manifestResultOptions.GetProperty("generateSchemaSummary").GetBoolean(), Is.True);
            Assert.That(jsonSidecarDocument.RootElement.TryGetProperty("snapshot", out _), Is.True);
            Assert.That(jsonSidecarDocument.RootElement.TryGetProperty("diff", out _), Is.True);
            Assert.That(snapshotDocument.RootElement.TryGetProperty("schemaVersion", out _), Is.True);
            Assert.That(diffDocument.RootElement.GetProperty("summary").GetProperty("addedColumns").GetInt32(), Is.EqualTo(1));
            Assert.That(diffDocument.RootElement.GetProperty("summary").GetProperty("modifiedObjects").GetInt32(), Is.EqualTo(1));
            Assert.That(diffDocument.RootElement.GetProperty("columnChanges")[0].GetProperty("changeType").GetString(), Is.EqualTo("Added"));
            Assert.That(markdownSidecar, Does.Contain("## Snapshot Diff"));
            Assert.That(markdownSidecar, Does.Contain("dbo.Users (TABLE)"));
            Assert.That(schemaSummary, Does.Contain("# Schema Summary"));
            Assert.That(schemaSummary, Does.Contain("routine signatures"));
        }

        await providerFactory.Received(1).LoadObjectsAsync(
            connection.DatabaseType,
            connection.ConnectionString,
            Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task ExportAsync_WhenProfileContainsBlankIncludePattern_IgnoresBlankPattern() {
        using TempDirectoryScope directory = new();
        IDatabaseSchemaProviderFactory providerFactory = Substitute.For<IDatabaseSchemaProviderFactory>();
        SchemaExportOrchestrator sut = CreateSubject(providerFactory);
        SchemaConnection connection = CreateConnection();
        DatabaseSchemaExport schemaExport = SchemaTestData.CreateSchemaExport();
        SetupProviderFactory(providerFactory, connection, schemaExport);
        ExportProfile profile = new() {
            Name = "Default",
            IncludeObjects = ["   "]
        };

        ExportResult result = await sut.ExportAsync(connection, directory.Path, profile, new ExportResultOptions());

        Assert.That(File.Exists(result.OutputFilePath), Is.True);
    }

    [Test]
    public async Task ExportAsync_WhenRedactionIsEnabled_RedactsSensitiveArtifactMetadata() {
        using TempDirectoryScope directory = new();
        IDatabaseSchemaProviderFactory providerFactory = Substitute.For<IDatabaseSchemaProviderFactory>();
        SchemaExportOrchestrator sut = CreateSubject(providerFactory);
        SchemaConnection connection = CreateConnection();
        DatabaseSchemaExport schemaExport = CreateSensitiveSchemaExport();
        SetupProviderFactory(providerFactory, connection, schemaExport);
        ExportResultOptions resultOptions = new() {
            GenerateJsonSidecar = true,
            GenerateMarkdownSidecar = true,
            GenerateSchemaSummary = true,
            GenerateSchemaSnapshot = true
        };
        SchemaRedactionOptions redaction = new() {
            Enabled = true,
            ReplacementText = "[MASKED]",
            SensitiveNamePatterns = ["password"],
            SensitiveTextPatterns = ["AKIA[0-9A-Z]+"]
        };

        ExportResult result = await sut.ExportAsync(
            connection,
            directory.Path,
            CreateProfile(),
            resultOptions,
            redaction
        );

        string snapshotFilePath = result.SnapshotFilePath
            ?? throw new AssertionException("Expected a snapshot file path.");
        string jsonSidecarFilePath = result.JsonSidecarFilePath
            ?? throw new AssertionException("Expected a JSON sidecar file path.");
        string markdownSidecarFilePath = result.MarkdownSidecarFilePath
            ?? throw new AssertionException("Expected a Markdown sidecar file path.");
        string schemaSummaryFilePath = result.SchemaSummaryFilePath
            ?? throw new AssertionException("Expected a schema summary file path.");
        string combinedArtifacts = string.Join(
            Environment.NewLine,
            await File.ReadAllTextAsync(snapshotFilePath),
            await File.ReadAllTextAsync(jsonSidecarFilePath),
            await File.ReadAllTextAsync(markdownSidecarFilePath),
            await File.ReadAllTextAsync(schemaSummaryFilePath)
        );

        using (Assert.EnterMultipleScope()) {
            Assert.That(combinedArtifacts, Does.Contain("[MASKED]"));
            Assert.That(combinedArtifacts, Does.Not.Contain("AKIA1234567890ABCDEF"));
            Assert.That(combinedArtifacts, Does.Not.Contain("not-a-real-password"));
            Assert.That(combinedArtifacts, Does.Not.Contain("Password hash"));
            Assert.That(result.Diagnostics.Any(x => x.Category == ExportDiagnosticCategory.Redaction), Is.True);
        }
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

    private static DatabaseSchemaExport CreateSensitiveSchemaExport() {
        return new DatabaseSchemaExport {
            Objects = [
                new DatabaseObjectSchema {
                    SchemaName = "dbo",
                    ObjectName = "Users",
                    ObjectType = "TABLE",
                    ObjectDescription = "Uses AKIA1234567890ABCDEF for external integration."
                }
            ],
            Columns = [
                new DatabaseColumnSchema {
                    SchemaName = "dbo",
                    ObjectName = "Users",
                    ObjectType = "TABLE",
                    ColumnName = "PasswordHash",
                    ColumnType = "nvarchar(256)",
                    IsNullable = "NO",
                    ColumnDefault = "('not-a-real-password')",
                    IsPrimaryKey = "NO",
                    IsIdentity = "NO",
                    ColumnDescription = "Password hash",
                    ColumnOrder = 1
                }
            ],
            Indexes = [],
            Routines = [
                new DatabaseRoutineSchema {
                    SchemaName = "dbo",
                    RoutineName = "usp_GetSecret",
                    RoutineType = "PROCEDURE",
                    ParameterSignature = "@Id int",
                    RoutineDescription = "Returns secret value",
                    RoutineDefinition = "SELECT 'AKIA1234567890ABCDEF';"
                }
            ]
        };
    }

    private static SchemaExportOrchestrator CreateSubject(IDatabaseSchemaProviderFactory providerFactory) {
        return new SchemaExportOrchestrator(
            providerFactory,
            Substitute.For<ILogger<SchemaExportOrchestrator>>(),
            new SchemaSnapshotBuilder(),
            new SchemaSnapshotDiffService()
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
