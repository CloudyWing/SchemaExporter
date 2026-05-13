using System.Text;
using System.Text.Json;
using CloudyWing.SchemaExporter.Core.Exporting.Diffs;
using CloudyWing.SchemaExporter.Core.Exporting.Snapshots;

namespace CloudyWing.SchemaExporter.Core.Exporting;

/// <summary>
/// 負責寫入次要匯出成品，包含資訊清單、附屬檔案、快照與差異比對結果。
/// </summary>
internal static class SchemaExportArtifactWriter {
    /// <summary>
    /// 依據匯出設定非同步產生所有次要成品，並回傳各成品的輸出路徑。
    /// </summary>
    /// <param name="outputFilePath">主要 Excel 輸出檔案的完整路徑。</param>
    /// <param name="connection">匯出所使用的資料庫連線設定。</param>
    /// <param name="profile">匯出設定檔。</param>
    /// <param name="filteredExport">篩選後的結構描述匯出資料。</param>
    /// <param name="diagnostics">本次匯出過程收集到的診斷訊息清單。</param>
    /// <param name="resultOptions">匯出結果選項，決定要產生哪些成品。</param>
    /// <param name="snapshotBuilder">用於建立 snapshot 文件的服務。</param>
    /// <param name="diffService">用於載入與比較 snapshot 的服務。</param>
    /// <param name="cancellationToken">取消語彙基元。</param>
    /// <returns>包含各成品輸出路徑的 <see cref="ArtifactOutputs"/> 物件。</returns>
    internal static async Task<ArtifactOutputs> WriteArtifactsAsync(
        string outputFilePath,
        SchemaConnection connection,
        ExportProfile profile,
        FilteredSchemaExport filteredExport,
        List<ExportDiagnostic> diagnostics,
        ExportResultOptions resultOptions,
        SchemaSnapshotBuilder snapshotBuilder,
        SchemaSnapshotDiffService diffService,
        CancellationToken cancellationToken
    ) {
        ArgumentNullException.ThrowIfNull(snapshotBuilder);
        ArgumentNullException.ThrowIfNull(diffService);

        string? manifestFilePath = null;
        if (resultOptions.GenerateManifest) {
            manifestFilePath = await GenerateManifestAsync(outputFilePath, connection, profile, filteredExport, diagnostics, resultOptions, cancellationToken).ConfigureAwait(false);
        }

        SchemaSnapshotDocument snapshot = snapshotBuilder.Build(outputFilePath, connection, profile, filteredExport, diagnostics);
        string? snapshotFilePath = null;
        if (resultOptions.GenerateSchemaSnapshot) {
            snapshotFilePath = BuildArtifactPath(outputFilePath, "snapshot.json");
            await WriteJsonArtifactAsync(snapshotFilePath, snapshot, cancellationToken, "無法產生 schema snapshot 檔案：").ConfigureAwait(false);
        }

        SchemaDiffDocument? diff = null;
        string? diffFilePath = null;
        if (!string.IsNullOrWhiteSpace(resultOptions.DiffSourceSnapshotPath)) {
            string normalizedDiffSourcePath = Path.GetFullPath(resultOptions.DiffSourceSnapshotPath.Trim());
            SchemaSnapshotDocument previousSnapshot = await diffService.LoadSnapshotAsync(normalizedDiffSourcePath, cancellationToken).ConfigureAwait(false);
            diff = diffService.Compare(previousSnapshot, snapshot, normalizedDiffSourcePath, snapshotFilePath ?? outputFilePath);
            diffFilePath = BuildArtifactPath(outputFilePath, "diff.json");
            await diffService.WriteJsonAsync(diffFilePath, diff, cancellationToken).ConfigureAwait(false);
        }

        string? jsonSidecarFilePath = null;
        if (resultOptions.GenerateJsonSidecar) {
            jsonSidecarFilePath = BuildArtifactPath(outputFilePath, "schema.json");
            SchemaJsonSidecar sidecar = new() { Snapshot = snapshot, Diff = diff };
            await WriteJsonArtifactAsync(jsonSidecarFilePath, sidecar, cancellationToken, "無法產生 JSON sidecar 檔案：").ConfigureAwait(false);
        }

        string? markdownSidecarFilePath = null;
        if (resultOptions.GenerateMarkdownSidecar) {
            markdownSidecarFilePath = BuildArtifactPath(outputFilePath, "schema.md");
            string markdown = BuildMarkdownSidecar(snapshot, diff);
            await WriteTextArtifactAsync(markdownSidecarFilePath, markdown, cancellationToken, "無法產生 Markdown sidecar 檔案：").ConfigureAwait(false);
        }

        string? schemaSummaryFilePath = null;
        if (resultOptions.GenerateSchemaSummary) {
            schemaSummaryFilePath = BuildArtifactPath(outputFilePath, "schema-summary.md");
            string schemaSummary = SchemaSummaryMarkdownBuilder.BuildMarkdown(snapshot, diff);
            await WriteTextArtifactAsync(
                schemaSummaryFilePath,
                schemaSummary,
                cancellationToken,
                "無法產生 Schema Summary 檔案："
            ).ConfigureAwait(false);
        }

        return new ArtifactOutputs {
            ManifestFilePath = manifestFilePath,
            JsonSidecarFilePath = jsonSidecarFilePath,
            MarkdownSidecarFilePath = markdownSidecarFilePath,
            SchemaSummaryFilePath = schemaSummaryFilePath,
            SnapshotFilePath = snapshotFilePath,
            DiffFilePath = diffFilePath
        };
    }

    private static string BuildMarkdownSidecar(SchemaSnapshotDocument snapshot, SchemaDiffDocument? diff) {
        StringBuilder markdown = new();
        markdown.AppendLine("# Schema Export Sidecar");
        markdown.AppendLine();
        markdown.AppendLine($"- Exported At: {snapshot.ExportedAt:O}");
        markdown.AppendLine($"- Connection: {EscapeMarkdown(snapshot.ConnectionName)}");
        markdown.AppendLine($"- Database Type: {EscapeMarkdown(snapshot.DatabaseType)}");
        markdown.AppendLine($"- Profile: {EscapeMarkdown(snapshot.ProfileName)}");
        markdown.AppendLine($"- Output File: {EscapeMarkdown(snapshot.OutputFilePath)}");
        markdown.AppendLine($"- Objects: {snapshot.Counts.Objects}");
        markdown.AppendLine($"- Columns: {snapshot.Counts.Columns}");
        markdown.AppendLine($"- Indexes: {snapshot.Counts.Indexes}");
        markdown.AppendLine($"- Routines: {snapshot.Counts.Routines}");
        if (diff is not null) {
            markdown.AppendLine();
            markdown.AppendLine("## Snapshot Diff");
            markdown.AppendLine();
            markdown.AppendLine($"- Left Snapshot: {EscapeMarkdown(diff.LeftSnapshotPath)}");
            markdown.AppendLine($"- Right Snapshot: {EscapeMarkdown(diff.RightSnapshotPath)}");
            markdown.AppendLine($"- Added Objects: {diff.Summary.AddedObjects}");
            markdown.AppendLine($"- Removed Objects: {diff.Summary.RemovedObjects}");
            markdown.AppendLine($"- Modified Objects: {diff.Summary.ModifiedObjects}");
            markdown.AppendLine($"- Added Columns: {diff.Summary.AddedColumns}");
            markdown.AppendLine($"- Removed Columns: {diff.Summary.RemovedColumns}");
            markdown.AppendLine($"- Modified Columns: {diff.Summary.ModifiedColumns}");
            markdown.AppendLine($"- Added Indexes: {diff.Summary.AddedIndexes}");
            markdown.AppendLine($"- Removed Indexes: {diff.Summary.RemovedIndexes}");
            markdown.AppendLine($"- Modified Indexes: {diff.Summary.ModifiedIndexes}");
            markdown.AppendLine($"- Added Routines: {diff.Summary.AddedRoutines}");
            markdown.AppendLine($"- Removed Routines: {diff.Summary.RemovedRoutines}");
            markdown.AppendLine($"- Modified Routines: {diff.Summary.ModifiedRoutines}");
            AppendDiffSection(markdown, "Object Changes", diff.ObjectChanges);
            AppendDiffSection(markdown, "Column Changes", diff.ColumnChanges);
            AppendDiffSection(markdown, "Index Changes", diff.IndexChanges);
            AppendDiffSection(markdown, "Routine Changes", diff.RoutineChanges);
        }

        if (snapshot.Diagnostics.Count > 0) {
            markdown.AppendLine();
            markdown.AppendLine("## Diagnostics");
            markdown.AppendLine();
            markdown.AppendLine("| Severity | Category | Affected Object | Message |");
            markdown.AppendLine("| --- | --- | --- | --- |");
            foreach (SchemaSnapshotDiagnostic diagnostic in snapshot.Diagnostics) {
                markdown.AppendLine(
                    $"| {EscapeMarkdown(diagnostic.Severity)} | {EscapeMarkdown(diagnostic.Category)} | {EscapeMarkdown(diagnostic.AffectedObject ?? "")} | {EscapeMarkdown(diagnostic.Message)} |"
                );
            }
        }

        markdown.AppendLine();
        markdown.AppendLine("## Objects");
        foreach (SchemaSnapshotObjectDocument databaseObject in snapshot.Objects) {
            markdown.AppendLine();
            markdown.AppendLine(
                $"### {EscapeMarkdown(databaseObject.SchemaName)}.{EscapeMarkdown(databaseObject.ObjectName)} ({EscapeMarkdown(databaseObject.ObjectType)})"
            );
            if (!string.IsNullOrWhiteSpace(databaseObject.ObjectDescription)) {
                markdown.AppendLine();
                markdown.AppendLine(EscapeMarkdown(databaseObject.ObjectDescription));
            }

            markdown.AppendLine();
            markdown.AppendLine("#### Columns");
            markdown.AppendLine();
            markdown.AppendLine("| Order | Name | Type | Nullable | PK | Identity | Default | Description |");
            markdown.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- |");
            foreach (SchemaSnapshotColumnDocument column in databaseObject.Columns) {
                markdown.AppendLine(
                    $"| {column.ColumnOrder} | {EscapeMarkdown(column.ColumnName)} | {EscapeMarkdown(column.ColumnType)} | {EscapeMarkdown(column.IsNullable)} | {EscapeMarkdown(column.IsPrimaryKey)} | {EscapeMarkdown(column.IsIdentity)} | {EscapeMarkdown(column.ColumnDefault)} | {EscapeMarkdown(column.ColumnDescription)} |"
                );
            }

            if (databaseObject.Indexes.Count > 0) {
                markdown.AppendLine();
                markdown.AppendLine("#### Indexes");
                markdown.AppendLine();
                markdown.AppendLine("| Name | PK | Clustered | Unique | Foreign Key | Columns | Other Columns |");
                markdown.AppendLine("| --- | --- | --- | --- | --- | --- | --- |");
                foreach (SchemaSnapshotIndexDocument index in databaseObject.Indexes) {
                    markdown.AppendLine(
                        $"| {EscapeMarkdown(index.IndexName)} | {EscapeMarkdown(index.IsPrimaryKey)} | {EscapeMarkdown(index.IsClustered)} | {EscapeMarkdown(index.IsUnique)} | {EscapeMarkdown(index.IsForeignKey)} | {EscapeMarkdown(index.Columns)} | {EscapeMarkdown(index.OtherColumns)} |"
                    );
                }
            }
        }

        if (snapshot.Routines.Count > 0) {
            markdown.AppendLine();
            markdown.AppendLine("## Routines");
            foreach (SchemaSnapshotRoutineDocument routine in snapshot.Routines) {
                markdown.AppendLine();
                markdown.AppendLine(
                    $"### {EscapeMarkdown(BuildRoutineIdentifier(routine.SchemaName, routine.ContainerName, routine.RoutineName, routine.RoutineType, routine.OverloadIdentifier))}"
                );
                if (!string.IsNullOrWhiteSpace(routine.ParameterSignature)) {
                    markdown.AppendLine();
                    markdown.AppendLine($"- Parameters: {EscapeMarkdown(routine.ParameterSignature)}");
                }

                if (!string.IsNullOrWhiteSpace(routine.ReturnType)) {
                    markdown.AppendLine($"- Return Type: {EscapeMarkdown(routine.ReturnType)}");
                }

                if (!string.IsNullOrWhiteSpace(routine.RoutineDescription)) {
                    markdown.AppendLine($"- Description: {EscapeMarkdown(routine.RoutineDescription)}");
                }

                if (!string.IsNullOrWhiteSpace(routine.RoutineDefinition)) {
                    markdown.AppendLine();
                    markdown.AppendLine("#### Definition");
                    markdown.AppendLine();
                    markdown.AppendLine("```sql");
                    markdown.AppendLine(routine.RoutineDefinition);
                    markdown.AppendLine("```");
                }
            }
        }

        return markdown.ToString();
    }

    private static void AppendDiffSection(StringBuilder markdown, string title, IReadOnlyCollection<SchemaDiffEntry> entries) {
        if (entries.Count == 0) {
            return;
        }

        markdown.AppendLine();
        markdown.AppendLine($"### {title}");
        markdown.AppendLine();
        foreach (SchemaDiffEntry entry in entries) {
            markdown.AppendLine($"- **{EscapeMarkdown(entry.ChangeType.ToString())}** {EscapeMarkdown(entry.Identifier)}");
            foreach ((string propertyName, SchemaValueChange change) in entry.PropertyChanges) {
                string previousValue = EscapeMarkdown(change.Previous ?? "");
                string currentValue = EscapeMarkdown(change.Current ?? "");
                markdown.AppendLine($"  - {EscapeMarkdown(propertyName)}: `{previousValue}` -> `{currentValue}`");
            }
        }
    }

    private static string BuildRoutineIdentifier(
        string schemaName,
        string containerName,
        string routineName,
        string routineType,
        string overloadIdentifier
    ) {
        string qualifiedName = string.IsNullOrWhiteSpace(containerName)
            ? $"{schemaName}.{routineName}"
            : $"{schemaName}.{containerName}.{routineName}";

        return string.IsNullOrWhiteSpace(overloadIdentifier)
            ? $"{qualifiedName} ({routineType})"
            : $"{qualifiedName}#{overloadIdentifier} ({routineType})";
    }

    private static string EscapeMarkdown(string value) {
        if (string.IsNullOrEmpty(value)) {
            return "";
        }

        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("\r\n", "<br />", StringComparison.Ordinal)
            .Replace("\n", "<br />", StringComparison.Ordinal)
            .Replace("\r", "", StringComparison.Ordinal);
    }

    private static string BuildArtifactPath(string outputFilePath, string suffix) {
        string? directoryPath = Path.GetDirectoryName(outputFilePath);
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(outputFilePath);
        return Path.Combine(directoryPath ?? "", $"{fileNameWithoutExtension}.{suffix}");
    }

    private static async Task WriteJsonArtifactAsync<T>(
        string filePath,
        T document,
        CancellationToken cancellationToken,
        string errorMessagePrefix
    ) {
        try {
            string json = JsonSerializer.Serialize(document, SchemaArtifactJsonSerializerOptions.Default);
            await File.WriteAllTextAsync(filePath, json, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        } catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException or PathTooLongException) {
            throw new ExportOutputException($"{errorMessagePrefix}{filePath}", ex);
        }
    }

    private static async Task WriteTextArtifactAsync(
        string filePath,
        string content,
        CancellationToken cancellationToken,
        string errorMessagePrefix
    ) {
        try {
            await File.WriteAllTextAsync(filePath, content, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        } catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException or PathTooLongException) {
            throw new ExportOutputException($"{errorMessagePrefix}{filePath}", ex);
        }
    }

    private static async Task<string> GenerateManifestAsync(
        string outputFilePath,
        SchemaConnection connection,
        ExportProfile profile,
        FilteredSchemaExport filteredExport,
        IReadOnlyCollection<ExportDiagnostic> diagnostics,
        ExportResultOptions resultOptions,
        CancellationToken cancellationToken
    ) {
        string manifestPath = BuildManifestPath(outputFilePath);
        ExportManifest manifest = new() {
            ExportedAt = DateTimeOffset.Now,
            ConnectionName = connection.Name,
            DatabaseType = connection.DatabaseType.ToString(),
            ProfileName = profile.Name,
            OutputFilePath = outputFilePath,
            ResultOptions = new ExportManifestResultOptions {
                UseTimestamp = resultOptions.UseTimestamp,
                TimestampFormat = resultOptions.TimestampFormat,
                OverwriteStrategy = resultOptions.OverwriteStrategy.ToString(),
                OpenOutputFolder = resultOptions.OpenOutputFolder,
                GenerateManifest = resultOptions.GenerateManifest,
                GenerateJsonSidecar = resultOptions.GenerateJsonSidecar,
                GenerateMarkdownSidecar = resultOptions.GenerateMarkdownSidecar,
                GenerateSchemaSummary = resultOptions.GenerateSchemaSummary,
                GenerateSchemaSnapshot = resultOptions.GenerateSchemaSnapshot,
                DiffSourceSnapshotPath = resultOptions.DiffSourceSnapshotPath ?? ""
            },
            Counts = new ExportManifestCounts {
                Objects = filteredExport.Objects.Count,
                Columns = filteredExport.Columns.Count,
                Indexes = filteredExport.Indexes.Count,
                Routines = filteredExport.Routines.Count
            },
            Diagnostics = diagnostics.Select(x => new ExportManifestDiagnostic {
                Severity = x.SeverityText,
                Category = x.Category.ToString(),
                SupportLevel = x.SupportLevelText,
                AffectedObject = x.AffectedObject,
                Message = x.Message
            }).ToList()
        };

        try {
            string json = JsonSerializer.Serialize(manifest, SchemaArtifactJsonSerializerOptions.Default);
            await File.WriteAllTextAsync(manifestPath, json, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
            return manifestPath;
        } catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException or PathTooLongException) {
            throw new ExportOutputException($"無法產生 manifest 檔案：{manifestPath}", ex);
        }
    }

    private static string BuildManifestPath(string outputFilePath) {
        string? directoryPath = Path.GetDirectoryName(outputFilePath);
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(outputFilePath);
        return Path.Combine(directoryPath ?? "", $"{fileNameWithoutExtension}.manifest.json");
    }
}
