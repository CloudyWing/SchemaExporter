using System.Text;
using CloudyWing.SchemaExporter.Core.Exporting;
using CloudyWing.SchemaExporter.Core.Exporting.CommentSql;
using CloudyWing.SchemaExporter.Core.Tests.Infrastructure;

namespace CloudyWing.SchemaExporter.Core.Tests.Exporting;

[TestFixture]
public sealed class CommentSqlServiceTests {
    private const string ValidCommentList = """
        [
          { "schema": "dbo", "object": "Users", "column": "", "comment": "使用者資料表" },
          { "schema": "dbo", "object": "Users", "column": "Id", "comment": "識別碼 it's" },
          { "schema": "dbo", "object": "Users", "column": "Name", "comment": "Display name" }
        ]
        """;

    [Test]
    public async Task GenerateToFileAsync_WhenSnapshotIsSqlServer_WritesUtf8FileWithBom() {
        using TempDirectoryScope directory = new();
        (string snapshotPath, string commentsPath) = await CreateInputsAsync(directory, ValidCommentList);
        string outputPath = Path.Combine(directory.Path, "nested", "folder", "comments.sql");

        CommentSqlResult result = await new CommentSqlService().GenerateToFileAsync(snapshotPath, commentsPath, outputPath);

        byte[] bytes = await File.ReadAllBytesAsync(outputPath);
        string text = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        using (Assert.EnterMultipleScope()) {
            Assert.That(bytes.Take(3), Is.EqualTo(new byte[] { 0xEF, 0xBB, 0xBF }));
            Assert.That(text, Is.EqualTo(result.SqlText));
            Assert.That(result.DatabaseType, Is.EqualTo(DatabaseType.SqlServer));
            Assert.That(result.AddCount, Is.EqualTo(1));
            Assert.That(result.UpdateCount, Is.Zero);
            Assert.That(result.UnchangedCount, Is.EqualTo(2));
            Assert.That(text, Does.Contain("N'識別碼 it''s'"));
        }
    }

    [Test]
    public async Task GenerateToFileAsync_WhenSnapshotIsOracle_WritesUtf8FileWithoutBom() {
        using TempDirectoryScope directory = new();
        (string snapshotPath, string commentsPath) = await CreateInputsAsync(directory, ValidCommentList, databaseType: "Oracle");
        string outputPath = Path.Combine(directory.Path, "comments.sql");

        await new CommentSqlService().GenerateToFileAsync(snapshotPath, commentsPath, outputPath);

        byte[] bytes = await File.ReadAllBytesAsync(outputPath);
        string text = Encoding.UTF8.GetString(bytes);
        using (Assert.EnterMultipleScope()) {
            Assert.That(bytes.Take(3), Is.Not.EqualTo(new byte[] { 0xEF, 0xBB, 0xBF }));
            Assert.That(text, Does.StartWith("SET DEFINE OFF\nSET SQLBLANKLINES ON\n"));
            Assert.That(text, Does.Contain("COMMENT ON COLUMN \"dbo\".\"Users\".\"Id\" IS '識別碼 it''s';"));
        }
    }

    [Test]
    public async Task GenerateToFileAsync_WhenRunTwice_ProducesByteIdenticalFiles() {
        using TempDirectoryScope directory = new();
        (string snapshotPath, string commentsPath) = await CreateInputsAsync(directory, ValidCommentList);
        string firstPath = Path.Combine(directory.Path, "first.sql");
        string secondPath = Path.Combine(directory.Path, "second.sql");
        CommentSqlService sut = new();

        await sut.GenerateToFileAsync(snapshotPath, commentsPath, firstPath);
        await sut.GenerateToFileAsync(snapshotPath, commentsPath, secondPath);

        Assert.That(await File.ReadAllBytesAsync(secondPath), Is.EqualTo(await File.ReadAllBytesAsync(firstPath)));
    }

    [Test]
    public async Task GenerateToFileAsync_WhenListHasInvalidEntry_ThrowsAndLeavesExistingOutputUntouched() {
        using TempDirectoryScope directory = new();
        (string snapshotPath, string commentsPath) = await CreateInputsAsync(
            directory,
            """[ { "schema": "dbo", "object": "Missing", "column": "", "comment": "x" } ]"""
        );
        string outputPath = Path.Combine(directory.Path, "comments.sql");
        await File.WriteAllTextAsync(outputPath, "previous content");

        ExportValidationException? exception = Assert.ThrowsAsync<ExportValidationException>(
            () => new CommentSqlService().GenerateToFileAsync(snapshotPath, commentsPath, outputPath)
        );

        using (Assert.EnterMultipleScope()) {
            Assert.That(exception!.Message, Does.Contain("找不到物件"));
            Assert.That(await File.ReadAllTextAsync(outputPath), Is.EqualTo("previous content"));
        }
    }

    [Test]
    public async Task GenerateToFileAsync_WhenListHasInvalidEntry_DoesNotCreateOutputFile() {
        using TempDirectoryScope directory = new();
        (string snapshotPath, string commentsPath) = await CreateInputsAsync(
            directory,
            """[ { "schema": "dbo", "object": "Users", "column": "Id", "comment": "a\u0000b" } ]"""
        );
        string outputPath = Path.Combine(directory.Path, "comments.sql");

        Assert.ThrowsAsync<ExportValidationException>(
            () => new CommentSqlService().GenerateToFileAsync(snapshotPath, commentsPath, outputPath)
        );

        Assert.That(File.Exists(outputPath), Is.False);
    }

    [Test]
    public async Task GenerateToFileAsync_WhenOutputPathEqualsAnInputPath_ThrowsAndKeepsTheInputUntouched() {
        using TempDirectoryScope directory = new();
        (string snapshotPath, string commentsPath) = await CreateInputsAsync(directory, ValidCommentList);
        string snapshotBefore = await File.ReadAllTextAsync(snapshotPath);
        string commentsBefore = await File.ReadAllTextAsync(commentsPath);
        CommentSqlService sut = new();

        Assert.ThrowsAsync<ExportValidationException>(() => sut.GenerateToFileAsync(snapshotPath, commentsPath, commentsPath));
        Assert.ThrowsAsync<ExportValidationException>(() => sut.GenerateToFileAsync(snapshotPath, commentsPath, Path.Combine(directory.Path, ".", "input.snapshot.json")));

        using (Assert.EnterMultipleScope()) {
            Assert.That(await File.ReadAllTextAsync(snapshotPath), Is.EqualTo(snapshotBefore));
            Assert.That(await File.ReadAllTextAsync(commentsPath), Is.EqualTo(commentsBefore));
        }
    }

    [Test]
    public async Task GenerateToFileAsync_WhenWriteSucceeds_LeavesNoTemporaryFileBehind() {
        using TempDirectoryScope directory = new();
        (string snapshotPath, string commentsPath) = await CreateInputsAsync(directory, ValidCommentList);
        string outputPath = Path.Combine(directory.Path, "comments.sql");
        await File.WriteAllTextAsync(outputPath, "previous content");

        await new CommentSqlService().GenerateToFileAsync(snapshotPath, commentsPath, outputPath);

        using (Assert.EnterMultipleScope()) {
            Assert.That(Directory.GetFiles(directory.Path, "*.tmp"), Is.Empty);
            Assert.That(await File.ReadAllTextAsync(outputPath), Does.Not.Contain("previous content"));
        }
    }

    [Test]
    public async Task GenerateToFileAsync_WhenTargetPathIsADirectory_ThrowsValidationExceptionAndLeavesNoTemporaryFile() {
        using TempDirectoryScope directory = new();
        (string snapshotPath, string commentsPath) = await CreateInputsAsync(directory, ValidCommentList);
        string outputPath = Path.Combine(directory.Path, "occupied");
        Directory.CreateDirectory(outputPath);

        Assert.ThrowsAsync<ExportValidationException>(() => new CommentSqlService().GenerateToFileAsync(snapshotPath, commentsPath, outputPath));

        Assert.That(Directory.GetFiles(directory.Path, "*.tmp"), Is.Empty);
    }

    [TestCase("{ \"schema\": \"dbo\" }", TestName = "LoadCommentListAsync_WhenRootIsNotArray")]
    [TestCase("[ { \"schema\": \"dbo\", \"objekt\": \"Users\", \"comment\": \"x\" } ]", TestName = "LoadCommentListAsync_WhenPropertyNameIsMisspelled")]
    [TestCase("[ { \"schema\": \"dbo\", \"object\": \"Users\", \"comment\": 5 } ]", TestName = "LoadCommentListAsync_WhenCommentIsNotString")]
    [TestCase("[ null ]", TestName = "LoadCommentListAsync_WhenElementIsNull")]
    [TestCase("not json", TestName = "LoadCommentListAsync_WhenFileIsNotJson")]
    [TestCase("", TestName = "LoadCommentListAsync_WhenFileIsEmpty")]
    public async Task LoadCommentListAsync_WhenContentIsInvalid_ThrowsValidationException(string content) {
        using TempDirectoryScope directory = new();
        string path = Path.Combine(directory.Path, "comments.json");
        await File.WriteAllTextAsync(path, content);

        Assert.ThrowsAsync<ExportValidationException>(() => CommentSqlService.LoadCommentListAsync(path));
    }

    [Test]
    public void LoadCommentListAsync_WhenFileDoesNotExist_ThrowsValidationException() {
        Assert.ThrowsAsync<ExportValidationException>(
            () => CommentSqlService.LoadCommentListAsync(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.json"))
        );
    }

    [Test]
    public async Task LoadCommentListAsync_WhenFileHasBomAndNullColumn_ReadsEntries() {
        using TempDirectoryScope directory = new();
        string path = Path.Combine(directory.Path, "comments.json");
        string json = """[ { "Schema": "dbo", "OBJECT": "Users", "column": null, "comment": "表" }, { "schema": "dbo", "object": "Users", "comment": "" } ]""";
        await File.WriteAllBytesAsync(path, [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(json)]);

        IReadOnlyList<CommentSqlEntry> entries = await CommentSqlService.LoadCommentListAsync(path);

        using (Assert.EnterMultipleScope()) {
            Assert.That(entries, Has.Count.EqualTo(2));
            Assert.That(entries[0].Column, Is.Null);
            Assert.That(entries[0].Comment, Is.EqualTo("表"));
            Assert.That(entries[1].Comment, Is.Empty);
        }
    }

    private static async Task<(string SnapshotPath, string CommentsPath)> CreateInputsAsync(
        TempDirectoryScope directory,
        string commentListJson,
        string databaseType = "SqlServer"
    ) {
        string snapshotPath = Path.Combine(directory.Path, "input.snapshot.json");
        string commentsPath = Path.Combine(directory.Path, "comments.json");

        await SchemaTestData.WriteSnapshotAsync(snapshotPath, objectDescription: "使用者資料表");
        string snapshotJson = await File.ReadAllTextAsync(snapshotPath);
        await File.WriteAllTextAsync(
            snapshotPath,
            snapshotJson.Replace("\"databaseType\": \"SqlServer\"", $"\"databaseType\": \"{databaseType}\"", StringComparison.Ordinal)
        );
        await File.WriteAllTextAsync(commentsPath, commentListJson);

        return (snapshotPath, commentsPath);
    }
}
