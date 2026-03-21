using System.Text.Json;
using CloudyWing.SchemaExporter.Core.Tests.Infrastructure;
using CloudyWing.SchemaExporter.Exporting;

namespace CloudyWing.SchemaExporter.Core.Tests.Exporting;

[TestFixture]
public sealed class SchemaSnapshotDiffServiceTests {
    [Test]
    public async Task CompareAsync_WhenSnapshotsDiffer_ReturnsExpectedSummaryAndMarkdown() {
        // Arrange
        using TempDirectoryScope directory = new();
        SchemaSnapshotDiffService sut = new();
        string leftSnapshotPath = System.IO.Path.Combine(directory.Path, "left.snapshot.json");
        string rightSnapshotPath = System.IO.Path.Combine(directory.Path, "right.snapshot.json");

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

        // Act
        SchemaDiffDocument diff = await sut.CompareAsync(leftSnapshotPath, rightSnapshotPath);
        string markdown = sut.BuildMarkdownReport(diff);

        // Assert
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
        // Arrange
        using TempDirectoryScope directory = new();
        SchemaSnapshotDiffService sut = new();
        string snapshotPath = System.IO.Path.Combine(directory.Path, "invalid.snapshot.json");

        await File.WriteAllTextAsync(snapshotPath, "{ invalid json");

        // Act
        ExportValidationException? exception = Assert.ThrowsAsync<ExportValidationException>(
            async () => await sut.LoadSnapshotAsync(snapshotPath)
        );

        // Assert
        Assert.That(exception, Is.Not.Null);
        Assert.That(exception!.Message, Does.Contain("格式無效"));
    }

    [Test]
    public async Task WriteJsonAsync_WhenDiffIsProvided_WritesSerializableJsonFile() {
        // Arrange
        using TempDirectoryScope directory = new();
        SchemaSnapshotDiffService sut = new();
        string leftSnapshotPath = System.IO.Path.Combine(directory.Path, "left.snapshot.json");
        string rightSnapshotPath = System.IO.Path.Combine(directory.Path, "right.snapshot.json");
        string outputPath = System.IO.Path.Combine(directory.Path, "schema.diff.json");

        await SchemaTestData.WriteSnapshotAsync(leftSnapshotPath, includeNameColumn: false);
        await SchemaTestData.WriteSnapshotAsync(rightSnapshotPath, includeNameColumn: true);
        SchemaDiffDocument diff = await sut.CompareAsync(leftSnapshotPath, rightSnapshotPath);

        // Act
        await sut.WriteJsonAsync(outputPath, diff);
        string json = await File.ReadAllTextAsync(outputPath);

        // Assert
        using JsonDocument document = JsonDocument.Parse(json);
        Assert.That(File.Exists(outputPath), Is.True);
        Assert.That(document.RootElement.GetProperty("Summary").GetProperty("AddedColumns").GetInt32(), Is.EqualTo(1));
    }
}
