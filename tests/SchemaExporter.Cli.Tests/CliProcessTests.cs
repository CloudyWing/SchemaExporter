using System.Diagnostics;

namespace CloudyWing.SchemaExporter.Cli.Tests;

/// <summary>
/// 以子處理程序啟動實際的 CLI 組件，驗證 Exit Code、stdout/stderr 與設定檔行為的對外契約。
/// </summary>
[TestFixture]
[NonParallelizable]
public sealed class CliProcessTests {
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromMinutes(1);
    private string temporaryDirectory = "";
    private string localAppDataDirectory = "";

    [SetUp]
    public void SetUp() {
        temporaryDirectory = Path.Combine(Path.GetTempPath(), "SchemaExporter.Cli.ProcessTests", Guid.NewGuid().ToString("N"));
        localAppDataDirectory = Path.Combine(temporaryDirectory, "LocalAppData");
        Directory.CreateDirectory(temporaryDirectory);
    }

    [TearDown]
    public void TearDown() {
        if (Directory.Exists(temporaryDirectory)) {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [TestCase]
    [TestCase("--help")]
    [TestCase("diff", "--help")]
    public async Task Run_WhenHelpIsRequested_WritesUsageAndReturnsSuccess(params string[] args) {
        CliProcessResult result = await RunCliAsync(args);

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.ExitCode, Is.EqualTo((int)CliExitCode.Success));
            Assert.That(result.Output, Does.Contain("Usage:"));
            Assert.That(result.Output, Does.Contain("Exit codes:"));
            Assert.That(result.Error, Is.Empty);
        }
    }

    [Test]
    public async Task Run_WhenCommandIsUnknown_WritesErrorToStandardErrorAndReturnsArgumentError() {
        CliProcessResult result = await RunCliAsync("unknown-command");

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.ExitCode, Is.EqualTo((int)CliExitCode.ArgumentError));
            Assert.That(result.Error, Does.Contain("Unknown command: unknown-command"));
        }
    }

    [Test]
    public async Task Run_WhenDiffSnapshotIsMissing_ReturnsWorkflowErrorWithoutCreatingUserConfig() {
        string missingPath = Path.Combine(temporaryDirectory, "missing.snapshot.json");

        CliProcessResult result = await RunCliAsync("diff", "--left", missingPath, "--right", missingPath);

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.ExitCode, Is.EqualTo((int)CliExitCode.WorkflowError));
            Assert.That(result.Error, Does.StartWith("Diff failed:"));
            Assert.That(Directory.Exists(localAppDataDirectory), Is.False);
        }
    }

    [Test]
    public async Task Run_WhenDiffSnapshotsAreValid_WritesMarkdownReportWithoutUserConfig() {
        string leftPath = Path.Combine(temporaryDirectory, "left.snapshot.json");
        string rightPath = Path.Combine(temporaryDirectory, "right.snapshot.json");
        string outputPath = Path.Combine(temporaryDirectory, "out", "diff.md");
        await CliRunnerTests.WriteSnapshotAsync(leftPath, includeNameColumn: false);
        await CliRunnerTests.WriteSnapshotAsync(rightPath, includeNameColumn: true);

        CliProcessResult result = await RunCliAsync("diff", "--left", leftPath, "--right", rightPath, "--output", outputPath);

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.ExitCode, Is.EqualTo((int)CliExitCode.Success), result.Error);
            Assert.That(result.Output, Does.Contain($"Diff written: {outputPath}"));
            Assert.That(File.Exists(outputPath), Is.True);
            Assert.That(Directory.Exists(localAppDataDirectory), Is.False);
        }
    }

    [Test]
    public async Task Run_WhenUserConfigIsCorruptAndCommandIsOffline_StillRuns() {
        string userConfigDirectory = Path.Combine(localAppDataDirectory, "SchemaExporter");
        Directory.CreateDirectory(userConfigDirectory);
        await File.WriteAllTextAsync(Path.Combine(userConfigDirectory, "appsettings.json"), "{ not json");

        CliProcessResult result = await RunCliAsync("comment-sql", "--help");

        Assert.That(result.ExitCode, Is.EqualTo((int)CliExitCode.Success));
    }

    [Test]
    public async Task Run_WhenExportUserConfigIsCorrupt_ReportsStartupFailure() {
        string userConfigDirectory = Path.Combine(localAppDataDirectory, "SchemaExporter");
        Directory.CreateDirectory(userConfigDirectory);
        await File.WriteAllTextAsync(Path.Combine(userConfigDirectory, "appsettings.json"), "{ not json");

        CliProcessResult result = await RunCliAsync("export", "--connection", "Primary");

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.Error, Does.StartWith("Startup failed:"));
        }
    }

    [Test]
    public async Task Run_WhenExportCannotConnect_WritesErrorsOnlyToStandardErrorWithoutPassword() {
        string userConfigDirectory = Path.Combine(localAppDataDirectory, "SchemaExporter");
        Directory.CreateDirectory(userConfigDirectory);
        // 連到不存在的本機埠，連線會立即被拒，不需等待逾時。
        await File.WriteAllTextAsync(Path.Combine(userConfigDirectory, "appsettings.json"), $$"""
            {
              "Schema": {
                "ExportPath": {{System.Text.Json.JsonSerializer.Serialize(Path.Combine(temporaryDirectory, "exports"))}},
                "Connections": [
                  {
                    "Name": "Broken",
                    "DatabaseType": "SqlServer",
                    "ConnectionString": "Server=tcp:127.0.0.1,1;Database=None;User Id=sa;Password=Secret123;Connect Timeout=2;TrustServerCertificate=True"
                  }
                ],
                "ExportProfiles": [ { "Name": "Default" } ]
              }
            }
            """);

        CliProcessResult result = await RunCliAsync("export", "--connection", "Broken");

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.ExitCode, Is.EqualTo((int)CliExitCode.WorkflowError));
            Assert.That(result.Error, Does.Contain("Export failed:"));
            Assert.That(result.Error, Does.Contain("fail:"));
            Assert.That(result.Output, Does.Not.Contain("fail:"));
            Assert.That(result.Output + result.Error, Does.Not.Contain("Secret123"));
        }
    }

    [Test]
    public async Task Run_WhenConnectionReferencesMissingEnvironmentVariable_ReturnsWorkflowError() {
        string userConfigDirectory = Path.Combine(localAppDataDirectory, "SchemaExporter");
        Directory.CreateDirectory(userConfigDirectory);
        await File.WriteAllTextAsync(Path.Combine(userConfigDirectory, "appsettings.json"), $$"""
            {
              "Schema": {
                "ExportPath": {{System.Text.Json.JsonSerializer.Serialize(Path.Combine(temporaryDirectory, "exports"))}},
                "Connections": [
                  { "Name": "FromEnv", "DatabaseType": "SqlServer", "ConnectionString": "${env:SCHEMAEXPORTER_PROCESS_TEST_MISSING}" }
                ],
                "ExportProfiles": [ { "Name": "Default" } ]
              }
            }
            """);

        CliProcessResult result = await RunCliAsync("export", "--connection", "FromEnv");

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.ExitCode, Is.EqualTo((int)CliExitCode.WorkflowError));
            Assert.That(result.Error, Does.Contain("SCHEMAEXPORTER_PROCESS_TEST_MISSING"));
        }
    }

    private async Task<CliProcessResult> RunCliAsync(params string[] args) {
        string cliAssemblyPath = Path.Combine(AppContext.BaseDirectory, "CloudyWing.SchemaExporter.Cli.dll");
        ProcessStartInfo startInfo = new() {
            FileName = ResolveDotnetHostPath(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = temporaryDirectory
        };
        startInfo.ArgumentList.Add(cliAssemblyPath);
        foreach (string arg in args) {
            startInfo.ArgumentList.Add(arg);
        }

        startInfo.Environment["LOCALAPPDATA"] = localAppDataDirectory;

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("無法啟動 CLI 子處理程序。");
        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();
        using CancellationTokenSource timeout = new(ProcessTimeout);
        await process.WaitForExitAsync(timeout.Token);

        return new CliProcessResult(process.ExitCode, await outputTask, (await errorTask).Trim());
    }

    private static string ResolveDotnetHostPath() {
        // dotnet test 會設定 DOTNET_HOST_PATH 指向執行測試的 dotnet；直接執行測試組件時退回 PATH 上的 dotnet。
        string? hostPath = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        return string.IsNullOrWhiteSpace(hostPath) ? "dotnet" : hostPath;
    }

    private sealed record CliProcessResult(int ExitCode, string Output, string Error);
}
