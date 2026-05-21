using System.Text.Json;
using CloudyWing.SchemaExporter.Cli;
using CloudyWing.SchemaExporter.Core;
using CloudyWing.SchemaExporter.Core.Exporting;
using CloudyWing.SchemaExporter.Core.Exporting.Snapshots;
using CloudyWing.SchemaExporter.Core.SchemaProviders;
using CloudyWing.SchemaExporter.Services;
using CloudyWing.SpreadsheetExporter;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CloudyWing.SchemaExporter.Tests.Cli;

[TestFixture]
public sealed class CliRunnerTests {
    private static readonly JsonSerializerOptions snapshotJsonOptions = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    [OneTimeSetUp]
    public void OneTimeSetUp() {
        SpreadsheetExporterBootstrapper.Configure();
    }

    [Test]
    public async Task RunAsync_WhenHelpIsRequested_ReturnsSuccessCode() {
        CliRunner sut = CreateRunner();

        int result = await RunWithConsoleCaptureAsync(sut, ["--help"]);

        Assert.That(result, Is.EqualTo((int)CliExitCode.Success));
    }

    [Test]
    public async Task RunAsync_WhenArgumentIsInvalid_ReturnsArgumentErrorCode() {
        CliRunner sut = CreateRunner();

        int result = await RunWithConsoleCaptureAsync(sut, ["export", "--unknown"]);

        Assert.That(result, Is.EqualTo((int)CliExitCode.ArgumentError));
    }

    [Test]
    public async Task RunAsync_WhenWorkflowExceptionOccurs_ReturnsWorkflowErrorCode() {
        CliRunner sut = CreateRunner();

        int result = await RunWithConsoleCaptureAsync(sut, [
            "diff",
            "--left",
            @"C:\Missing\left.snapshot.json",
            "--right",
            @"C:\Missing\right.snapshot.json"
        ]);

        Assert.That(result, Is.EqualTo((int)CliExitCode.WorkflowError));
    }

    [Test]
    public async Task RunAsync_WhenExportArtifactsAreRequested_WritesWorkbookAndArtifacts() {
        string temporaryDirectory = CreateTemporaryDirectory();
        IDatabaseSchemaProviderFactory providerFactory = Substitute.For<IDatabaseSchemaProviderFactory>();
        SchemaConnection connection = new() {
            Name = "Primary",
            DatabaseType = DatabaseType.SqlServer,
            ConnectionString = "Server=.;Database=Inline;"
        };
        DatabaseSchemaExport schemaExport = CreateSchemaExport();
        SetupProviderFactory(providerFactory, connection, schemaExport);
        ISettingsService settingsService = Substitute.For<ISettingsService>();
        string outputDirectory = Path.Combine(temporaryDirectory, "exports");
        settingsService.LoadAsync().Returns(Task.FromResult(CreateSchemaOptions(temporaryDirectory, connection)));
        CliRunner sut = CreateRunner(settingsService, providerFactory);

        try {
            int result = await RunWithConsoleCaptureAsync(sut, [
                "export",
                "--connection",
                connection.Name,
                "--output",
                outputDirectory,
                "--manifest",
                "--json-sidecar",
                "--markdown-sidecar",
                "--schema-summary",
                "--snapshot",
                "--no-timestamp",
                "--no-open-output-folder"
            ]);

            string workbookPath = Path.Combine(
                outputDirectory,
                $"TableSchema_{connection.Name}{SpreadsheetManager.CreateDocument().FileNameExtension}"
            );
            string manifestPath = Path.Combine(outputDirectory, "TableSchema_Primary.manifest.json");
            string jsonSidecarPath = Path.Combine(outputDirectory, "TableSchema_Primary.schema.json");
            string markdownSidecarPath = Path.Combine(outputDirectory, "TableSchema_Primary.schema.md");
            string schemaSummaryPath = Path.Combine(outputDirectory, "TableSchema_Primary.schema-summary.md");
            string snapshotPath = Path.Combine(outputDirectory, "TableSchema_Primary.snapshot.json");

            Assert.That(result, Is.EqualTo((int)CliExitCode.Success));
            using (Assert.EnterMultipleScope()) {
                Assert.That(File.Exists(workbookPath), Is.True);
                Assert.That(File.Exists(manifestPath), Is.True);
                Assert.That(File.Exists(jsonSidecarPath), Is.True);
                Assert.That(File.Exists(markdownSidecarPath), Is.True);
                Assert.That(File.Exists(schemaSummaryPath), Is.True);
                Assert.That(File.Exists(snapshotPath), Is.True);
            }

            using JsonDocument manifestDocument = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath));
            using JsonDocument jsonSidecarDocument = JsonDocument.Parse(await File.ReadAllTextAsync(jsonSidecarPath));
            using JsonDocument snapshotDocument = JsonDocument.Parse(await File.ReadAllTextAsync(snapshotPath));
            JsonElement manifestResultOptions = manifestDocument.RootElement.GetProperty("resultOptions");
            JsonElement jsonSidecarRoot = jsonSidecarDocument.RootElement;
            JsonElement snapshotCounts = snapshotDocument.RootElement.GetProperty("counts");
            string markdownSidecar = await File.ReadAllTextAsync(markdownSidecarPath);
            string schemaSummary = await File.ReadAllTextAsync(schemaSummaryPath);
            using (Assert.EnterMultipleScope()) {
                Assert.That(manifestResultOptions.GetProperty("useTimestamp").GetBoolean(), Is.False);
                Assert.That(manifestResultOptions.GetProperty("openOutputFolder").GetBoolean(), Is.False);
                Assert.That(manifestResultOptions.GetProperty("generateManifest").GetBoolean(), Is.True);
                Assert.That(manifestResultOptions.GetProperty("generateJsonSidecar").GetBoolean(), Is.True);
                Assert.That(manifestResultOptions.GetProperty("generateSchemaSnapshot").GetBoolean(), Is.True);
                Assert.That(jsonSidecarRoot.TryGetProperty("snapshot", out _), Is.True);
                Assert.That(jsonSidecarRoot.GetProperty("diff").ValueKind, Is.EqualTo(JsonValueKind.Null));
                Assert.That(snapshotCounts.GetProperty("columns").GetInt32(), Is.EqualTo(2));
                Assert.That(markdownSidecar, Does.Contain("# Schema Export"));
                Assert.That(schemaSummary, Does.Contain("# Schema Summary"));
            }

            await providerFactory.Received(1).LoadObjectsAsync(
                connection.DatabaseType,
                connection.ConnectionString,
                Arg.Any<CancellationToken>()
            );
            await providerFactory.Received(1).LoadDetailsAsync(
                connection.DatabaseType,
                connection.ConnectionString,
                Arg.Any<IReadOnlyList<DatabaseObjectSchema>>(),
                Arg.Any<CancellationToken>()
            );
        } finally {
            DeleteDirectory(temporaryDirectory);
        }
    }

    [Test]
    [NonParallelizable]
    public async Task RunAsync_WhenDiffJsonOutputIsRequestedWithRelativePaths_WritesJsonArtifact() {
        string temporaryDirectory = CreateTemporaryDirectory();
        string previousCurrentDirectory = Environment.CurrentDirectory;
        CliRunner sut = CreateRunner();
        string outputPath = Path.Combine(temporaryDirectory, "reports", "schema.diff.json");

        try {
            await WriteSnapshotAsync(Path.Combine(temporaryDirectory, "left.snapshot.json"), includeNameColumn: false);
            await WriteSnapshotAsync(Path.Combine(temporaryDirectory, "right.snapshot.json"), includeNameColumn: true);
            Environment.CurrentDirectory = temporaryDirectory;

            int result = await RunWithConsoleCaptureAsync(sut, [
                "diff",
                "--left",
                "left.snapshot.json",
                "--right",
                "right.snapshot.json",
                "--output",
                Path.Combine("reports", "schema.diff.json"),
                "--format",
                "json"
            ]);

            Assert.That(result, Is.EqualTo((int)CliExitCode.Success));
            Assert.That(File.Exists(outputPath), Is.True);
            string json = await File.ReadAllTextAsync(outputPath);
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement summary = document.RootElement.GetProperty("summary");
            JsonElement firstColumnChange = document.RootElement.GetProperty("columnChanges")[0];
            using (Assert.EnterMultipleScope()) {
                Assert.That(document.RootElement.TryGetProperty("Summary", out _), Is.False);
                Assert.That(summary.GetProperty("addedColumns").GetInt32(), Is.EqualTo(1));
                Assert.That(firstColumnChange.GetProperty("changeType").GetString(), Is.EqualTo("Added"));
            }
        } finally {
            Environment.CurrentDirectory = previousCurrentDirectory;
            DeleteDirectory(temporaryDirectory);
        }
    }

    [Test]
    [NonParallelizable]
    public async Task RunAsync_WhenDiffMarkdownOutputIsRequested_WritesMarkdownArtifact() {
        string temporaryDirectory = CreateTemporaryDirectory();
        CliRunner sut = CreateRunner();
        string leftSnapshotPath = Path.Combine(temporaryDirectory, "left.snapshot.json");
        string rightSnapshotPath = Path.Combine(temporaryDirectory, "right.snapshot.json");
        string outputPath = Path.Combine(temporaryDirectory, "schema.diff.md");

        try {
            await WriteSnapshotAsync(leftSnapshotPath, includeNameColumn: false);
            await WriteSnapshotAsync(rightSnapshotPath, includeNameColumn: true);

            int result = await RunWithConsoleCaptureAsync(sut, [
                "diff",
                "--left",
                leftSnapshotPath,
                "--right",
                rightSnapshotPath,
                "--output",
                outputPath,
                "--format",
                "markdown"
            ]);

            Assert.That(result, Is.EqualTo((int)CliExitCode.Success));
            Assert.That(File.Exists(outputPath), Is.True);
            string markdown = await File.ReadAllTextAsync(outputPath);
            using (Assert.EnterMultipleScope()) {
                Assert.That(markdown, Does.Contain("# Schema Diff"));
                Assert.That(markdown, Does.Contain("## Summary"));
                Assert.That(markdown, Does.Contain("### Added: dbo.Users.Name (TABLE)"));
            }
        } finally {
            DeleteDirectory(temporaryDirectory);
        }
    }

    [Test]
    public async Task RunAsync_WhenUnexpectedExceptionOccurs_ReturnsUnexpectedErrorCode() {
        ISettingsService settingsService = Substitute.For<ISettingsService>();
        settingsService.LoadAsync().Returns<Task<SchemaOptions>>(_ => throw new InvalidOperationException("boom"));
        CliRunner sut = CreateRunner(settingsService);

        int result = await RunWithConsoleCaptureAsync(sut, ["export", "--connection", "Primary"]);

        Assert.That(result, Is.EqualTo((int)CliExitCode.UnexpectedError));
    }

    private static CliRunner CreateRunner(
        ISettingsService? settingsService = null,
        IDatabaseSchemaProviderFactory? providerFactory = null
    ) {
        SchemaExportOrchestrator exportOrchestrator = new(
            providerFactory ?? Substitute.For<IDatabaseSchemaProviderFactory>(),
            Substitute.For<ILogger<SchemaExportOrchestrator>>(),
            new SchemaSnapshotBuilder(),
            new SchemaSnapshotDiffService()
        );

        return new CliRunner(
            exportOrchestrator,
            new SchemaSnapshotDiffService(),
            settingsService ?? Substitute.For<ISettingsService>(),
            new SchemaExportRequestResolver()
        );
    }

    private static async Task<int> RunWithConsoleCaptureAsync(CliRunner runner, string[] args) {
        TextWriter originalOutput = Console.Out;
        TextWriter originalError = Console.Error;
        using StringWriter outputWriter = new();
        using StringWriter errorWriter = new();
        Console.SetOut(outputWriter);
        Console.SetError(errorWriter);

        try {
            return await runner.RunAsync(args);
        } finally {
            Console.SetOut(originalOutput);
            Console.SetError(originalError);
        }
    }

    private static string CreateTemporaryDirectory() {
        string path = Path.Combine(
            Path.GetTempPath(),
            "SchemaExporter.Tests",
            Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path) {
        if (Directory.Exists(path)) {
            Directory.Delete(path, recursive: true);
        }
    }

    private static void SetupProviderFactory(
        IDatabaseSchemaProviderFactory providerFactory,
        SchemaConnection connection,
        DatabaseSchemaExport schemaExport
    ) {
        providerFactory.LoadObjectsAsync(
            connection.DatabaseType,
            connection.ConnectionString,
            Arg.Any<CancellationToken>()
        )
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

    private static SchemaOptions CreateSchemaOptions(string exportPath, SchemaConnection connection) {
        return new SchemaOptions {
            ExportPath = exportPath,
            Connections = [connection],
            ExportProfiles = [
                new ExportProfile {
                    Name = "Default"
                }
            ],
            ExportResultOptions = new ExportResultOptions {
                UseTimestamp = true,
                OpenOutputFolder = true
            }
        };
    }

    private static DatabaseSchemaExport CreateSchemaExport() {
        return new DatabaseSchemaExport {
            Objects = [
                new DatabaseObjectSchema {
                    SchemaName = "dbo",
                    ObjectName = "Users",
                    ObjectType = "TABLE",
                    ObjectDescription = "User table"
                }
            ],
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
                },
                new DatabaseColumnSchema {
                    SchemaName = "dbo",
                    ObjectName = "Users",
                    ObjectType = "TABLE",
                    ColumnName = "Name",
                    ColumnType = "nvarchar(128)",
                    IsNullable = "NO",
                    ColumnDefault = "('unknown')",
                    IsPrimaryKey = "NO",
                    IsIdentity = "NO",
                    ColumnDescription = "Display name",
                    ColumnOrder = 2
                }
            ],
            Indexes = [
                new DatabaseIndexSchema {
                    SchemaName = "dbo",
                    ObjectName = "Users",
                    ObjectType = "TABLE",
                    IndexName = "PK_Users",
                    IsPrimaryKey = "YES",
                    IsClustered = "YES",
                    IsUnique = "YES",
                    IsForeignKey = "NO",
                    Columns = "Id"
                }
            ],
            Routines = [
                new DatabaseRoutineSchema {
                    SchemaName = "dbo",
                    RoutineName = "usp_GetUsers",
                    RoutineType = "PROCEDURE",
                    ParameterSignature = "@IsActive bit",
                    RoutineDescription = "Returns users",
                    RoutineDefinition = "SELECT [Id], [Name] FROM [dbo].[Users];"
                }
            ]
        };
    }

    private static async Task WriteSnapshotAsync(string path, bool includeNameColumn) {
        SchemaSnapshotDocument snapshot = CreateSnapshotDocument(path, includeNameColumn);
        string json = JsonSerializer.Serialize(snapshot, snapshotJsonOptions);
        await File.WriteAllTextAsync(path, json);
    }

    private static SchemaSnapshotDocument CreateSnapshotDocument(string outputFilePath, bool includeNameColumn) {
        List<SchemaSnapshotColumnDocument> columns = [
            new SchemaSnapshotColumnDocument {
                ColumnName = "Id",
                ColumnType = "int",
                IsNullable = "NO",
                ColumnDefault = "",
                IsPrimaryKey = "YES",
                IsIdentity = "YES",
                ColumnDescription = "",
                ColumnOrder = 1
            }
        ];

        if (includeNameColumn) {
            columns.Add(new SchemaSnapshotColumnDocument {
                ColumnName = "Name",
                ColumnType = "nvarchar(128)",
                IsNullable = "NO",
                ColumnDefault = "('unknown')",
                IsPrimaryKey = "NO",
                IsIdentity = "NO",
                ColumnDescription = "Display name",
                ColumnOrder = 2
            });
        }

        return new SchemaSnapshotDocument {
            SchemaVersion = 2,
            ExportedAt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            ConnectionName = "Primary",
            DatabaseType = DatabaseType.SqlServer.ToString(),
            ProfileName = "Default",
            OutputFilePath = outputFilePath,
            Counts = new SchemaSnapshotCounts {
                Objects = 1,
                Columns = columns.Count,
                Indexes = 1,
                Routines = 1
            },
            Diagnostics = [],
            Objects = [
                new SchemaSnapshotObjectDocument {
                    SchemaName = "dbo",
                    ObjectName = "Users",
                    ObjectType = "TABLE",
                    ObjectDescription = "User table",
                    Columns = columns,
                    Indexes = [
                        new SchemaSnapshotIndexDocument {
                            IndexName = "PK_Users",
                            IsPrimaryKey = "YES",
                            IsClustered = "YES",
                            IsUnique = "YES",
                            IsForeignKey = "NO",
                            Columns = "Id",
                            OtherColumns = ""
                        }
                    ]
                }
            ],
            Routines = [
                new SchemaSnapshotRoutineDocument {
                    SchemaName = "dbo",
                    ContainerName = "",
                    RoutineName = "usp_GetUsers",
                    RoutineType = "PROCEDURE",
                    OverloadIdentifier = "",
                    ParameterSignature = "@IsActive bit",
                    ReturnType = "",
                    RoutineDescription = "Returns users",
                    RoutineDefinition = "SELECT [Id], [Name] FROM [dbo].[Users];"
                }
            ]
        };
    }
}
