using System.Text.Json;
using CloudyWing.SchemaExporter.Cli;
using CloudyWing.SchemaExporter.Core;
using CloudyWing.SchemaExporter.Core.Exporting;
using CloudyWing.SchemaExporter.Core.Exporting.Snapshots;
using CloudyWing.SchemaExporter.Core.SchemaProviders;
using CloudyWing.SchemaExporter.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CloudyWing.SchemaExporter.Tests.Cli;

[TestFixture]
public sealed class CliRunnerTests {
    private static readonly JsonSerializerOptions snapshotJsonOptions = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

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
            using (Assert.EnterMultipleScope()) {
                Assert.That(document.RootElement.TryGetProperty("Summary", out _), Is.False);
                Assert.That(document.RootElement.GetProperty("summary").GetProperty("addedColumns").GetInt32(), Is.EqualTo(1));
                Assert.That(document.RootElement.GetProperty("columnChanges")[0].GetProperty("changeType").GetString(), Is.EqualTo("Added"));
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

    private static CliRunner CreateRunner(ISettingsService? settingsService = null) {
        SchemaExportOrchestrator exportOrchestrator = new(
            Substitute.For<IDatabaseSchemaProviderFactory>(),
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
