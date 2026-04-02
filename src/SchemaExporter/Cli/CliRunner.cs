using System.IO;
using CloudyWing.SchemaExporter.Core;
using CloudyWing.SchemaExporter.Core.Exporting;
using CloudyWing.SchemaExporter.Core.Exporting.Diffs;
using CloudyWing.SchemaExporter.Core.Exporting.Snapshots;
using CloudyWing.SchemaExporter.Services;

namespace CloudyWing.SchemaExporter.Cli;

/// <summary>
/// 負責解析命令列引數並分派至對應的 CLI 子命令執行器。
/// </summary>
internal sealed class CliRunner {
    private readonly SchemaExportOrchestrator exportOrchestrator;
    private readonly SchemaSnapshotDiffService diffService;
    private readonly ISettingsService settingsService;

    /// <summary>
    /// 初始化 <see cref="CliRunner"/> 類別的新執行個體。
    /// </summary>
    /// <param name="exportOrchestrator">Schema 匯出流程協調器。</param>
    /// <param name="diffService">Schema Snapshot 差異比對服務。</param>
    /// <param name="settingsService">設定檔存取服務。</param>
    public CliRunner(
        SchemaExportOrchestrator exportOrchestrator,
        SchemaSnapshotDiffService diffService,
        ISettingsService settingsService
    ) {
        ArgumentNullException.ThrowIfNull(exportOrchestrator);
        ArgumentNullException.ThrowIfNull(diffService);
        ArgumentNullException.ThrowIfNull(settingsService);

        this.exportOrchestrator = exportOrchestrator;
        this.diffService = diffService;
        this.settingsService = settingsService;
    }

    /// <summary>
    /// 解析命令列引數並執行對應的命令。
    /// </summary>
    /// <param name="args">應用程式啟動時傳入的命令列引數陣列。</param>
    /// <returns>代表執行結果的結束代碼；<c>0</c> 表示成功，非零表示失敗。</returns>
    public async Task<int> RunAsync(string[] args) {
        ArgumentNullException.ThrowIfNull(args);

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

    private async Task<int> ExecuteExportAsync(CliArguments arguments) {
        SchemaOptions schemaOptions = await settingsService.LoadAsync().ConfigureAwait(false);
        SchemaConnection connection = ResolveConnection(schemaOptions, arguments);
        ExportProfile profile = ResolveProfile(schemaOptions, connection, arguments);
        ExportResultOptions resultOptions = BuildResultOptions(schemaOptions.ExportResultOptions, arguments);
        string outputPath = string.IsNullOrWhiteSpace(arguments.OutputPath)
            ? schemaOptions.ExportPath
            : arguments.OutputPath;

        Progress<ExportProgress> progress = new(exportProgress => {
            Console.WriteLine($"[{exportProgress.Stage}] {exportProgress.Message}");
        });

        ExportResult result = await exportOrchestrator.ExportAsync(
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

    private async Task<int> ExecuteDiffAsync(CliArguments arguments) {
        string leftSnapshotPath = arguments.LeftSnapshotPath ?? throw new InvalidOperationException("Diff command is missing the left snapshot path.");
        string rightSnapshotPath = arguments.RightSnapshotPath ?? throw new InvalidOperationException("Diff command is missing the right snapshot path.");
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

    private static SchemaConnection ResolveConnection(SchemaOptions schemaOptions, CliArguments arguments) {
        string connectionName = arguments.ConnectionName
            ?? throw new InvalidOperationException("Export command is missing the connection name.");
        SchemaConnection? connection = schemaOptions.Connections.FirstOrDefault(x =>
            string.Equals(x.Name, connectionName, StringComparison.OrdinalIgnoreCase)
        );

        if (connection is null) {
            throw new ExportValidationException($"找不到名稱為「{connectionName}」的連線設定。");
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
        ArgumentNullException.ThrowIfNull(defaults);
        ArgumentNullException.ThrowIfNull(arguments);

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
