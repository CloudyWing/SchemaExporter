using System.IO;
using CloudyWing.SchemaExporter.Core;
using CloudyWing.SchemaExporter.Core.Exporting;
using CloudyWing.SchemaExporter.Core.Exporting.CommentSql;
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
    private readonly CommentSqlService commentSqlService;
    private readonly ISettingsService settingsService;
    private readonly SchemaExportRequestResolver requestResolver;

    /// <summary>
    /// 初始化 <see cref="CliRunner"/> 類別的新執行個體。
    /// </summary>
    /// <param name="exportOrchestrator">Schema 匯出流程協調器。</param>
    /// <param name="diffService">Schema Snapshot 差異比對服務。</param>
    /// <param name="commentSqlService">註解 SQL 產生服務。</param>
    /// <param name="settingsService">設定檔存取服務。</param>
    /// <param name="requestResolver">Schema 匯出請求解析器。</param>
    public CliRunner(
        SchemaExportOrchestrator exportOrchestrator,
        SchemaSnapshotDiffService diffService,
        CommentSqlService commentSqlService,
        ISettingsService settingsService,
        SchemaExportRequestResolver requestResolver
    ) {
        ArgumentNullException.ThrowIfNull(exportOrchestrator);
        ArgumentNullException.ThrowIfNull(diffService);
        ArgumentNullException.ThrowIfNull(commentSqlService);
        ArgumentNullException.ThrowIfNull(settingsService);
        ArgumentNullException.ThrowIfNull(requestResolver);

        this.exportOrchestrator = exportOrchestrator;
        this.diffService = diffService;
        this.commentSqlService = commentSqlService;
        this.settingsService = settingsService;
        this.requestResolver = requestResolver;
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
            return showHelp ? (int)CliExitCode.Success : (int)CliExitCode.ArgumentError;
        }

        CliArguments arguments = parsedArguments ?? throw new InvalidOperationException("CLI parser returned success without arguments.");
        string? connectionStringForRedaction = null;

        try {
            return arguments.Command switch {
                CliCommand.Diff => await ExecuteDiffAsync(arguments).ConfigureAwait(false),
                CliCommand.CommentSql => await ExecuteCommentSqlAsync(arguments).ConfigureAwait(false),
                _ => await ExecuteExportAsync(
                    arguments,
                    connectionString => connectionStringForRedaction = connectionString
                ).ConfigureAwait(false)
            };
        } catch (ExportWorkflowException ex) {
            Exception redactedException = SchemaExportOrchestrator.RedactException(ex, connectionStringForRedaction);
            Console.Error.WriteLine($"{GetCommandLabel(arguments)} failed: {redactedException.Message}");
            return (int)CliExitCode.WorkflowError;
        } catch (Exception ex) {
            Exception redactedException = SchemaExportOrchestrator.RedactException(ex, connectionStringForRedaction);
            Console.Error.WriteLine($"Unexpected error: {redactedException.Message}");
            return (int)CliExitCode.UnexpectedError;
        }
    }

    private async Task<int> ExecuteExportAsync(
        CliArguments arguments,
        Action<string?> setConnectionStringForRedaction
    ) {
        SchemaOptions schemaOptions = await settingsService.LoadAsync().ConfigureAwait(false);
        ExportOptionOverrides overrides = new() {
            OutputPath = arguments.OutputPath,
            OpenOutputFolder = arguments.OpenOutputFolder,
            GenerateManifest = arguments.GenerateManifest,
            GenerateJsonSidecar = arguments.GenerateJsonSidecar,
            GenerateMarkdownSidecar = arguments.GenerateMarkdownSidecar,
            GenerateSchemaSummary = arguments.GenerateSchemaSummary,
            GenerateSchemaSnapshot = arguments.GenerateSchemaSnapshot,
            UseTimestamp = arguments.UseTimestamp,
            DiffSourceSnapshotPath = arguments.DiffSourceSnapshotPath,
            OverrideDiffSourceSnapshotPath = !string.IsNullOrWhiteSpace(arguments.DiffSourceSnapshotPath)
        };
        SchemaExportRequest request = requestResolver.Resolve(
            schemaOptions,
            arguments.ConnectionName,
            arguments.ProfileName,
            overrides
        );
        setConnectionStringForRedaction(request.Connection.ConnectionString);

        Progress<ExportProgress> progress = new(exportProgress => {
            Console.WriteLine($"[{exportProgress.Stage}] {exportProgress.Message}");
        });

        ExportResult result = await exportOrchestrator.ExportAsync(
            request,
            progress,
            CancellationToken.None
        ).ConfigureAwait(false);

        Console.WriteLine();
        Console.WriteLine("Export completed successfully.");
        WriteArtifactLine("Workbook", result.OutputFilePath);
        WriteArtifactLine("Manifest", result.ManifestFilePath);
        WriteArtifactLine("JSON sidecar", result.JsonSidecarFilePath);
        WriteArtifactLine("Markdown sidecar", result.MarkdownSidecarFilePath);
        WriteArtifactLine("Schema summary", result.SchemaSummaryFilePath);
        WriteArtifactLine("Snapshot", result.SnapshotFilePath);
        WriteArtifactLine("Diff", result.DiffFilePath);

        WriteDiagnostics(result.Diagnostics);

        return (int)CliExitCode.Success;
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
            return (int)CliExitCode.Success;
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
        return (int)CliExitCode.Success;
    }

    private async Task<int> ExecuteCommentSqlAsync(CliArguments arguments) {
        string snapshotPath = arguments.CommentSnapshotPath ?? throw new InvalidOperationException("Comment SQL command is missing the snapshot path.");
        string commentListPath = arguments.CommentListPath ?? throw new InvalidOperationException("Comment SQL command is missing the comments path.");
        string outputPath = arguments.CommentSqlOutputPath ?? throw new InvalidOperationException("Comment SQL command is missing the output path.");

        CommentSqlResult result = await commentSqlService.GenerateToFileAsync(
            snapshotPath,
            commentListPath,
            outputPath,
            CancellationToken.None
        ).ConfigureAwait(false);

        Console.WriteLine($"Comment SQL written: {Path.GetFullPath(outputPath.Trim())}");
        Console.WriteLine($"Database type: {result.DatabaseType}");
        Console.WriteLine(
            $"Add: {result.AddCount}, Update: {result.UpdateCount}, "
            + $"Unchanged (skipped): {result.UnchangedCount}, Empty comment (skipped): {result.SkippedEmptyCount}"
        );
        foreach (string warning in result.Warnings) {
            Console.WriteLine($"Warning: {warning}");
        }

        return (int)CliExitCode.Success;
    }

    private static void WriteArtifactLine(string label, string? path) {
        if (string.IsNullOrWhiteSpace(path)) {
            return;
        }

        Console.WriteLine($"{label}: {path}");
    }

    private static void WriteDiagnostics(IReadOnlyList<ExportDiagnostic> diagnostics) {
        int warningCount = diagnostics.Count(x => x.Severity == DiagnosticSeverity.Warning);
        int errorCount = diagnostics.Count(x => x.Severity == DiagnosticSeverity.Error);
        Console.WriteLine($"Diagnostics: {diagnostics.Count} total, {warningCount} warning(s), {errorCount} error(s).");

        WriteDiagnosticsBySeverity(diagnostics, DiagnosticSeverity.Error);
        WriteDiagnosticsBySeverity(diagnostics, DiagnosticSeverity.Warning);
        WriteDiagnosticsBySeverity(diagnostics, DiagnosticSeverity.Info);
    }

    private static void WriteDiagnosticsBySeverity(
        IReadOnlyList<ExportDiagnostic> diagnostics,
        DiagnosticSeverity severity
    ) {
        List<ExportDiagnostic> matchedDiagnostics = diagnostics
            .Where(x => x.Severity == severity)
            .ToList();
        if (matchedDiagnostics.Count == 0) {
            return;
        }

        Console.WriteLine($"{matchedDiagnostics[0].SeverityText}:");
        foreach (ExportDiagnostic diagnostic in matchedDiagnostics) {
            Console.WriteLine($"- [{diagnostic.CategoryText}] {diagnostic.Message}");
        }
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
            CliCommand.CommentSql => "Comment SQL",
            _ => "Export"
        };
    }

    private static void WriteUsage() {
        Console.WriteLine("SchemaExporter CLI");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  schemaexporter export --connection <name> [options]");
        Console.WriteLine("  schemaexporter diff --left <path> --right <path> [options]");
        Console.WriteLine("  schemaexporter comment-sql --snapshot <path> --comments <path> --output <path>");
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
        Console.WriteLine("  --schema-summary             Generate a compact schema summary Markdown file.");
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
        Console.WriteLine();
        Console.WriteLine("Comment SQL options (offline: never connects to or queries a database):");
        Console.WriteLine("  --snapshot <path>            Required. Schema snapshot JSON file (provides names and existing comments).");
        Console.WriteLine("  --comments <path>            Required. JSON array of { schema, object, column, comment }; column is empty for tables and views.");
        Console.WriteLine("  --output <path>              Required. SQL file to write. SQL Server: UTF-8 with BOM; Oracle: UTF-8 without BOM.");
        Console.WriteLine();
        Console.WriteLine("  --help                       Show this help text.");
    }
}
