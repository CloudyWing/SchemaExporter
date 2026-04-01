using System.ComponentModel;
using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CloudyWing.SchemaExporter.Core.SchemaProviders;
using CloudyWing.SpreadsheetExporter;
using CloudyWing.SpreadsheetExporter.Templates.Grid;
using CloudyWing.SpreadsheetExporter.Templates.RecordSet;
using Microsoft.Extensions.Logging;

namespace CloudyWing.SchemaExporter.Core.Exporting;

/// <summary>
/// Orchestrates the schema export workflow with validation, filtering, diagnostics, and progress reporting.
/// </summary>
public sealed partial class SchemaExportOrchestrator {
    private const int MaxSheetNameLength = 31;
    private static readonly char[] InvalidSheetNameCharacters = [':', '\\', '/', '?', '*', '[', ']'];

    private readonly IDatabaseSchemaProviderFactory providerFactory;
    private readonly ILogger<SchemaExportOrchestrator> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SchemaExportOrchestrator"/> class.
    /// </summary>
    public SchemaExportOrchestrator(
        IDatabaseSchemaProviderFactory providerFactory,
        ILogger<SchemaExportOrchestrator> logger
    ) {
        ArgumentNullException.ThrowIfNull(providerFactory, nameof(providerFactory));
        ArgumentNullException.ThrowIfNull(logger, nameof(logger));
        this.providerFactory = providerFactory;
        this.logger = logger;
    }

    /// <summary>
    /// Executes a schema export asynchronously.
    /// </summary>
    public async Task<ExportResult> ExportAsync(
        SchemaConnection connection,
        string exportPath,
        ExportProfile profile,
        ExportResultOptions resultOptions,
        IProgress<ExportProgress>? progress = null,
        CancellationToken cancellationToken = default
    ) {
        ArgumentNullException.ThrowIfNull(connection, nameof(connection));
        ArgumentNullException.ThrowIfNull(profile, nameof(profile));
        ArgumentNullException.ThrowIfNull(resultOptions, nameof(resultOptions));

        List<ExportDiagnostic> diagnostics = [];
        ExportStage currentStage = ExportStage.Validating;
        Stopwatch totalStopwatch = Stopwatch.StartNew();
        ExportExecutionSummary executionSummary = new();
        string? outputFilePath = null;

        try {
            LogExportStarted(logger, connection.Name, connection.DatabaseType, profile.Name);

            Stopwatch stageStopwatch = Stopwatch.StartNew();
            ReportProgress(progress, ExportStage.Validating, "正在驗證匯出設定...", 0);
            string normalizedExportPath = ValidateAndPrepareExportPath(exportPath);
            string connectionString = ValidateConnection(connection);
            ValidateResultOptions(resultOptions);
            cancellationToken.ThrowIfCancellationRequested();
            executionSummary.ValidationDuration = stageStopwatch.Elapsed;

            currentStage = ExportStage.LoadingSchema;
            stageStopwatch.Restart();
            ReportProgress(progress, ExportStage.LoadingSchema, "正在連線資料庫並載入物件清單...", 10);

            IReadOnlyList<DatabaseObjectSchema> allObjects;
            try {
                allObjects = await providerFactory.LoadObjectsAsync(
                    connection.DatabaseType,
                    connectionString,
                    cancellationToken
                ).ConfigureAwait(false);
            } catch (Exception ex) when (ex is DbException or TimeoutException or InvalidOperationException or NotSupportedException) {
                throw new ExportConnectionException(
                    $"無法載入「{connection.Name}」的資料庫結構。請確認連線字串、資料庫權限與資料庫類型設定。",
                    ex
                );
            }

            cancellationToken.ThrowIfCancellationRequested();

            currentStage = ExportStage.ApplyingFilters;
            ReportProgress(progress, ExportStage.ApplyingFilters, "正在套用匯出設定檔篩選條件...", 20);
            List<DatabaseObjectSchema> filteredObjects = FilterObjects(allObjects, profile, diagnostics);
            cancellationToken.ThrowIfCancellationRequested();

            currentStage = ExportStage.LoadingSchema;
            ReportProgress(progress, ExportStage.LoadingSchema, "正在載入欄位、索引與程序明細...", 30);

            DatabaseSchemaDetails schemaDetails;
            try {
                schemaDetails = await providerFactory.LoadDetailsAsync(
                    connection.DatabaseType,
                    connectionString,
                    filteredObjects,
                    cancellationToken
                ).ConfigureAwait(false);
            } catch (Exception ex) when (ex is DbException or TimeoutException or InvalidOperationException or NotSupportedException) {
                throw new ExportConnectionException(
                    $"無法載入「{connection.Name}」的明細資料。請確認連線字串、資料庫權限與資料庫類型設定。",
                    ex
                );
            }

            cancellationToken.ThrowIfCancellationRequested();
            executionSummary.SchemaLoadDuration = stageStopwatch.Elapsed;

            stageStopwatch.Restart();
            ReportProgress(progress, ExportStage.ApplyingFilters, "正在篩選程序與函數...", 40);
            FilteredSchemaExport filteredExport = BuildFilteredExport(filteredObjects, schemaDetails, profile, diagnostics);
            cancellationToken.ThrowIfCancellationRequested();
            executionSummary.FilteringDuration = stageStopwatch.Elapsed;

            OutputPlan outputPlan = BuildOutputPlan(connection.Name, normalizedExportPath, resultOptions, diagnostics);
            outputFilePath = outputPlan.FilePath;

            currentStage = ExportStage.GeneratingExport;
            stageStopwatch.Restart();
            ReportProgress(progress, ExportStage.GeneratingExport, "正在產生 Excel 檔案...", 45);
            await BuildExportFileAsync(outputPlan.FilePath, filteredExport, diagnostics, progress, cancellationToken)
                .ConfigureAwait(false);
            executionSummary.WorkbookDuration = stageStopwatch.Elapsed;

            currentStage = ExportStage.Finalizing;
            stageStopwatch.Restart();
            ReportProgress(progress, ExportStage.Finalizing, "正在整理匯出結果...", 92);

            ArtifactOutputs artifactOutputs = await SchemaExportArtifactWriter.WriteArtifactsAsync(
                outputPlan.FilePath,
                connection,
                profile,
                filteredExport,
                diagnostics,
                resultOptions,
                cancellationToken
            ).ConfigureAwait(false);

            if (resultOptions.OpenOutputFolder) {
                TryOpenOutputFolder(Path.GetDirectoryName(outputPlan.FilePath), diagnostics);
            }

            executionSummary.ArtifactDuration = stageStopwatch.Elapsed;
            executionSummary.TotalDuration = totalStopwatch.Elapsed;
            RegisterExecutionDiagnostic(filteredExport, diagnostics, executionSummary);

            int warningCount = diagnostics.Count(x => x.Severity == DiagnosticSeverity.Warning);
            LogExportCompleted(
                logger,
                connection.Name,
                executionSummary.TotalDuration.TotalMilliseconds,
                filteredExport.Objects.Count,
                filteredExport.Columns.Count,
                filteredExport.Indexes.Count,
                filteredExport.Routines.Count,
                warningCount,
                outputPlan.FilePath,
                executionSummary.ValidationDuration.TotalMilliseconds,
                executionSummary.SchemaLoadDuration.TotalMilliseconds,
                executionSummary.FilteringDuration.TotalMilliseconds,
                executionSummary.WorkbookDuration.TotalMilliseconds,
                executionSummary.ArtifactDuration.TotalMilliseconds
            );

            ReportProgress(progress, ExportStage.Completed, "匯出完成", 100);

            return new ExportResult {
                OutputFilePath = outputPlan.FilePath,
                ManifestFilePath = artifactOutputs.ManifestFilePath,
                JsonSidecarFilePath = artifactOutputs.JsonSidecarFilePath,
                MarkdownSidecarFilePath = artifactOutputs.MarkdownSidecarFilePath,
                SnapshotFilePath = artifactOutputs.SnapshotFilePath,
                DiffFilePath = artifactOutputs.DiffFilePath,
                ConnectionName = connection.Name,
                ProfileName = profile.Name,
                Diagnostics = [.. diagnostics]
            };
        } catch (OperationCanceledException) {
            LogExportCancelled(logger, connection.Name, currentStage, totalStopwatch.Elapsed.TotalMilliseconds);
            throw;
        } catch (Exception ex) {
            LogExportFailed(
                logger,
                ex,
                connection.Name,
                currentStage,
                totalStopwatch.Elapsed.TotalMilliseconds,
                profile.Name,
                connection.DatabaseType,
                diagnostics.Count(x => x.Severity == DiagnosticSeverity.Warning),
                diagnostics.Count,
                outputFilePath ?? "(not created)"
            );
            throw;
        } finally {
            totalStopwatch.Stop();
        }
    }

    private static string ValidateConnection(SchemaConnection connection) {
        if (string.IsNullOrWhiteSpace(connection.Name)) {
            throw new ExportValidationException("請先設定連線名稱。");
        }

        if (string.IsNullOrWhiteSpace(connection.ConnectionString)) {
            throw new ExportValidationException($"連線「{connection.Name}」缺少 ConnectionString 設定。");
        }

        return connection.ConnectionString.Trim();
    }

    private static string ValidateAndPrepareExportPath(string exportPath) {
        if (string.IsNullOrWhiteSpace(exportPath)) {
            throw new ExportValidationException("請輸入匯出資料夾路徑。");
        }

        string trimmedPath = exportPath.Trim();
        if (!Path.IsPathFullyQualified(trimmedPath)) {
            throw new ExportValidationException($"匯出資料夾必須使用絕對路徑：{trimmedPath}");
        }

        try {
            string normalizedPath = Path.GetFullPath(trimmedPath);
            Directory.CreateDirectory(normalizedPath);
            return normalizedPath;
        } catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) {
            throw new ExportValidationException($"匯出資料夾路徑格式無效：{trimmedPath}", ex);
        } catch (Exception ex) when (ex is UnauthorizedAccessException or IOException) {
            throw new ExportOutputException($"無法建立或存取匯出資料夾：{trimmedPath}", ex);
        }
    }

    private static void ValidateResultOptions(ExportResultOptions resultOptions) {
        if (resultOptions.UseTimestamp) {
            if (string.IsNullOrWhiteSpace(resultOptions.TimestampFormat)) {
                throw new ExportValidationException("啟用時間戳記時，必須提供 TimestampFormat。");
            }

            try {
                _ = DateTimeOffset.Now.ToString(resultOptions.TimestampFormat, CultureInfo.InvariantCulture);
            } catch (FormatException ex) {
                throw new ExportValidationException($"TimestampFormat 無效：{resultOptions.TimestampFormat}", ex);
            }
        }

        if (string.IsNullOrWhiteSpace(resultOptions.DiffSourceSnapshotPath)) {
            return;
        }

        string trimmedPath = resultOptions.DiffSourceSnapshotPath.Trim();
        if (!Path.IsPathFullyQualified(trimmedPath)) {
            throw new ExportValidationException($"差異比對快照檔必須使用絕對路徑：{trimmedPath}");
        }

        string normalizedPath;
        try {
            normalizedPath = Path.GetFullPath(trimmedPath);
        } catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) {
            throw new ExportValidationException($"差異比對快照檔路徑格式無效：{trimmedPath}", ex);
        }

        if (!File.Exists(normalizedPath)) {
            throw new ExportValidationException($"找不到差異比對快照檔：{normalizedPath}");
        }
    }

    private static List<DatabaseObjectSchema> FilterObjects(
        IReadOnlyList<DatabaseObjectSchema> allObjects,
        ExportProfile profile,
        List<ExportDiagnostic> diagnostics
    ) {
        List<DatabaseObjectSchema> orderedObjects = allObjects
            .OrderBy(x => x.SchemaName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.ObjectType, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.ObjectName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        List<DatabaseObjectSchema> filteredObjects = orderedObjects.Where(x => ShouldIncludeObject(x, profile)).ToList();
        int excludedObjectCount = orderedObjects.Count - filteredObjects.Count;
        if (excludedObjectCount > 0) {
            diagnostics.Add(new ExportDiagnostic {
                Severity = DiagnosticSeverity.Info,
                Category = ExportDiagnosticCategory.Filtering,
                Message = $"依匯出設定檔共排除 {excludedObjectCount} 個物件。"
            });
        }

        if (!profile.IncludeViews) {
            int excludedViewCount = filteredObjects.Count(x => IsViewObjectType(x.ObjectType));
            if (excludedViewCount > 0) {
                diagnostics.Add(new ExportDiagnostic {
                    Severity = DiagnosticSeverity.Info,
                    Category = ExportDiagnosticCategory.Filtering,
                    Message = $"依匯出設定檔排除 {excludedViewCount} 個檢視表。"
                });
            }

            filteredObjects = filteredObjects.Where(x => !IsViewObjectType(x.ObjectType)).ToList();
        }

        return filteredObjects;
    }

    private static FilteredSchemaExport BuildFilteredExport(
        List<DatabaseObjectSchema> filteredObjects,
        DatabaseSchemaDetails schemaDetails,
        ExportProfile profile,
        List<ExportDiagnostic> diagnostics
    ) {
        List<DatabaseRoutineSchema> orderedRoutines = schemaDetails.Routines
            .OrderBy(x => x.SchemaName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.ContainerName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.RoutineName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.RoutineType, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.OverloadIdentifier, StringComparer.OrdinalIgnoreCase)
            .ToList();

        List<DatabaseRoutineSchema> filteredRoutines = orderedRoutines.Where(x => ShouldIncludeRoutine(x, profile)).ToList();
        int excludedRoutineCount = orderedRoutines.Count - filteredRoutines.Count;
        if (excludedRoutineCount > 0) {
            diagnostics.Add(new ExportDiagnostic {
                Severity = DiagnosticSeverity.Info,
                Category = ExportDiagnosticCategory.Filtering,
                Message = $"依匯出設定檔共排除 {excludedRoutineCount} 個程序或函數。"
            });
        }

        if (filteredObjects.Count == 0 && filteredRoutines.Count == 0) {
            throw new ExportValidationException("目前的匯出設定沒有任何可匯出的資料表、檢視表或程序/函數。請調整篩選條件後再試一次。");
        }

        List<DatabaseColumnSchema> filteredColumns = schemaDetails.Columns
            .OrderBy(x => x.SchemaName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.ObjectName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.ColumnOrder)
            .ToList();

        List<DatabaseIndexSchema> filteredIndexes = schemaDetails.Indexes
            .OrderBy(x => x.SchemaName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.ObjectName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.IndexName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (DatabaseObjectSchema viewObject in filteredObjects.Where(x => IsViewObjectType(x.ObjectType))) {
            diagnostics.Add(new ExportDiagnostic {
                Severity = DiagnosticSeverity.Warning,
                Category = ExportDiagnosticCategory.ViewSupport,
                SupportLevel = ExportSupportLevel.Partial,
                Message = "檢視表目前僅匯出物件與欄位中繼資料，不包含定義 SQL、相依性與索引/主鍵明細。",
                AffectedObject = $"{viewObject.SchemaName}.{viewObject.ObjectName}"
            });
        }

        int routinesWithoutDefinition = filteredRoutines.Count(x => string.IsNullOrWhiteSpace(x.RoutineDefinition));
        if (routinesWithoutDefinition > 0) {
            diagnostics.Add(new ExportDiagnostic {
                Severity = DiagnosticSeverity.Warning,
                Category = ExportDiagnosticCategory.RoutineSupport,
                SupportLevel = ExportSupportLevel.Partial,
                Message = $"共 {routinesWithoutDefinition} 個程序或函數未取得定義內容，可能因權限不足、物件加密或資料庫限制而無法完整文件化。"
            });
        }

        return new FilteredSchemaExport {
            Objects = filteredObjects,
            Columns = filteredColumns,
            Indexes = filteredIndexes,
            Routines = filteredRoutines
        };
    }

    private static bool ShouldIncludeObject(DatabaseObjectSchema databaseObject, ExportProfile profile) {
        if (!IsMatch(databaseObject.SchemaName, profile.IncludeSchemas, profile.ExcludeSchemas)) {
            return false;
        }

        return IsMatchObjectName(databaseObject, profile.IncludeObjects, profile.ExcludeObjects);
    }

    private static bool ShouldIncludeRoutine(DatabaseRoutineSchema routine, ExportProfile profile) {
        if (!IsMatch(routine.SchemaName, profile.IncludeSchemas, profile.ExcludeSchemas)) {
            return false;
        }

        return IsMatchRoutineName(routine, profile.IncludeObjects, profile.ExcludeObjects);
    }

    private static bool IsMatchObjectName(DatabaseObjectSchema databaseObject, IReadOnlyCollection<string> includePatterns, IReadOnlyCollection<string> excludePatterns) {
        string qualifiedName = $"{databaseObject.SchemaName}.{databaseObject.ObjectName}";
        bool isIncluded = includePatterns.Count == 0 || includePatterns.Any(pattern => MatchesObjectPattern(databaseObject.ObjectName, qualifiedName, pattern));
        if (!isIncluded || excludePatterns.Count == 0) {
            return isIncluded;
        }

        return !excludePatterns.Any(pattern => MatchesObjectPattern(databaseObject.ObjectName, qualifiedName, pattern));
    }

    private static bool IsMatchRoutineName(DatabaseRoutineSchema routine, IReadOnlyCollection<string> includePatterns, IReadOnlyCollection<string> excludePatterns) {
        string schemaQualifiedName = routine.QualifiedName;
        string packageQualifiedName = string.IsNullOrWhiteSpace(routine.ContainerName) ? "" : $"{routine.ContainerName}.{routine.RoutineName}";
        bool isIncluded = includePatterns.Count == 0 || includePatterns.Any(pattern => MatchesRoutinePattern(routine.RoutineName, packageQualifiedName, schemaQualifiedName, pattern));
        if (!isIncluded || excludePatterns.Count == 0) {
            return isIncluded;
        }

        return !excludePatterns.Any(pattern => MatchesRoutinePattern(routine.RoutineName, packageQualifiedName, schemaQualifiedName, pattern));
    }

    private static bool MatchesObjectPattern(string objectName, string qualifiedName, string pattern) {
        return pattern.Contains('.', StringComparison.Ordinal)
            ? MatchesPattern(qualifiedName, pattern)
            : MatchesPattern(objectName, pattern);
    }

    private static bool MatchesRoutinePattern(string routineName, string packageQualifiedName, string schemaQualifiedName, string pattern) {
        if (pattern.Contains('.', StringComparison.Ordinal)) {
            return MatchesPattern(schemaQualifiedName, pattern) || (!string.IsNullOrWhiteSpace(packageQualifiedName) && MatchesPattern(packageQualifiedName, pattern));
        }

        return MatchesPattern(routineName, pattern) || (!string.IsNullOrWhiteSpace(packageQualifiedName) && MatchesPattern(packageQualifiedName, pattern));
    }

    private static bool IsMatch(string value, IReadOnlyCollection<string> includePatterns, IReadOnlyCollection<string> excludePatterns) {
        bool isIncluded = includePatterns.Count == 0 || includePatterns.Any(pattern => MatchesPattern(value, pattern));
        if (!isIncluded || excludePatterns.Count == 0) {
            return isIncluded;
        }

        return !excludePatterns.Any(pattern => MatchesPattern(value, pattern));
    }

    private static bool MatchesPattern(string value, string pattern) {
        if (string.IsNullOrWhiteSpace(pattern)) {
            return false;
        }

        string regexPattern = "^" + Regex.Escape(pattern.Trim()).Replace("\\*", ".*", StringComparison.Ordinal).Replace("\\?", ".", StringComparison.Ordinal) + "$";
        return Regex.IsMatch(value, regexPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static OutputPlan BuildOutputPlan(string connectionName, string exportPath, ExportResultOptions resultOptions, List<ExportDiagnostic> diagnostics) {
        string normalizedConnectionName = SanitizeFileNameSegment(connectionName);
        if (!string.Equals(normalizedConnectionName, connectionName, StringComparison.Ordinal)) {
            diagnostics.Add(new ExportDiagnostic {
                Severity = DiagnosticSeverity.Info,
                Category = ExportDiagnosticCategory.Naming,
                Message = $"輸出檔名已依 Windows 檔名限制調整為「{normalizedConnectionName}」。"
            });
        }

        SpreadsheetDocument document = SpreadsheetManager.CreateDocument();
        string basicFileName = $"TableSchema_{normalizedConnectionName}";
        if (resultOptions.UseTimestamp) {
            string timestamp = DateTimeOffset.Now.ToString(resultOptions.TimestampFormat, CultureInfo.InvariantCulture);
            basicFileName = $"{basicFileName}_{timestamp}";
        }

        string filePath = Path.Combine(exportPath, $"{basicFileName}{document.FileNameExtension}");
        string resolvedFilePath = ResolveOutputFilePath(filePath, resultOptions.OverwriteStrategy, diagnostics);
        return new OutputPlan(resolvedFilePath);
    }

    private static string ResolveOutputFilePath(string filePath, OverwriteStrategy overwriteStrategy, List<ExportDiagnostic> diagnostics) {
        if (!File.Exists(filePath)) {
            return filePath;
        }

        return overwriteStrategy switch {
            OverwriteStrategy.Overwrite => RegisterOverwrite(filePath, diagnostics),
            OverwriteStrategy.AppendSuffix => RegisterAppendedSuffix(filePath, diagnostics),
            OverwriteStrategy.Fail => throw new ExportOutputException($"輸出檔案已存在：{filePath}"),
            _ => throw new ExportOutputException($"不支援的覆寫策略：{overwriteStrategy}")
        };
    }

    private static string RegisterOverwrite(string filePath, List<ExportDiagnostic> diagnostics) {
        diagnostics.Add(new ExportDiagnostic {
            Severity = DiagnosticSeverity.Info,
            Category = ExportDiagnosticCategory.Naming,
            Message = $"輸出檔案已存在，將直接覆寫：{Path.GetFileName(filePath)}"
        });
        return filePath;
    }

    private static string RegisterAppendedSuffix(string filePath, List<ExportDiagnostic> diagnostics) {
        string resolvedFilePath = GenerateUniqueFilePath(filePath);
        diagnostics.Add(new ExportDiagnostic {
            Severity = DiagnosticSeverity.Info,
            Category = ExportDiagnosticCategory.Naming,
            Message = $"輸出檔案已存在，已改用檔名：{Path.GetFileName(resolvedFilePath)}"
        });
        return resolvedFilePath;
    }

    private static string GenerateUniqueFilePath(string filePath) {
        string? directoryPath = Path.GetDirectoryName(filePath);
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
        string extension = Path.GetExtension(filePath);
        for (int suffixIndex = 1; ; suffixIndex++) {
            string candidateFilePath = Path.Combine(directoryPath ?? "", $"{fileNameWithoutExtension}_{suffixIndex}{extension}");
            if (!File.Exists(candidateFilePath)) {
                return candidateFilePath;
            }
        }
    }

    private static async Task BuildExportFileAsync(string filePath, FilteredSchemaExport filteredExport, List<ExportDiagnostic> diagnostics, IProgress<ExportProgress>? progress, CancellationToken cancellationToken) {
        try {
            await Task.Run(() => {
                cancellationToken.ThrowIfCancellationRequested();
                Dictionary<DatabaseObjectKey, string> sheetNames = BuildSheetNames(filteredExport.Objects, diagnostics);
                SpreadsheetDocument document = SpreadsheetManager.CreateDocument();
                BuildTableListSheet(document, filteredExport.Objects);
                ReportProgress(progress, ExportStage.GeneratingExport, "正在建立資料表清單工作表...", 52);
                cancellationToken.ThrowIfCancellationRequested();
                BuildColumnListSheet(document, filteredExport.Columns);
                ReportProgress(progress, ExportStage.GeneratingExport, "正在建立欄位清單工作表...", 58);
                cancellationToken.ThrowIfCancellationRequested();
                BuildRoutineListSheet(document, filteredExport.Routines);
                ReportProgress(progress, ExportStage.GeneratingExport, "正在建立程序與函數清單工作表...", 64);
                cancellationToken.ThrowIfCancellationRequested();
                if (diagnostics.Count > 0) {
                    BuildDiagnosticsSheet(document, diagnostics);
                }

                BuildTableDetailSheets(document, filteredExport.Objects, filteredExport.Columns, filteredExport.Indexes, sheetNames, progress, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                ReportProgress(progress, ExportStage.GeneratingExport, "正在寫入 Excel 檔案...", 88);
                document.ExportFile(filePath, SpreadsheetFileMode.Create);
            }, cancellationToken).ConfigureAwait(false);
        } catch (OperationCanceledException) {
            throw;
        } catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException or PathTooLongException) {
            throw new ExportOutputException($"無法寫入輸出檔案：{filePath}", ex);
        }
    }

    private static void TryOpenOutputFolder(string? outputDirectoryPath, List<ExportDiagnostic> diagnostics) {
        if (string.IsNullOrWhiteSpace(outputDirectoryPath)) {
            return;
        }

        try {
            Process.Start(new ProcessStartInfo { FileName = outputDirectoryPath, UseShellExecute = true });
        } catch (Exception ex) when (ex is InvalidOperationException or Win32Exception) {
            diagnostics.Add(new ExportDiagnostic {
                Severity = DiagnosticSeverity.Warning,
                Category = ExportDiagnosticCategory.General,
                Message = $"匯出完成，但無法自動開啟輸出資料夾：{ex.Message}"
            });
        }
    }

    private static void RegisterExecutionDiagnostic(FilteredSchemaExport filteredExport, List<ExportDiagnostic> diagnostics, ExportExecutionSummary executionSummary) {
        int warningCount = diagnostics.Count(x => x.Severity == DiagnosticSeverity.Warning);
        diagnostics.Add(new ExportDiagnostic {
            Severity = DiagnosticSeverity.Info,
            Category = ExportDiagnosticCategory.Execution,
            Message = string.Create(CultureInfo.InvariantCulture, $"匯出摘要：耗時 {executionSummary.TotalDuration.TotalMilliseconds:N0} ms；物件 {filteredExport.Objects.Count}、欄位 {filteredExport.Columns.Count}、索引 {filteredExport.Indexes.Count}、程序/函數 {filteredExport.Routines.Count}；警告 {warningCount}。各階段耗時：驗證 {executionSummary.ValidationDuration.TotalMilliseconds:N0} ms、載入 {executionSummary.SchemaLoadDuration.TotalMilliseconds:N0} ms、篩選 {executionSummary.FilteringDuration.TotalMilliseconds:N0} ms、Excel {executionSummary.WorkbookDuration.TotalMilliseconds:N0} ms、收尾 {executionSummary.ArtifactDuration.TotalMilliseconds:N0} ms。")
        });
    }

    private static void ReportProgress(IProgress<ExportProgress>? progress, ExportStage stage, string message, int? percentComplete) {
        progress?.Report(new ExportProgress { Stage = stage, Message = message, PercentComplete = percentComplete });
    }

    [LoggerMessage(EventId = 2000, Level = LogLevel.Information, Message = "Starting schema export for connection {connectionName} ({databaseType}) using profile {profileName}.")]
    private static partial void LogExportStarted(ILogger logger, string connectionName, DatabaseType databaseType, string profileName);

    [LoggerMessage(EventId = 2001, Level = LogLevel.Information, Message = "Schema export completed for {connectionName} in {elapsedMilliseconds} ms. Objects={objectCount}, Columns={columnCount}, Indexes={indexCount}, Routines={routineCount}, Warnings={warningCount}, Output={outputFilePath}. Validation={validationMilliseconds} ms, Load={loadMilliseconds} ms, Filter={filterMilliseconds} ms, Workbook={workbookMilliseconds} ms, Finalize={finalizeMilliseconds} ms.")]
    private static partial void LogExportCompleted(ILogger logger, string connectionName, double elapsedMilliseconds, int objectCount, int columnCount, int indexCount, int routineCount, int warningCount, string outputFilePath, double validationMilliseconds, double loadMilliseconds, double filterMilliseconds, double workbookMilliseconds, double finalizeMilliseconds);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Warning, Message = "Schema export for {connectionName} was cancelled during {stage} after {elapsedMilliseconds} ms.")]
    private static partial void LogExportCancelled(ILogger logger, string connectionName, ExportStage stage, double elapsedMilliseconds);

    [LoggerMessage(EventId = 2003, Level = LogLevel.Error, Message = "Schema export failed for {connectionName} during {stage} after {elapsedMilliseconds} ms. Profile={profileName}, DatabaseType={databaseType}, WarningCount={warningCount}, DiagnosticCount={diagnosticCount}, Output={outputFilePath}.")]
    private static partial void LogExportFailed(ILogger logger, Exception exception, string connectionName, ExportStage stage, double elapsedMilliseconds, string profileName, DatabaseType databaseType, int warningCount, int diagnosticCount, string outputFilePath);

    private static void BuildTableListSheet(SpreadsheetDocument document, IReadOnlyCollection<DatabaseObjectSchema> databaseObjects) {
        CellStyle itemStyle = SpreadsheetManager.DefaultCellStyles.FieldStyle with { HorizontalAlignment = SpreadsheetExporter.HorizontalAlignment.Left };
        RecordSetTemplate<DatabaseObjectSchema> template = new(databaseObjects) { RecordHeight = Constants.AutoFitRowHeight };
        template.Columns.Add("Schema", x => x.SchemaName);
        template.Columns.Add("名稱", x => x.ObjectName, fieldStyleGenerator: _ => itemStyle);
        template.Columns.Add("類型", x => x.ObjectType, fieldStyleGenerator: _ => itemStyle);
        template.Columns.Add("描述", x => x.ObjectDescription, fieldStyleGenerator: _ => itemStyle);
        SheetDefinition sheet = document.CreateSheet("資料表清單");
        sheet.AddTemplate(template);
        sheet.SetColumnWidth(0, 14D);
        sheet.SetColumnWidth(1, 40D);
        sheet.SetColumnWidth(2, 15D);
        sheet.SetColumnWidth(3, 50D);
    }

    private static void BuildColumnListSheet(SpreadsheetDocument document, IReadOnlyCollection<DatabaseColumnSchema> columns) {
        CellStyle centerFieldStyle = SpreadsheetManager.DefaultCellStyles.FieldStyle with { HorizontalAlignment = SpreadsheetExporter.HorizontalAlignment.Center };
        RecordSetTemplate<DatabaseColumnSchema> template = new(columns) { RecordHeight = Constants.AutoFitRowHeight };
        template.Columns.Add("Schema", x => x.SchemaName);
        template.Columns.Add("物件名稱", x => x.ObjectName);
        template.Columns.Add("欄位名稱", x => x.ColumnName);
        template.Columns.Add("欄位型別", x => x.ColumnType);
        template.Columns.Add("預設值", x => x.ColumnDefault);
        template.Columns.Add("是否允許 Null", x => x.IsNullable, fieldStyleGenerator: _ => centerFieldStyle);
        template.Columns.Add("是否為 PK", x => x.IsPrimaryKey, fieldStyleGenerator: _ => centerFieldStyle);
        template.Columns.Add("是否為 Identity", x => x.IsIdentity, fieldStyleGenerator: _ => centerFieldStyle);
        template.Columns.Add("描述", x => x.ColumnDescription);
        SheetDefinition sheet = document.CreateSheet("資料表欄位清單");
        sheet.AddTemplate(template);
        sheet.SetColumnWidth(0, 14D);
        sheet.SetColumnWidth(1, 40D);
        sheet.SetColumnWidth(2, 30D);
        sheet.SetColumnWidth(3, 30D);
        sheet.SetColumnWidth(4, 15D);
        sheet.SetColumnWidth(5, 15D);
        sheet.SetColumnWidth(6, 15D);
        sheet.SetColumnWidth(7, 15D);
        sheet.SetColumnWidth(8, 50D);
    }

    private static void BuildRoutineListSheet(SpreadsheetDocument document, IReadOnlyCollection<DatabaseRoutineSchema> routines) {
        CellStyle itemStyle = SpreadsheetManager.DefaultCellStyles.FieldStyle with { HorizontalAlignment = SpreadsheetExporter.HorizontalAlignment.Left };
        RecordSetTemplate<DatabaseRoutineSchema> template = new(routines) { RecordHeight = Constants.AutoFitRowHeight };
        template.Columns.Add("Schema", x => x.SchemaName);
        template.Columns.Add("容器", x => x.ContainerName, fieldStyleGenerator: _ => itemStyle);
        template.Columns.Add("名稱", x => x.RoutineName, fieldStyleGenerator: _ => itemStyle);
        template.Columns.Add("類型", x => x.RoutineType, fieldStyleGenerator: _ => itemStyle);
        template.Columns.Add("Overload", x => x.OverloadIdentifier, fieldStyleGenerator: _ => itemStyle);
        template.Columns.Add("參數", x => x.ParameterSignature, x => x.UseValue(static value => NormalizeMultilineText(value.Value)));
        template.Columns.Add("回傳型別", x => x.ReturnType, fieldStyleGenerator: _ => itemStyle);
        template.Columns.Add("描述", x => x.RoutineDescription, fieldStyleGenerator: _ => itemStyle);
        template.Columns.Add("定義", x => x.RoutineDefinition, x => x.UseValue(static value => NormalizeMultilineText(value.Value)));
        SheetDefinition sheet = document.CreateSheet("程序與函數清單");
        sheet.AddTemplate(template);
        sheet.SetColumnWidth(0, 14D);
        sheet.SetColumnWidth(1, 20D);
        sheet.SetColumnWidth(2, 30D);
        sheet.SetColumnWidth(3, 12D);
        sheet.SetColumnWidth(4, 12D);
        sheet.SetColumnWidth(5, 45D);
        sheet.SetColumnWidth(6, 18D);
        sheet.SetColumnWidth(7, 40D);
        sheet.SetColumnWidth(8, 90D);
    }

    private static void BuildDiagnosticsSheet(SpreadsheetDocument document, IReadOnlyCollection<ExportDiagnostic> diagnostics) {
        RecordSetTemplate<ExportDiagnostic> template = new(diagnostics) { RecordHeight = Constants.AutoFitRowHeight };
        template.Columns.Add("嚴重性", x => x.SeverityText);
        template.Columns.Add("類別", x => x.CategoryText);
        template.Columns.Add("支援層級", x => x.SupportLevelText);
        template.Columns.Add("影響物件", x => x.AffectedObjectDisplay);
        template.Columns.Add("訊息", x => x.Message);
        SheetDefinition sheet = document.CreateSheet("匯出診斷");
        sheet.AddTemplate(template);
        sheet.SetColumnWidth(0, 12D);
        sheet.SetColumnWidth(1, 18D);
        sheet.SetColumnWidth(2, 15D);
        sheet.SetColumnWidth(3, 35D);
        sheet.SetColumnWidth(4, 80D);
    }

    private static void BuildTableDetailSheets(SpreadsheetDocument document, IReadOnlyList<DatabaseObjectSchema> databaseObjects, IReadOnlyList<DatabaseColumnSchema> columns, IReadOnlyList<DatabaseIndexSchema> indexes, IReadOnlyDictionary<DatabaseObjectKey, string> sheetNames, IProgress<ExportProgress>? progress, CancellationToken cancellationToken) {
        ILookup<DatabaseObjectKey, DatabaseColumnSchema> columnsByObject = columns.ToLookup(x => x.ObjectKey);
        ILookup<DatabaseObjectKey, DatabaseIndexSchema> indexesByObject = indexes.ToLookup(x => x.ObjectKey);
        int totalObjects = Math.Max(1, databaseObjects.Count);
        for (int index = 0; index < databaseObjects.Count; index++) {
            cancellationToken.ThrowIfCancellationRequested();
            DatabaseObjectSchema databaseObject = databaseObjects[index];
            DatabaseObjectKey objectKey = databaseObject.ObjectKey;
            SheetDefinition sheet = document.CreateSheet(sheetNames[objectKey]);
            BuildTableDetailSheet(
                sheet, databaseObject,
                columnsByObject[objectKey].OrderBy(x => x.ColumnOrder).ToList(),
                indexesByObject[objectKey].OrderBy(x => x.IndexName, StringComparer.OrdinalIgnoreCase).ToList()
            );
            int percent = 68 + (int)Math.Round((double)(index + 1) / totalObjects * 17, MidpointRounding.AwayFromZero);
            ReportProgress(
                progress, ExportStage.GeneratingExport,
                $"正在建立工作表：{databaseObject.SchemaName}.{databaseObject.ObjectName} ({index + 1}/{databaseObjects.Count})",
                percent
            );
        }
    }

    private static void BuildTableDetailSheet(SheetDefinition sheet, DatabaseObjectSchema databaseObject, IReadOnlyCollection<DatabaseColumnSchema> columns, IReadOnlyCollection<DatabaseIndexSchema> indexes) {
        CellStyle defaultGridStyle = SpreadsheetManager.DefaultCellStyles.GridCellStyle;
        CellFont defaultFont = SpreadsheetManager.DefaultCellStyles.GridCellStyle.Font;
        CellStyle headerLabelStyle = defaultGridStyle with { HorizontalAlignment = HorizontalAlignment.Right, Font = defaultFont with { Style = defaultFont.Style | SpreadsheetExporter.FontStyles.Bold } };
        GridTemplate headerTemplate = new();
        headerTemplate.CreateRow().CreateCell("Schema：", cellStyle: headerLabelStyle).CreateCell(databaseObject.SchemaName, 2).CreateCell("物件名稱：", cellStyle: headerLabelStyle).CreateCell(databaseObject.ObjectName, 3).CreateRow(Constants.AutoFitRowHeight).CreateCell("類型：", cellStyle: headerLabelStyle).CreateCell(databaseObject.ObjectType, 2).CreateCell("資料表描述：", cellStyle: headerLabelStyle).CreateCell(databaseObject.ObjectDescription, 3);
        sheet.AddTemplate(headerTemplate);
        CellStyle centerFieldStyle = SpreadsheetManager.DefaultCellStyles.FieldStyle with { HorizontalAlignment = SpreadsheetExporter.HorizontalAlignment.Center };
        RecordSetTemplate<DatabaseColumnSchema> columnsTemplate = new(columns);
        columnsTemplate.Columns.Add("欄位名稱", x => x.ColumnName);
        columnsTemplate.Columns.Add("欄位型別", x => x.ColumnType);
        columnsTemplate.Columns.Add("預設值", x => x.ColumnDefault);
        columnsTemplate.Columns.Add("是否允許 Null", x => x.IsNullable, fieldStyleGenerator: _ => centerFieldStyle);
        columnsTemplate.Columns.Add("是否為 PK", x => x.IsPrimaryKey, fieldStyleGenerator: _ => centerFieldStyle);
        columnsTemplate.Columns.Add("是否為 Identity", x => x.IsIdentity, fieldStyleGenerator: _ => centerFieldStyle);
        columnsTemplate.Columns.Add("描述", x => x.ColumnDescription);
        sheet.AddTemplate(columnsTemplate);
        if (indexes.Count > 0) {
            sheet.AddTemplate(new GridTemplate().CreateRow());
            RecordSetTemplate<DatabaseIndexSchema> indexesTemplate = new(indexes) { RecordHeight = Constants.AutoFitRowHeight };
            indexesTemplate.Columns.Add("索引名稱", x => x.IndexName);
            indexesTemplate.Columns.Add("是否為 PK", x => x.IsPrimaryKey, fieldStyleGenerator: _ => centerFieldStyle);
            indexesTemplate.Columns.Add("是否為叢集索引", x => x.IsClustered, fieldStyleGenerator: _ => centerFieldStyle);
            indexesTemplate.Columns.Add("是否為唯一索引", x => x.IsUnique, fieldStyleGenerator: _ => centerFieldStyle);
            indexesTemplate.Columns.Add("是否為外鍵", x => x.IsForeignKey, fieldStyleGenerator: _ => centerFieldStyle);
            indexesTemplate.Columns.Add("欄位", x => x.Columns, x => x.UseValue(static value => NormalizeMultilineText(value.Value)));
            indexesTemplate.Columns.Add("Include/外鍵 欄位", x => x.OtherColumns, x => x.UseValue(static value => NormalizeMultilineText(value.Value)));
            sheet.AddTemplate(indexesTemplate);
        }

        sheet.SetColumnWidth(0, 40D);
        sheet.SetColumnWidth(1, 15D);
        sheet.SetColumnWidth(2, 15D);
        sheet.SetColumnWidth(3, 15D);
        sheet.SetColumnWidth(4, 15D);
        sheet.SetColumnWidth(5, 25D);
        sheet.SetColumnWidth(6, 50D);
    }

    private static Dictionary<DatabaseObjectKey, string> BuildSheetNames(IEnumerable<DatabaseObjectSchema> databaseObjects, List<ExportDiagnostic> diagnostics) {
        Dictionary<DatabaseObjectKey, string> sheetNames = [];
        HashSet<string> usedSheetNames = new(StringComparer.OrdinalIgnoreCase) { "資料表清單", "資料表欄位清單", "程序與函數清單", "匯出診斷" };
        foreach (DatabaseObjectSchema databaseObject in databaseObjects) {
            string originalName = $"{databaseObject.SchemaName}.{databaseObject.ObjectName}";
            string sanitizedName = SanitizeSheetName(originalName);
            string sheetName = CreateUniqueSheetName(sanitizedName, usedSheetNames);
            if (!string.Equals(sheetName, originalName, StringComparison.Ordinal)) {
                diagnostics.Add(new ExportDiagnostic { Severity = DiagnosticSeverity.Info, Category = ExportDiagnosticCategory.Naming, Message = $"工作表名稱已調整為「{sheetName}」以符合 Excel 限制。", AffectedObject = originalName });
            }
            sheetNames.Add(databaseObject.ObjectKey, sheetName);
        }

        return sheetNames;
    }

    private static string CreateUniqueSheetName(string baseName, ISet<string> usedSheetNames) {
        string normalizedBaseName = string.IsNullOrWhiteSpace(baseName) ? "Sheet" : baseName;
        string candidateName = TrimSheetName(normalizedBaseName);
        if (usedSheetNames.Add(candidateName)) {
            return candidateName;
        }

        int suffixIndex = 1;
        while (true) {
            string suffix = $"~{suffixIndex}";
            string trimmedBaseName = TrimSheetName(normalizedBaseName, suffix.Length);
            candidateName = $"{trimmedBaseName}{suffix}";
            if (usedSheetNames.Add(candidateName)) {
                return candidateName;
            }
            suffixIndex++;
        }
    }

    private static string TrimSheetName(string value, int reservedSuffixLength = 0) {
        int maxLength = Math.Max(1, MaxSheetNameLength - reservedSuffixLength);
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static string SanitizeSheetName(string value) {
        StringBuilder stringBuilder = new(value.Length);
        foreach (char character in value) {
            stringBuilder.Append(InvalidSheetNameCharacters.Contains(character) ? '_' : character);
        }
        return stringBuilder.ToString().Trim();
    }

    private static string SanitizeFileNameSegment(string value) {
        StringBuilder stringBuilder = new(value.Length);
        foreach (char character in value.Trim()) {
            stringBuilder.Append(Path.GetInvalidFileNameChars().Contains(character) ? '_' : character);
        }
        string sanitized = stringBuilder.ToString().Trim().TrimEnd('.', ' ');
        return string.IsNullOrWhiteSpace(sanitized) ? "Connection" : sanitized;
    }

    private static bool IsViewObjectType(string objectType) {
        return string.Equals(objectType, "VIEW", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeMultilineText(string value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return value;
        }

        return value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\\n", "\n", StringComparison.Ordinal).Replace("\n", Environment.NewLine, StringComparison.Ordinal);
    }
}

