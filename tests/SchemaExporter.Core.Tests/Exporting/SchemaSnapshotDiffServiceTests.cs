using System.Text.Json;
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
}

