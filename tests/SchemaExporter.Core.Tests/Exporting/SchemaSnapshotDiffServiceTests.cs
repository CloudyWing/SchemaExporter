using System.Text.Json;
using System.Text.Json.Nodes;
using CloudyWing.SchemaExporter.Core.Exporting;
using CloudyWing.SchemaExporter.Core.Exporting.Diffs;
using CloudyWing.SchemaExporter.Core.Exporting.Snapshots;
using CloudyWing.SchemaExporter.Core.Tests.Infrastructure;

namespace CloudyWing.SchemaExporter.Core.Tests.Exporting;

[TestFixture]
public sealed class SchemaSnapshotDiffServiceTests {
    [Test]
    public async Task CompareAsync_WhenSnapshotsDiffer_ReturnsExpectedSummaryAndMarkdown() {
        using TempDirectoryScope directory = new();
        SchemaSnapshotDiffService sut = new();
        string leftSnapshotPath = Path.Combine(directory.Path, "left.snapshot.json");
        string rightSnapshotPath = Path.Combine(directory.Path, "right.snapshot.json");

        await SchemaTestData.WriteSnapshotAsync(
            leftSnapshotPath,
            objectDescription: "Previous user table",
            includeNameColumn: false
        );
        await SchemaTestData.WriteSnapshotAsync(
            rightSnapshotPath,
            objectDescription: "Current user table",
            includeNameColumn: true
        );

        SchemaDiffDocument diff = await sut.CompareAsync(leftSnapshotPath, rightSnapshotPath);
        string markdown = sut.BuildMarkdownReport(diff);

        Assert.That(diff.Summary.ModifiedObjects, Is.EqualTo(1));
        Assert.That(diff.Summary.AddedColumns, Is.EqualTo(1));
        Assert.That(diff.ObjectChanges.Single().Identifier, Is.EqualTo("dbo.Users (TABLE)"));
        Assert.That(diff.ColumnChanges.Single().Identifier, Is.EqualTo("dbo.Users.Name (TABLE)"));
        Assert.That(markdown, Does.Contain("## Summary"));
        Assert.That(markdown, Does.Contain("### Modified: dbo.Users (TABLE)"));
        Assert.That(markdown, Does.Contain("### Added: dbo.Users.Name (TABLE)"));
    }

    [Test]
    public async Task LoadSnapshotAsync_WhenSnapshotJsonIsInvalid_ThrowsValidationException() {
        using TempDirectoryScope directory = new();
        SchemaSnapshotDiffService sut = new();
        string snapshotPath = Path.Combine(directory.Path, "invalid.snapshot.json");

        await File.WriteAllTextAsync(snapshotPath, "{ invalid json");

        ExportValidationException? exception = Assert.ThrowsAsync<ExportValidationException>(
            async () => await sut.LoadSnapshotAsync(snapshotPath)
        );

        Assert.That(exception, Is.Not.Null);
        ExportValidationException assertedException = exception ?? throw new AssertionException("Expected an ExportValidationException.");
        Assert.That(assertedException.Message, Does.Contain("格式無效"));
    }

    [Test]
    [NonParallelizable]
    public async Task CompareAsync_WhenSnapshotPathsAreRelative_ResolvesFromCurrentDirectory() {
        using TempDirectoryScope directory = new();
        SchemaSnapshotDiffService sut = new();
        string previousCurrentDirectory = Environment.CurrentDirectory;
        string leftSnapshotFileName = "left.snapshot.json";
        string rightSnapshotFileName = "right.snapshot.json";
        await SchemaTestData.WriteSnapshotAsync(Path.Combine(directory.Path, leftSnapshotFileName), includeNameColumn: false);
        await SchemaTestData.WriteSnapshotAsync(Path.Combine(directory.Path, rightSnapshotFileName), includeNameColumn: true);

        try {
            Environment.CurrentDirectory = directory.Path;

            SchemaDiffDocument diff = await sut.CompareAsync(leftSnapshotFileName, rightSnapshotFileName);

            using (Assert.EnterMultipleScope()) {
                Assert.That(diff.Summary.AddedColumns, Is.EqualTo(1));
                Assert.That(diff.LeftSnapshotPath, Is.EqualTo(Path.Combine(directory.Path, leftSnapshotFileName)));
                Assert.That(diff.RightSnapshotPath, Is.EqualTo(Path.Combine(directory.Path, rightSnapshotFileName)));
            }
        } finally {
            Environment.CurrentDirectory = previousCurrentDirectory;
        }
    }

    [Test]
    public async Task WriteJsonAsync_WhenDiffIsProvided_WritesSerializableJsonFile() {
        using TempDirectoryScope directory = new();
        SchemaSnapshotDiffService sut = new();
        string leftSnapshotPath = Path.Combine(directory.Path, "left.snapshot.json");
        string rightSnapshotPath = Path.Combine(directory.Path, "right.snapshot.json");
        string outputPath = Path.Combine(directory.Path, "schema.diff.json");

        await SchemaTestData.WriteSnapshotAsync(leftSnapshotPath, includeNameColumn: false);
        await SchemaTestData.WriteSnapshotAsync(rightSnapshotPath, includeNameColumn: true);
        SchemaDiffDocument diff = await sut.CompareAsync(leftSnapshotPath, rightSnapshotPath);

        await sut.WriteJsonAsync(outputPath, diff);
        string json = await File.ReadAllTextAsync(outputPath);

        using JsonDocument document = JsonDocument.Parse(json);
        Assert.That(File.Exists(outputPath), Is.True);
        using (Assert.EnterMultipleScope()) {
            Assert.That(document.RootElement.TryGetProperty("Summary", out _), Is.False);
            Assert.That(document.RootElement.GetProperty("summary").GetProperty("addedColumns").GetInt32(), Is.EqualTo(1));
            Assert.That(document.RootElement.GetProperty("columnChanges")[0].GetProperty("changeType").GetString(), Is.EqualTo("Added"));
        }
    }

    [Test]
    public async Task CompareAsync_WhenRelationshipsChange_ReturnsAddedRemovedModifiedEntries() {
        using TempDirectoryScope directory = new();
        SchemaSnapshotDiffService sut = new();
        string leftSnapshotPath = Path.Combine(directory.Path, "left.snapshot.json");
        string rightSnapshotPath = Path.Combine(directory.Path, "right.snapshot.json");

        await SchemaTestData.WriteSnapshotAsync(
            leftSnapshotPath,
            relationshipOverride: [
                CreateRelationship("FK_Keep", "Customers", "Id"),
                CreateRelationship("FK_Remove", "Departments", "Id")
            ]
        );
        await SchemaTestData.WriteSnapshotAsync(
            rightSnapshotPath,
            relationshipOverride: [
                CreateRelationship("FK_Keep", "Accounts", "Id"),
                CreateRelationship("FK_Add", "Orders", "UserId")
            ]
        );

        SchemaDiffDocument diff = await sut.CompareAsync(leftSnapshotPath, rightSnapshotPath);
        string markdown = sut.BuildMarkdownReport(diff);

        using (Assert.EnterMultipleScope()) {
            Assert.That(diff.Summary.AddedRelationships, Is.EqualTo(1));
            Assert.That(diff.Summary.RemovedRelationships, Is.EqualTo(1));
            Assert.That(diff.Summary.ModifiedRelationships, Is.EqualTo(1));
            Assert.That(diff.RelationshipChanges, Has.Count.EqualTo(3));
            Assert.That(diff.RelationshipChanges.Single(x => x.ChangeType == SchemaChangeType.Modified).Identifier, Is.EqualTo("FK_Keep: dbo.Users.Id"));
            Assert.That(
                diff.RelationshipChanges.Single(x => x.ChangeType == SchemaChangeType.Modified).PropertyChanges[nameof(SchemaSnapshotRelationshipDocument.ReferencedTableName)].Current,
                Is.EqualTo("Accounts")
            );
            Assert.That(markdown, Does.Contain("- Added Relationships: 1"));
            Assert.That(markdown, Does.Contain("## Relationship Changes"));
        }
    }

    [Test]
    public async Task LoadSnapshotAsync_WhenV2UsesLegacyIndexFields_MapsIncludedColumnsAndLeavesRelationshipsEmpty() {
        using TempDirectoryScope directory = new();
        SchemaSnapshotDiffService sut = new();
        string snapshotPath = Path.Combine(directory.Path, "legacy.snapshot.json");
        string currentSnapshotPath = Path.Combine(directory.Path, "current.snapshot.json");

        await SchemaTestData.WriteSnapshotAsync(snapshotPath);
        JsonObject root = JsonNode.Parse(await File.ReadAllTextAsync(snapshotPath))?.AsObject()
            ?? throw new AssertionException("Expected a JSON object.");
        JsonObject firstObject = root["objects"]?.AsArray()[0]?.AsObject()
            ?? throw new AssertionException("Expected a snapshot object.");
        JsonObject firstIndex = firstObject["indexes"]?.AsArray()[0]?.AsObject()
            ?? throw new AssertionException("Expected a snapshot index.");
        string includedColumns = firstIndex["includedColumns"]?.GetValue<string>() ?? "";
        firstIndex.Remove("includedColumns");
        firstIndex["otherColumns"] = includedColumns;
        firstIndex["isForeignKey"] = "No";
        root["schemaVersion"] = 2;
        root.Remove("relationships");
        await File.WriteAllTextAsync(snapshotPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        await SchemaTestData.WriteSnapshotAsync(currentSnapshotPath, includeNameColumn: false);

        SchemaSnapshotDocument snapshot = await sut.LoadSnapshotAsync(snapshotPath);
        SchemaDiffDocument diff = await sut.CompareAsync(snapshotPath, currentSnapshotPath);

        using (Assert.EnterMultipleScope()) {
            Assert.That(snapshot.SchemaVersion, Is.EqualTo(2));
            Assert.That(snapshot.Relationships, Is.Empty);
            Assert.That(snapshot.Objects.Single().Indexes.Single().IncludedColumns, Is.EqualTo(includedColumns));
            Assert.That(diff.RelationshipChanges, Is.Empty);
            Assert.That(diff.Summary.AddedRelationships, Is.EqualTo(0));
            Assert.That(diff.Summary.RemovedRelationships, Is.EqualTo(0));
            Assert.That(diff.Summary.ModifiedRelationships, Is.EqualTo(0));
            Assert.That(diff.Summary.RemovedColumns, Is.EqualTo(1));
        }
    }

    private static SchemaSnapshotRelationshipDocument CreateRelationship(
        string foreignKeyName,
        string referencedTableName,
        string referencedColumnName
    ) {
        return new SchemaSnapshotRelationshipDocument {
            ForeignKeyName = foreignKeyName,
            SourceSchemaName = "dbo",
            SourceTableName = "Users",
            SourceColumnName = "Id",
            ReferencedSchemaName = "dbo",
            ReferencedTableName = referencedTableName,
            ReferencedColumnName = referencedColumnName,
            ColumnOrder = 1
        };
    }
}

