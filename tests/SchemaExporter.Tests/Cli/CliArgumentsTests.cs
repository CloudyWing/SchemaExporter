using CloudyWing.SchemaExporter.Cli;

namespace CloudyWing.SchemaExporter.Tests.Cli;

[TestFixture]
public sealed class CliArgumentsTests {
    [Test]
    public void TryParse_WhenExportArgumentsAreValid_ReturnsExportArguments() {
        string[] args = ["export", "--connection", "Primary", "--profile", "Default", "--manifest", "--ai-context"];

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
            Assert.That(parsedArguments?.GenerateAiContext, Is.True);
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
}
