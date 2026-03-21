using System.Text;
using CloudyWing.SchemaExporter.Core;
using CloudyWing.SchemaExporter.Core.Exporting;
using CloudyWing.SchemaExporter.Core.Exporting.Diffs;
using CloudyWing.SchemaExporter.Core.Exporting.Snapshots;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CloudyWing.SchemaExporter.Cli;

internal static class Program {
    private static async Task<int> Main(string[] args) {
        Console.OutputEncoding = Encoding.UTF8;
        SpreadsheetExporterBootstrapper.Configure();

        if (!CliArguments.TryParse(args, out CliArguments? parsedArguments, out string? errorMessage, out bool showHelp)) {
            if (!string.IsNullOrWhiteSpace(errorMessage)) {
                Console.Error.WriteLine(errorMessage);
                Console.Error.WriteLine();
            }

            WriteUsage();
            return showHelp ? 0 : 1;
        }

        CliArguments arguments = parsedArguments ?? throw new InvalidOperationException("CLI parser returned success without arguments.");

        try {
            return arguments.Command switch {
                CliCommand.Diff => await ExecuteDiffAsync(arguments).ConfigureAwait(false),
                _ => await ExecuteExportAsync(arguments).ConfigureAwait(false)
            };
        } catch (ExportWorkflowException ex) {
            Console.Error.WriteLine($"{GetCommandLabel(arguments)} failed: {ex.Message}");
            return 2;
        } catch (Exception ex) {
            Console.Error.WriteLine($"Unexpected error: {ex.Message}");
            return 3;
        }
    }

    private static async Task<int> ExecuteExportAsync(CliArguments arguments) {
        IConfiguration configuration = LoadConfiguration();
        ServiceCollection serviceCollection = new();
        serviceCollection.AddLogging(logging => {
            logging.ClearProviders();
            logging.AddSimpleConsole(options => {
                options.SingleLine = true;
                options.TimestampFormat = "HH:mm:ss ";
            });
            logging.SetMinimumLevel(LogLevel.Information);
        });
        serviceCollection.AddSchemaExporterCore(configuration);

        using ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();
        SchemaOptions schemaOptions = serviceProvider.GetRequiredService<IOptions<SchemaOptions>>().Value;
        SchemaExportOrchestrator orchestrator = serviceProvider.GetRequiredService<SchemaExportOrchestrator>();

        SchemaConnection connection = ResolveConnection(schemaOptions, arguments);
        ExportProfile profile = ResolveProfile(schemaOptions, connection, arguments);
        ExportResultOptions resultOptions = BuildResultOptions(schemaOptions.ExportResultOptions, arguments);
        string outputPath = string.IsNullOrWhiteSpace(arguments.OutputPath)
            ? schemaOptions.ExportPath
            : arguments.OutputPath;

        Progress<ExportProgress> progress = new(exportProgress => {
            Console.WriteLine($"[{exportProgress.Stage}] {exportProgress.Message}");
        });

        ExportResult result = await orchestrator.ExportAsync(
            connection,
            outputPath,
            profile,
            resultOptions,
            progress,
            CancellationToken.None
        ).ConfigureAwait(false);

        Console.WriteLine();
        Console.WriteLine("Export completed successfully.");
        WriteArtifactLine("Workbook", result.OutputFilePath);
        WriteArtifactLine("Manifest", result.ManifestFilePath);
        WriteArtifactLine("JSON sidecar", result.JsonSidecarFilePath);
        WriteArtifactLine("Markdown sidecar", result.MarkdownSidecarFilePath);
        WriteArtifactLine("Snapshot", result.SnapshotFilePath);
        WriteArtifactLine("Diff", result.DiffFilePath);

        int warningCount = result.Diagnostics.Count(x => x.Severity == DiagnosticSeverity.Warning);
        Console.WriteLine($"Diagnostics: {result.Diagnostics.Count} total, {warningCount} warning(s).");
        foreach (ExportDiagnostic diagnostic in result.Diagnostics) {
            Console.WriteLine($"- [{diagnostic.SeverityText}/{diagnostic.CategoryText}] {diagnostic.Message}");
        }

        return 0;
    }

    private static async Task<int> ExecuteDiffAsync(CliArguments arguments) {
        string leftSnapshotPath = arguments.LeftSnapshotPath ?? throw new InvalidOperationException("Diff command is missing the left snapshot path.");
        string rightSnapshotPath = arguments.RightSnapshotPath ?? throw new InvalidOperationException("Diff command is missing the right snapshot path.");
        SchemaSnapshotDiffService diffService = new();
        SchemaDiffDocument diff = await diffService.CompareAsync(
            leftSnapshotPath,
            rightSnapshotPath,
            CancellationToken.None
        ).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(arguments.DiffOutputPath)) {
            Console.WriteLine(diffService.BuildMarkdownReport(diff));
            return 0;
        }

        string outputPath = Path.GetFullPath(arguments.DiffOutputPath.Trim());
        string? directoryPath = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directoryPath)) {
            Directory.CreateDirectory(directoryPath);
        }

        DiffOutputFormat outputFormat = ResolveDiffOutputFormat(arguments.DiffOutputFormat, outputPath);
        switch (outputFormat) {
            case DiffOutputFormat.Markdown:
                await diffService.WriteMarkdownAsync(outputPath, diff, CancellationToken.None).ConfigureAwait(false);
                break;
            default:
                await diffService.WriteJsonAsync(outputPath, diff, CancellationToken.None).ConfigureAwait(false);
                break;
        }

        Console.WriteLine($"Diff written: {outputPath}");
        return 0;
    }

    private static IConfiguration LoadConfiguration() {
        return new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();
    }

    private static SchemaConnection ResolveConnection(SchemaOptions schemaOptions, CliArguments arguments) {
        SchemaConnection? connection = schemaOptions.Connections.FirstOrDefault(x =>
            string.Equals(x.Name, arguments.ConnectionName, StringComparison.OrdinalIgnoreCase)
        );

        if (connection is null) {
            throw new ExportValidationException($"找不到名稱為「{arguments.ConnectionName}」的連線設定。");
        }

        return connection;
    }

    private static ExportProfile ResolveProfile(
        SchemaOptions schemaOptions,
        SchemaConnection connection,
        CliArguments arguments
    ) {
        string? requestedProfileName = string.IsNullOrWhiteSpace(arguments.ProfileName)
            ? connection.ExportProfileName
            : arguments.ProfileName;

        ExportProfile? profile = schemaOptions.ExportProfiles.FirstOrDefault(x =>
            string.Equals(x.Name, requestedProfileName, StringComparison.OrdinalIgnoreCase)
        );

        if (profile is not null) {
            return profile;
        }

        return schemaOptions.ExportProfiles.FirstOrDefault() ?? new ExportProfile {
            Name = "Default"
        };
    }

    private static ExportResultOptions BuildResultOptions(ExportResultOptions defaults, CliArguments arguments) {
        ArgumentNullException.ThrowIfNull(defaults, nameof(defaults));
        ArgumentNullException.ThrowIfNull(arguments, nameof(arguments));

        return new ExportResultOptions {
            UseTimestamp = arguments.UseTimestamp ?? defaults.UseTimestamp,
            TimestampFormat = defaults.TimestampFormat,
            OverwriteStrategy = defaults.OverwriteStrategy,
            OpenOutputFolder = arguments.OpenOutputFolder ?? defaults.OpenOutputFolder,
            GenerateManifest = arguments.GenerateManifest ?? defaults.GenerateManifest,
            GenerateJsonSidecar = arguments.GenerateJsonSidecar ?? defaults.GenerateJsonSidecar,
            GenerateMarkdownSidecar = arguments.GenerateMarkdownSidecar ?? defaults.GenerateMarkdownSidecar,
            GenerateSchemaSnapshot = arguments.GenerateSchemaSnapshot ?? defaults.GenerateSchemaSnapshot,
            DiffSourceSnapshotPath = arguments.DiffSourceSnapshotPath ?? defaults.DiffSourceSnapshotPath
        };
    }

    private static void WriteArtifactLine(string label, string? path) {
        if (string.IsNullOrWhiteSpace(path)) {
            return;
        }

        Console.WriteLine($"{label}: {path}");
    }

    private static DiffOutputFormat ResolveDiffOutputFormat(string? configuredFormat, string outputPath) {
        if (!string.IsNullOrWhiteSpace(configuredFormat)) {
            return configuredFormat.Trim().ToLowerInvariant() switch {
                "markdown" or "md" => DiffOutputFormat.Markdown,
                "json" => DiffOutputFormat.Json,
                _ => throw new ExportValidationException($"不支援的 diff 輸出格式：{configuredFormat}")
            };
        }

        return string.Equals(Path.GetExtension(outputPath), ".md", StringComparison.OrdinalIgnoreCase)
            ? DiffOutputFormat.Markdown
            : DiffOutputFormat.Json;
    }

    private static string GetCommandLabel(CliArguments arguments) {
        return arguments.Command switch {
            CliCommand.Diff => "Diff",
            _ => "Export"
        };
    }

    private static void WriteUsage() {
        Console.WriteLine("SchemaExporter CLI");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  schemaexporter export --connection <name> [options]");
        Console.WriteLine("  schemaexporter diff --left <path> --right <path> [options]");
        Console.WriteLine("  schemaexporter --help");
        Console.WriteLine();
        Console.WriteLine("Export options:");
        Console.WriteLine("  --connection <name>          Required. Connection name from appsettings.json.");
        Console.WriteLine("  --profile <name>             Optional export profile override.");
        Console.WriteLine("  --output <path>              Optional absolute output directory override.");
        Console.WriteLine("  --manifest                   Generate manifest output.");
        Console.WriteLine("  --no-manifest                Disable manifest output.");
        Console.WriteLine("  --json-sidecar               Generate a schema JSON sidecar.");
        Console.WriteLine("  --markdown-sidecar           Generate a schema Markdown sidecar.");
        Console.WriteLine("  --snapshot                   Generate a reusable schema snapshot JSON file.");
        Console.WriteLine("  --diff-from <path>           Generate a schema diff against the specified snapshot file.");
        Console.WriteLine("  --open-output-folder         Open the output folder after export completes.");
        Console.WriteLine("  --no-open-output-folder      Keep headless behavior and do not open the output folder.");
        Console.WriteLine("  --no-timestamp               Disable timestamp suffix in output filenames.");
        Console.WriteLine();
        Console.WriteLine("Diff options:");
        Console.WriteLine("  --left <path>                Required. Baseline snapshot or schema JSON file.");
        Console.WriteLine("  --right <path>               Required. Current snapshot or schema JSON file.");
        Console.WriteLine("  --output <path>              Optional output file. Defaults to console Markdown.");
        Console.WriteLine("  --format <json|markdown>     Optional output format when --output is provided.");
        Console.WriteLine("  --help                       Show this help text.");
    }

}

