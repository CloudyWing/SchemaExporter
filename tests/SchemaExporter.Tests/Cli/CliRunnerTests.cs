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
}
