using CloudyWing.SchemaExporter.Cli;

namespace CloudyWing.SchemaExporter.Tests.Cli;

[TestFixture]
public sealed class CliArgumentsTests {
    [Test]
    public void TryParse_WhenExportArgumentsAreValid_ReturnsExportArguments() {
        string[] args = ["export", "--connection", "Primary", "--profile", "Default", "--manifest", "--schema-summary"];

        bool result = CliArguments.TryParse(
            args,
            out CliArguments? parsedArguments,
            out string? errorMessage,
            out bool showHelp
        );

        using (Assert.EnterMultipleScope()) {
            Assert.That(result, Is.True);
            Assert.That(parsedArguments?.Command, Is.EqualTo(CliCommand.Export));
            Assert.That(parsedArguments?.ConnectionName, Is.EqualTo("Primary"));
            Assert.That(parsedArguments?.ProfileName, Is.EqualTo("Default"));
            Assert.That(parsedArguments?.GenerateManifest, Is.True);
            Assert.That(parsedArguments?.GenerateSchemaSummary, Is.True);
            Assert.That(errorMessage, Is.Null);
            Assert.That(showHelp, Is.False);
        }
    }

    [Test]
    public void TryParse_WhenDiffArgumentsAreValid_ReturnsDiffArguments() {
        string[] args = ["diff", "--left", "left.snapshot.json", "--right", "right.snapshot.json", "--format", "json"];

        bool result = CliArguments.TryParse(
            args,
            out CliArguments? parsedArguments,
            out string? errorMessage,
            out bool showHelp
        );

        using (Assert.EnterMultipleScope()) {
            Assert.That(result, Is.True);
            Assert.That(parsedArguments?.Command, Is.EqualTo(CliCommand.Diff));
            Assert.That(parsedArguments?.LeftSnapshotPath, Is.EqualTo("left.snapshot.json"));
            Assert.That(parsedArguments?.RightSnapshotPath, Is.EqualTo("right.snapshot.json"));
            Assert.That(parsedArguments?.DiffOutputFormat, Is.EqualTo("json"));
            Assert.That(errorMessage, Is.Null);
            Assert.That(showHelp, Is.False);
        }
    }

    [Test]
    public void TryParse_WhenConnectionIsMissing_ReturnsArgumentError() {
        string[] args = ["export", "--manifest"];

        bool result = CliArguments.TryParse(
            args,
            out CliArguments? parsedArguments,
            out string? errorMessage,
            out bool showHelp
        );

        using (Assert.EnterMultipleScope()) {
            Assert.That(result, Is.False);
            Assert.That(parsedArguments, Is.Null);
            Assert.That(errorMessage, Is.EqualTo("--connection is required."));
            Assert.That(showHelp, Is.False);
        }
    }

    [Test]
    public void TryParse_WhenArgumentIsUnknown_ReturnsArgumentError() {
        string[] args = ["diff", "--left", "left.snapshot.json", "--right", "right.snapshot.json", "--bad"];

        bool result = CliArguments.TryParse(
            args,
            out CliArguments? parsedArguments,
            out string? errorMessage,
            out bool showHelp
        );

        using (Assert.EnterMultipleScope()) {
            Assert.That(result, Is.False);
            Assert.That(parsedArguments, Is.Null);
            Assert.That(errorMessage, Is.EqualTo("Unknown argument: --bad"));
            Assert.That(showHelp, Is.False);
        }
    }

    [Test]
    public void TryParse_WhenCommentSqlArgumentsAreValid_ReturnsCommentSqlArguments() {
        string[] args = ["comment-sql", "--snapshot", "db.snapshot.json", "--comments", "comments.json", "--output", "out.sql"];

        bool result = CliArguments.TryParse(
            args,
            out CliArguments? parsedArguments,
            out string? errorMessage,
            out bool showHelp
        );

        using (Assert.EnterMultipleScope()) {
            Assert.That(result, Is.True);
            Assert.That(parsedArguments?.Command, Is.EqualTo(CliCommand.CommentSql));
            Assert.That(parsedArguments?.CommentSnapshotPath, Is.EqualTo("db.snapshot.json"));
            Assert.That(parsedArguments?.CommentListPath, Is.EqualTo("comments.json"));
            Assert.That(parsedArguments?.CommentSqlOutputPath, Is.EqualTo("out.sql"));
            Assert.That(errorMessage, Is.Null);
            Assert.That(showHelp, Is.False);
        }
    }

    [TestCase(new[] { "comment-sql", "--comments", "c.json", "--output", "o.sql" }, "--snapshot is required.")]
    [TestCase(new[] { "comment-sql", "--snapshot", "s.json", "--output", "o.sql" }, "--comments is required.")]
    [TestCase(new[] { "comment-sql", "--snapshot", "s.json", "--comments", "c.json" }, "--output is required.")]
    [TestCase(new[] { "comment-sql", "--snapshot" }, "Missing value for --snapshot.")]
    [TestCase(new[] { "comment-sql", "--snapshot", "s.json", "--comments", "c.json", "--output", "o.sql", "--connection", "x" }, "Unknown argument: --connection")]
    public void TryParse_WhenCommentSqlArgumentsAreInvalid_ReturnsArgumentError(string[] args, string expectedMessage) {
        bool result = CliArguments.TryParse(
            args,
            out CliArguments? parsedArguments,
            out string? errorMessage,
            out bool showHelp
        );

        using (Assert.EnterMultipleScope()) {
            Assert.That(result, Is.False);
            Assert.That(parsedArguments, Is.Null);
            Assert.That(errorMessage, Is.EqualTo(expectedMessage));
            Assert.That(showHelp, Is.False);
        }
    }

    [Test]
    public void TryParse_WhenCommentSqlHelpIsRequested_ShowsHelp() {
        bool result = CliArguments.TryParse(
            ["comment-sql", "--help"],
            out CliArguments? parsedArguments,
            out string? errorMessage,
            out bool showHelp
        );

        using (Assert.EnterMultipleScope()) {
            Assert.That(result, Is.False);
            Assert.That(parsedArguments, Is.Null);
            Assert.That(errorMessage, Is.Null);
            Assert.That(showHelp, Is.True);
        }
    }

    [TestCase("--connection-string-env")]
    [TestCase("--connection-string-stdin")]
    [TestCase("--database-type")]
    public void TryParse_WhenRemovedConnectionOptionIsUsed_ReturnsUnknownArgumentWithoutEchoingValue(string option) {
        const string FakeSecret = "Server=db;Password=fake-secret-value";
        string[] args = ["export", "--connection", "LocalDev", option, FakeSecret];

        bool result = CliArguments.TryParse(
            args,
            out CliArguments? parsedArguments,
            out string? errorMessage,
            out bool showHelp
        );

        using (Assert.EnterMultipleScope()) {
            Assert.That(result, Is.False);
            Assert.That(parsedArguments, Is.Null);
            Assert.That(errorMessage, Is.EqualTo($"Unknown argument: {option}"));
            Assert.That(errorMessage, Does.Not.Contain(FakeSecret));
            Assert.That(showHelp, Is.False);
        }
    }
}
