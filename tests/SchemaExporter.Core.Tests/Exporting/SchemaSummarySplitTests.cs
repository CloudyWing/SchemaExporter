using CloudyWing.SchemaExporter.Core;
using CloudyWing.SchemaExporter.Core.Exporting;
using CloudyWing.SchemaExporter.Core.Exporting.Snapshots;
using CloudyWing.SchemaExporter.Core.SchemaProviders;
using CloudyWing.SchemaExporter.Core.Tests.Infrastructure;

namespace CloudyWing.SchemaExporter.Core.Tests.Exporting;

[TestFixture]
public sealed class SchemaSummarySplitTests {
    [Test]
    public async Task WriteArtifactsAsync_WhenDetailCountIsAtMost50_PreservesManualFiles() {
        using TempDirectoryScope directory = new();
        string outputPath = Path.Combine(directory.Path, "schema.xlsx");
        ArtifactOutputs splitOutputs = await WriteSummaryAsync(outputPath, CreateFilteredExport(50, 1));
        string splitSummaryPath = splitOutputs.SchemaSummaryFilePath
            ?? throw new AssertionException("Expected a schema summary file path.");
        string objectsDirectoryPath = Path.Combine(
            directory.Path,
            Path.GetFileNameWithoutExtension(splitSummaryPath) + ".objects"
        );
        string knownDetailPath = Directory.GetFiles(objectsDirectoryPath, "*Table_050.md").Single();
        string manualDetailPath = Path.Combine(objectsDirectoryPath, "manual-notes.md");
        await File.WriteAllTextAsync(manualDetailPath, "manual");

        ArtifactOutputs outputs = await WriteSummaryAsync(outputPath, CreateFilteredExport(49, 1));
        string summaryPath = outputs.SchemaSummaryFilePath
            ?? throw new AssertionException("Expected a schema summary file path.");
        string summary = await File.ReadAllTextAsync(summaryPath);

        using (Assert.EnterMultipleScope()) {
            Assert.That(Directory.Exists(objectsDirectoryPath), Is.True);
            Assert.That(File.Exists(knownDetailPath), Is.False);
            Assert.That(File.Exists(manualDetailPath), Is.True);
            Assert.That(summary, Does.Contain("## Objects"));
            Assert.That(summary, Does.Contain("## Routines"));
            Assert.That(summary, Does.Not.Contain("[details]("));
            Assert.That(summary, Does.Not.Contain("SELECT 1;"));
        }
    }

    [Test]
    public async Task WriteArtifactsAsync_WhenDetailCountIs51_EncodesInventoryLinksAndOmitsRoutineDefinition() {
        using TempDirectoryScope directory = new();
        string outputPath = Path.Combine(directory.Path, "schema Production#.xlsx");

        ArtifactOutputs outputs = await WriteSummaryAsync(outputPath, CreateFilteredExport(50, 1));
        string summaryPath = outputs.SchemaSummaryFilePath
            ?? throw new AssertionException("Expected a schema summary file path.");
        string summary = await File.ReadAllTextAsync(summaryPath);
        string objectsDirectoryPath = Path.Combine(
            directory.Path,
            Path.GetFileNameWithoutExtension(summaryPath) + ".objects"
        );
        string[] detailFiles = Directory.GetFiles(objectsDirectoryPath, "*.md");
        string firstObjectDetailPath = detailFiles.Single(
            x => Path.GetFileName(x).Contains("object_dbo_Table_001", StringComparison.Ordinal)
        );
        string routineDetailPath = detailFiles.Single(
            x => Path.GetFileName(x).Contains("routine_dbo_Get_Users", StringComparison.Ordinal)
        );
        string firstObjectDetail = await File.ReadAllTextAsync(firstObjectDetailPath);
        string routineDetail = await File.ReadAllTextAsync(routineDetailPath);
        string[] linkedFiles = summary
            .Split("[details](", StringSplitOptions.None)
            .Skip(1)
            .Select(link => link[..link.IndexOf(')')])
            .ToArray();

        using (Assert.EnterMultipleScope()) {
            Assert.That(detailFiles, Has.Length.EqualTo(51));
            Assert.That(linkedFiles, Has.Length.EqualTo(51));
            Assert.That(
                linkedFiles.All(link => File.Exists(
                    Path.Combine(
                        directory.Path,
                        Uri.UnescapeDataString(link.Replace('/', Path.DirectorySeparatorChar))
                    )
                )),
                Is.True
            );
            Assert.That(summary, Does.Contain("## Inventory"));
            Assert.That(summary, Does.Contain("## Relationships"));
            Assert.That(summary, Does.Not.Contain("## Objects"));
            Assert.That(summary, Does.Not.Contain("## Routines"));
            Assert.That(summary, Does.Not.Contain("SELECT 1;"));
            Assert.That(firstObjectDetail, Does.Contain("## Relationships"));
            Assert.That(firstObjectDetail, Does.Contain("FK_Table_001_Customers"));
            Assert.That(routineDetail, Does.Contain("Routine description"));
            Assert.That(routineDetail, Does.Not.Contain("SELECT 1;"));
            Assert.That(routineDetail, Does.Not.Contain("## Definition"));
            string encodedDirectoryName = Uri.EscapeDataString(Path.GetFileName(objectsDirectoryPath));
            Assert.That(summary, Does.Contain($"{encodedDirectoryName}/001_object_dbo_Table_001.md"));
            Assert.That(summary, Does.Contain($"{encodedDirectoryName}/051_routine_dbo_Get_Users.md"));
        }
    }

    [Test]
    public async Task WriteArtifactsAsync_WhenDetailDestinationConflictsWithManualFile_PreservesPreviousArtifacts() {
        using TempDirectoryScope directory = new();
        string outputPath = Path.Combine(directory.Path, "schema.xlsx");
        ArtifactOutputs outputs = await WriteSummaryAsync(outputPath, CreateFilteredExport(50, 1));
        string summaryPath = outputs.SchemaSummaryFilePath
            ?? throw new AssertionException("Expected a schema summary file path.");
        string previousSummary = await File.ReadAllTextAsync(summaryPath);
        string objectsDirectoryPath = Path.Combine(directory.Path, "schema.schema-summary.objects");
        string[] previousDetailPaths = Directory.GetFiles(objectsDirectoryPath, "*.md");
        string conflictingPath = Path.Combine(objectsDirectoryPath, "052_routine_dbo_Get_Users.md");
        await File.WriteAllTextAsync(conflictingPath, "manual");

        Assert.ThrowsAsync<ExportOutputException>(async () =>
            await WriteSummaryAsync(outputPath, CreateFilteredExport(51, 1))
        );

        using (Assert.EnterMultipleScope()) {
            Assert.That(await File.ReadAllTextAsync(summaryPath), Is.EqualTo(previousSummary));
            Assert.That(previousDetailPaths.All(File.Exists), Is.True);
            Assert.That(await File.ReadAllTextAsync(conflictingPath), Is.EqualTo("manual"));
        }
    }

    private static async Task<ArtifactOutputs> WriteSummaryAsync(
        string outputPath,
        FilteredSchemaExport filteredExport,
        CancellationToken cancellationToken = default
    ) {
        return await SchemaExportArtifactWriter.WriteArtifactsAsync(
            outputPath,
            new SchemaConnection {
                Name = "Primary",
                DatabaseType = DatabaseType.SqlServer,
                ConnectionString = "Server=.;Database=Schema;"
            },
            new ExportProfile { Name = "Default" },
            filteredExport,
            [],
            new ExportResultOptions { GenerateSchemaSummary = true },
            new SchemaSnapshotBuilder(),
            new SchemaSnapshotDiffService(),
            cancellationToken
        );
    }

    private static FilteredSchemaExport CreateFilteredExport(int objectCount, int routineCount) {
        List<DatabaseObjectSchema> objects = Enumerable.Range(1, objectCount)
            .Select(index => new DatabaseObjectSchema {
                SchemaName = "dbo",
                ObjectName = $"Table {index:D3}",
                ObjectType = "TABLE",
                ObjectDescription = $"Table {index:D3} description"
            })
            .ToList();
        List<DatabaseColumnSchema> columns = objects
            .Select(databaseObject => new DatabaseColumnSchema {
                SchemaName = databaseObject.SchemaName,
                ObjectName = databaseObject.ObjectName,
                ObjectType = databaseObject.ObjectType,
                ColumnName = "Id",
                ColumnType = "int",
                IsNullable = "NO",
                IsPrimaryKey = "YES",
                IsIdentity = "YES",
                ColumnDescription = "",
                ColumnOrder = 1
            })
            .ToList();
        List<DatabaseRoutineSchema> routines = Enumerable.Range(1, routineCount)
            .Select(index => new DatabaseRoutineSchema {
                SchemaName = "dbo",
                RoutineName = index == 1 ? "Get Users" : $"Get Users {index:D3}",
                RoutineType = "PROCEDURE",
                ParameterSignature = "",
                ReturnType = "",
                RoutineDescription = "Routine description",
                RoutineDefinition = "SELECT 1;"
            })
            .ToList();

        return new FilteredSchemaExport {
            Objects = objects,
            Columns = columns,
            Indexes = [],
            Routines = routines,
            Relationships = objectCount == 0
                ? []
                : [
                    new DatabaseForeignKeySchema {
                        ForeignKeyName = "FK_Table_001_Customers",
                        SourceSchemaName = "dbo",
                        SourceTableName = "Table 001",
                        SourceColumnName = "CustomerId",
                        ReferencedSchemaName = "dbo",
                        ReferencedTableName = "Customers",
                        ReferencedColumnName = "Id",
                        ColumnOrder = 1
                    }
                ]
        };
    }
}
