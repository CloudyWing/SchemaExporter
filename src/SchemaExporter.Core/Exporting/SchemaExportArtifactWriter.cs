using System.Text;
using System.Text.Json;
using CloudyWing.SchemaExporter.Core.Exporting.Diffs;
using CloudyWing.SchemaExporter.Core.Exporting.Snapshots;

namespace CloudyWing.SchemaExporter.Core.Exporting;

/// <summary>
/// 負責寫入次要匯出成品，包含資訊清單、附屬檔案、快照與差異比對結果。
/// </summary>
internal static class SchemaExportArtifactWriter {
    private const int SchemaSummarySplitThreshold = 50;

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
            await WriteSchemaSummaryAsync(
                schemaSummaryFilePath,
                snapshot,
                diff,
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

    private static async Task WriteSchemaSummaryAsync(
        string schemaSummaryFilePath,
        SchemaSnapshotDocument snapshot,
        SchemaDiffDocument? diff,
        CancellationToken cancellationToken,
        string errorMessagePrefix
    ) {
        string? entryDirectoryPath = Path.GetDirectoryName(schemaSummaryFilePath);
        string entryStem = Path.GetFileNameWithoutExtension(schemaSummaryFilePath);
        string objectsDirectoryName = entryStem + ".objects";
        string objectsDirectoryPath = Path.Combine(entryDirectoryPath ?? "", objectsDirectoryName);
        int detailCount = snapshot.Objects.Count + snapshot.Routines.Count;

        ValidateObjectsDirectoryPath(objectsDirectoryPath, entryDirectoryPath, objectsDirectoryName);
        IReadOnlySet<string> previousDetailFilePaths = await LoadLinkedDetailFilesAsync(
            schemaSummaryFilePath,
            objectsDirectoryPath,
            objectsDirectoryName,
            cancellationToken
        ).ConfigureAwait(false);
        if (detailCount <= SchemaSummarySplitThreshold) {
            string temporaryEntryPath = BuildTemporaryFilePath(schemaSummaryFilePath);
            EnsureTemporaryFileIsAvailable(temporaryEntryPath);
            try {
                await WriteTextArtifactAsync(
                    temporaryEntryPath,
                    SchemaSummaryMarkdownBuilder.BuildMarkdown(snapshot, diff),
                    cancellationToken,
                    errorMessagePrefix
                ).ConfigureAwait(false);
                ReplaceArtifactFile(temporaryEntryPath, schemaSummaryFilePath);
                DeleteDerivedObjectFiles(
                    previousDetailFilePaths,
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                    objectsDirectoryPath,
                    entryDirectoryPath,
                    objectsDirectoryName
                );
            } finally {
                TryDeleteFile(temporaryEntryPath);
            }

            return;
        }

        string stagingDirectoryPath = BuildTemporaryDirectoryPath(entryDirectoryPath, objectsDirectoryName, "staging");
        string backupDirectoryPath = BuildTemporaryDirectoryPath(entryDirectoryPath, objectsDirectoryName, "backup");
        List<string> stagedFilePaths = [];
        List<string> publishedDetailFilePaths = [];
        List<(string OriginalPath, string BackupPath)> backedUpDetailFiles = [];
        HashSet<string> currentDetailFilePaths = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> detailLinks = [];
        bool objectsDirectoryCreated = false;
        bool stagingDirectoryCreated = false;
        bool backupDirectoryCreated = false;
        bool entryPublished = false;
        try {
            EnsureTemporaryDirectoryIsAvailable(stagingDirectoryPath);
            EnsureTemporaryDirectoryIsAvailable(backupDirectoryPath);
            Directory.CreateDirectory(stagingDirectoryPath);
            stagingDirectoryCreated = true;

            int ordinal = 1;
            foreach (SchemaSnapshotObjectDocument databaseObject in OrderObjects(snapshot.Objects)) {
                string fileName = BuildDetailFileName(
                    ordinal,
                    "object",
                    databaseObject.SchemaName,
                    databaseObject.ObjectName
                );
                string finalFilePath = Path.Combine(objectsDirectoryPath, fileName);
                string stagedFilePath = Path.Combine(stagingDirectoryPath, fileName);
                IReadOnlyList<SchemaSnapshotRelationshipDocument> relationships = snapshot.Relationships
                    .Where(x => IsRelationshipForObject(x, databaseObject))
                    .ToList();
                stagedFilePaths.Add(stagedFilePath);
                await WriteTextArtifactAsync(
                    stagedFilePath,
                    SchemaSummaryMarkdownBuilder.BuildObjectMarkdown(
                        databaseObject,
                        relationships,
                        snapshot.Diagnostics
                    ),
                    cancellationToken,
                    errorMessagePrefix
                ).ConfigureAwait(false);
                currentDetailFilePaths.Add(Path.GetFullPath(finalFilePath));
                detailLinks[SchemaSummaryMarkdownBuilder.BuildObjectDetailKey(databaseObject)] =
                    BuildDetailLink(objectsDirectoryName, fileName);
                ordinal++;
            }

            foreach (SchemaSnapshotRoutineDocument routine in OrderRoutines(snapshot.Routines)) {
                string fileName = BuildDetailFileName(ordinal, "routine", routine.SchemaName, routine.RoutineName);
                string finalFilePath = Path.Combine(objectsDirectoryPath, fileName);
                string stagedFilePath = Path.Combine(stagingDirectoryPath, fileName);
                stagedFilePaths.Add(stagedFilePath);
                await WriteTextArtifactAsync(
                    stagedFilePath,
                    SchemaSummaryMarkdownBuilder.BuildRoutineMarkdown(routine),
                    cancellationToken,
                    errorMessagePrefix
                ).ConfigureAwait(false);
                currentDetailFilePaths.Add(Path.GetFullPath(finalFilePath));
                detailLinks[SchemaSummaryMarkdownBuilder.BuildRoutineDetailKey(routine)] =
                    BuildDetailLink(objectsDirectoryName, fileName);
                ordinal++;
            }

            ValidateObjectsDirectoryPath(objectsDirectoryPath, entryDirectoryPath, objectsDirectoryName);
            bool objectsDirectoryExisted = Directory.Exists(objectsDirectoryPath);
            Directory.CreateDirectory(objectsDirectoryPath);
            objectsDirectoryCreated = !objectsDirectoryExisted;
            EnsureDetailDestinationsAreSafe(currentDetailFilePaths, previousDetailFilePaths, objectsDirectoryPath);
            Directory.CreateDirectory(backupDirectoryPath);
            backupDirectoryCreated = true;

            foreach (string previousDetailFilePath in previousDetailFilePaths) {
                if (!currentDetailFilePaths.Contains(previousDetailFilePath)) {
                    continue;
                }

                string backupFilePath = Path.Combine(backupDirectoryPath, Path.GetFileName(previousDetailFilePath));
                backedUpDetailFiles.Add((previousDetailFilePath, backupFilePath));
                File.Move(previousDetailFilePath, backupFilePath);
            }

            foreach (string stagedFilePath in stagedFilePaths) {
                string fileName = Path.GetFileName(stagedFilePath);
                string finalFilePath = Path.Combine(objectsDirectoryPath, fileName);
                PublishDetailFile(stagedFilePath, finalFilePath);
                publishedDetailFilePaths.Add(finalFilePath);
            }

            string temporaryEntryPath = BuildTemporaryFilePath(schemaSummaryFilePath);
            EnsureTemporaryFileIsAvailable(temporaryEntryPath);
            try {
                await WriteTextArtifactAsync(
                    temporaryEntryPath,
                    SchemaSummaryMarkdownBuilder.BuildMarkdown(snapshot, diff, detailLinks),
                    cancellationToken,
                    errorMessagePrefix
                ).ConfigureAwait(false);
                ReplaceArtifactFile(temporaryEntryPath, schemaSummaryFilePath);
                entryPublished = true;
            } finally {
                TryDeleteFile(temporaryEntryPath);
            }

            DeleteDerivedObjectFiles(
                previousDetailFilePaths,
                currentDetailFilePaths,
                objectsDirectoryPath,
                entryDirectoryPath,
                objectsDirectoryName
            );
            foreach ((string _, string backupFilePath) in backedUpDetailFiles) {
                TryDeleteFile(backupFilePath);
            }
        } catch (Exception exception) {
            if (!entryPublished && !TryRestorePreviousDetails(publishedDetailFilePaths, backedUpDetailFiles)) {
                throw new ExportOutputException("Schema Summary 發佈失敗，且無法完整還原既有明細檔案。", exception);
            }

            throw;
        } finally {
            foreach (string stagedFilePath in stagedFilePaths) {
                TryDeleteFile(stagedFilePath);
            }

            foreach ((string _, string backupFilePath) in backedUpDetailFiles) {
                TryDeleteFile(backupFilePath);
            }

            if (stagingDirectoryCreated) {
                TryDeleteEmptyDirectory(stagingDirectoryPath);
            }

            if (backupDirectoryCreated) {
                TryDeleteEmptyDirectory(backupDirectoryPath);
            }

            if (objectsDirectoryCreated && !entryPublished) {
                TryDeleteEmptyDirectory(objectsDirectoryPath);
            }
        }
    }

    private static bool IsRelationshipForObject(
        SchemaSnapshotRelationshipDocument relationship,
        SchemaSnapshotObjectDocument databaseObject
    ) {
        bool isSourceObject = string.Equals(
            relationship.SourceSchemaName,
            databaseObject.SchemaName,
            StringComparison.OrdinalIgnoreCase
        ) && string.Equals(
            relationship.SourceTableName,
            databaseObject.ObjectName,
            StringComparison.OrdinalIgnoreCase
        );
        bool isReferencedObject = string.Equals(
            relationship.ReferencedSchemaName,
            databaseObject.SchemaName,
            StringComparison.OrdinalIgnoreCase
        ) && string.Equals(
            relationship.ReferencedTableName,
            databaseObject.ObjectName,
            StringComparison.OrdinalIgnoreCase
        );

        return isSourceObject || isReferencedObject;
    }

    private static IEnumerable<SchemaSnapshotObjectDocument> OrderObjects(
        IEnumerable<SchemaSnapshotObjectDocument> objects
    ) {
        return objects
            .OrderBy(x => x.SchemaName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.ObjectName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.ObjectType, StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<SchemaSnapshotRoutineDocument> OrderRoutines(
        IEnumerable<SchemaSnapshotRoutineDocument> routines
    ) {
        return routines
            .OrderBy(x => x.SchemaName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.ContainerName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.RoutineName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.RoutineType, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.OverloadIdentifier, StringComparer.OrdinalIgnoreCase);
    }

    private static string BuildDetailFileName(int ordinal, string kind, string schemaName, string objectName) {
        return $"{ordinal:D3}_{kind}_{SanitizeFileNamePart(schemaName)}_{SanitizeFileNamePart(objectName)}.md";
    }

    private static string SanitizeFileNamePart(string value) {
        StringBuilder sanitized = new();
        foreach (char character in value) {
            bool isAllowed = character is >= 'A' and <= 'Z'
                or >= 'a' and <= 'z'
                or >= '0' and <= '9'
                or '_' or '-' or '.';
            sanitized.Append(isAllowed ? character : '_');
        }

        return sanitized.Length == 0 ? "_" : sanitized.ToString();
    }

    private static async Task<IReadOnlySet<string>> LoadLinkedDetailFilesAsync(
        string schemaSummaryFilePath,
        string objectsDirectoryPath,
        string objectsDirectoryName,
        CancellationToken cancellationToken
    ) {
        if (!File.Exists(schemaSummaryFilePath)) {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        string summary;
        try {
            summary = await File.ReadAllTextAsync(schemaSummaryFilePath, cancellationToken).ConfigureAwait(false);
        } catch (Exception ex) when (
            ex is ArgumentException
                or IOException
                or UnauthorizedAccessException
                or NotSupportedException
                or PathTooLongException
        ) {
            throw new ExportOutputException($"無法讀取既有 Schema Summary 檔案：{schemaSummaryFilePath}", ex);
        }

        HashSet<string> linkedDetailFilePaths = new(StringComparer.OrdinalIgnoreCase);
        const string linkPrefix = "[details](";
        int searchStart = 0;
        while (searchStart < summary.Length) {
            int linkStart = summary.IndexOf(linkPrefix, searchStart, StringComparison.Ordinal);
            if (linkStart < 0) {
                break;
            }

            int linkValueStart = linkStart + linkPrefix.Length;
            int linkEnd = summary.IndexOf(')', linkValueStart);
            if (linkEnd < 0) {
                break;
            }

            string relativeLink = summary[linkValueStart..linkEnd];
            if (TryResolveDetailFilePath(
                    relativeLink,
                    objectsDirectoryPath,
                    objectsDirectoryName,
                    out string? filePath
                ) && filePath is not null) {
                linkedDetailFilePaths.Add(filePath);
            }

            searchStart = linkEnd + 1;
        }

        return linkedDetailFilePaths;
    }

    private static bool TryResolveDetailFilePath(
        string relativeLink,
        string objectsDirectoryPath,
        string objectsDirectoryName,
        out string? filePath
    ) {
        filePath = null;
        string encodedDirectoryName = Uri.EscapeDataString(objectsDirectoryName);
        string? encodedFileName = relativeLink.StartsWith($"{encodedDirectoryName}/", StringComparison.Ordinal)
            ? relativeLink[(encodedDirectoryName.Length + 1)..]
            : relativeLink.StartsWith($"{objectsDirectoryName}/", StringComparison.Ordinal)
                ? relativeLink[(objectsDirectoryName.Length + 1)..]
                : null;
        if (string.IsNullOrEmpty(encodedFileName) || encodedFileName.Contains('/')) {
            return false;
        }

        string decodedFileName;
        try {
            decodedFileName = Uri.UnescapeDataString(encodedFileName);
        } catch (UriFormatException) {
            return false;
        }

        if (string.IsNullOrEmpty(decodedFileName) || decodedFileName.Contains('/') || decodedFileName.Contains('\\')) {
            return false;
        }

        string normalizedDirectoryPath = Path.GetFullPath(objectsDirectoryPath);
        string normalizedFilePath = Path.GetFullPath(Path.Combine(normalizedDirectoryPath, decodedFileName));
        if (!string.Equals(
                Path.GetDirectoryName(normalizedFilePath),
                normalizedDirectoryPath,
                StringComparison.OrdinalIgnoreCase
            ) || !File.Exists(normalizedFilePath)) {
            return false;
        }

        filePath = normalizedFilePath;
        return true;
    }

    private static void ValidateObjectsDirectoryPath(
        string objectsDirectoryPath,
        string? entryDirectoryPath,
        string objectsDirectoryName
    ) {
        string normalizedObjectsDirectoryPath = Path.GetFullPath(objectsDirectoryPath);
        string normalizedEntryDirectoryPath = Path.GetFullPath(entryDirectoryPath ?? "");
        string? actualParentPath = Path.GetDirectoryName(normalizedObjectsDirectoryPath);
        string actualDirectoryName = Path.GetFileName(normalizedObjectsDirectoryPath);
        if (!string.Equals(actualParentPath, normalizedEntryDirectoryPath, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(actualDirectoryName, objectsDirectoryName, StringComparison.Ordinal)) {
            throw new ExportOutputException($"Schema Summary 明細資料夾路徑無效：{objectsDirectoryPath}");
        }
    }

    private static void EnsureDetailDestinationsAreSafe(
        IEnumerable<string> detailFilePaths,
        IReadOnlySet<string> previousDetailFilePaths,
        string objectsDirectoryPath
    ) {
        string normalizedDirectoryPath = Path.GetFullPath(objectsDirectoryPath);
        foreach (string detailFilePath in detailFilePaths) {
            string normalizedFilePath = Path.GetFullPath(detailFilePath);
            if (!string.Equals(
                    Path.GetDirectoryName(normalizedFilePath),
                    normalizedDirectoryPath,
                    StringComparison.OrdinalIgnoreCase
                )) {
                throw new ExportOutputException($"Schema Summary 明細檔案路徑無效：{detailFilePath}");
            }

            if (File.Exists(normalizedFilePath) && !previousDetailFilePaths.Contains(normalizedFilePath)) {
                throw new ExportOutputException($"Schema Summary 明細檔案已存在且未由現有 entry 參照：{normalizedFilePath}");
            }
        }
    }

    private static void DeleteDerivedObjectFiles(
        IEnumerable<string> previousDetailFilePaths,
        IReadOnlySet<string> currentDetailFilePaths,
        string objectsDirectoryPath,
        string? entryDirectoryPath,
        string objectsDirectoryName
    ) {
        ValidateObjectsDirectoryPath(objectsDirectoryPath, entryDirectoryPath, objectsDirectoryName);
        string normalizedDirectoryPath = Path.GetFullPath(objectsDirectoryPath);
        foreach (string previousDetailFilePath in previousDetailFilePaths) {
            string normalizedFilePath = Path.GetFullPath(previousDetailFilePath);
            if (currentDetailFilePaths.Contains(normalizedFilePath)
                || !string.Equals(
                    Path.GetDirectoryName(normalizedFilePath),
                    normalizedDirectoryPath,
                    StringComparison.OrdinalIgnoreCase
                )) {
                continue;
            }

            TryDeleteFile(normalizedFilePath);
        }

        TryDeleteEmptyDirectory(normalizedDirectoryPath);
    }

    private static bool TryRestorePreviousDetails(
        IEnumerable<string> publishedDetailFilePaths,
        IEnumerable<(string OriginalPath, string BackupPath)> backedUpDetailFiles
    ) {
        bool restored = true;
        foreach (string publishedDetailFilePath in publishedDetailFilePaths) {
            restored &= TryDeleteFile(publishedDetailFilePath);
        }

        foreach ((string originalPath, string backupPath) in backedUpDetailFiles) {
            if (!File.Exists(backupPath)) {
                continue;
            }

            try {
                File.Move(backupPath, originalPath, overwrite: true);
            } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) {
                restored = false;
            }
        }

        return restored;
    }

    private static void PublishDetailFile(string stagedFilePath, string finalFilePath) {
        try {
            File.Move(stagedFilePath, finalFilePath, overwrite: true);
        } catch (Exception ex) when (
            ex is ArgumentException
                or IOException
                or UnauthorizedAccessException
                or NotSupportedException
                or PathTooLongException
        ) {
            throw new ExportOutputException($"無法發佈 Schema Summary 明細檔案：{finalFilePath}", ex);
        }
    }

    private static void ReplaceArtifactFile(string temporaryFilePath, string targetFilePath) {
        try {
            File.Move(temporaryFilePath, targetFilePath, overwrite: true);
        } catch (Exception ex) when (
            ex is ArgumentException
                or IOException
                or UnauthorizedAccessException
                or NotSupportedException
                or PathTooLongException
        ) {
            throw new ExportOutputException($"無法取代 Schema Summary 檔案：{targetFilePath}", ex);
        }
    }

    private static void EnsureTemporaryDirectoryIsAvailable(string directoryPath) {
        if (Directory.Exists(directoryPath) || File.Exists(directoryPath)) {
            throw new ExportOutputException($"Schema Summary 暫存路徑已存在：{directoryPath}");
        }
    }

    private static void EnsureTemporaryFileIsAvailable(string filePath) {
        if (File.Exists(filePath) || Directory.Exists(filePath)) {
            throw new ExportOutputException($"Schema Summary 暫存檔案已存在：{filePath}");
        }
    }

    private static string BuildDetailLink(string objectsDirectoryName, string fileName) {
        return $"{Uri.EscapeDataString(objectsDirectoryName)}/{Uri.EscapeDataString(fileName)}";
    }

    private static string BuildTemporaryFilePath(string filePath) {
        return $"{filePath}.tmp-{Guid.NewGuid():N}";
    }

    private static string BuildTemporaryDirectoryPath(
        string? entryDirectoryPath,
        string objectsDirectoryName,
        string suffix
    ) {
        return Path.Combine(
            entryDirectoryPath ?? "",
            $"{objectsDirectoryName}.{suffix}-{Guid.NewGuid():N}"
        );
    }

    private static bool TryDeleteFile(string filePath) {
        try {
            File.Delete(filePath);
            return true;
        } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) {
            return false;
        }
    }

    private static bool TryDeleteEmptyDirectory(string directoryPath) {
        try {
            if (!Directory.Exists(directoryPath) || Directory.EnumerateFileSystemEntries(directoryPath).Any()) {
                return false;
            }

            Directory.Delete(directoryPath);
            return true;
        } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) {
            return false;
        }
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
        markdown.AppendLine($"- Relationships: {snapshot.Counts.Relationships}");
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
            markdown.AppendLine($"- Added Relationships: {diff.Summary.AddedRelationships}");
            markdown.AppendLine($"- Removed Relationships: {diff.Summary.RemovedRelationships}");
            markdown.AppendLine($"- Modified Relationships: {diff.Summary.ModifiedRelationships}");
            markdown.AppendLine($"- Added Routines: {diff.Summary.AddedRoutines}");
            markdown.AppendLine($"- Removed Routines: {diff.Summary.RemovedRoutines}");
            markdown.AppendLine($"- Modified Routines: {diff.Summary.ModifiedRoutines}");
            AppendDiffSection(markdown, "Object Changes", diff.ObjectChanges);
            AppendDiffSection(markdown, "Column Changes", diff.ColumnChanges);
            AppendDiffSection(markdown, "Index Changes", diff.IndexChanges);
            AppendDiffSection(markdown, "Relationship Changes", diff.RelationshipChanges);
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

        AppendRelationships(markdown, snapshot.Relationships, snapshot.Diagnostics);
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
                markdown.AppendLine("| Name | PK | Clustered | Unique | Columns | Included Columns |");
                markdown.AppendLine("| --- | --- | --- | --- | --- | --- |");
                foreach (SchemaSnapshotIndexDocument index in databaseObject.Indexes) {
                    markdown.AppendLine(
                        $"| {EscapeMarkdown(index.IndexName)} | {EscapeMarkdown(index.IsPrimaryKey)} | {EscapeMarkdown(index.IsClustered)} | {EscapeMarkdown(index.IsUnique)} | {EscapeMarkdown(index.Columns)} | {EscapeMarkdown(index.IncludedColumns)} |"
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
            }
        }

        return markdown.ToString();
    }

    private static void AppendRelationships(
        StringBuilder markdown,
        IReadOnlyCollection<SchemaSnapshotRelationshipDocument> relationships,
        IReadOnlyCollection<SchemaSnapshotDiagnostic> diagnostics
    ) {
        markdown.AppendLine();
        markdown.AppendLine("## Relationships");
        markdown.AppendLine();

        List<SchemaSnapshotRelationshipDocument> completeRelationships = relationships
            .Where(HasCompleteRelationship)
            .OrderBy(x => x.SourceSchemaName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.SourceTableName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.ForeignKeyName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.ColumnOrder)
            .ToList();
        if (completeRelationships.Count == 0) {
            bool permissionDenied = diagnostics.Any(x => string.Equals(
                x.Message,
                "FK catalog permission denied; relationships were not retrieved.",
                StringComparison.Ordinal
            ));
            markdown.AppendLine(permissionDenied
                ? "Relationships unavailable: FK catalog permission denied."
                : "No relationships found.");
            return;
        }

        markdown.AppendLine("| Constraint | Source Table | Source Column | Referenced Table | Referenced Column |");
        markdown.AppendLine("| --- | --- | --- | --- | --- |");
        foreach (SchemaSnapshotRelationshipDocument relationship in completeRelationships) {
            markdown.AppendLine(
                $"| {EscapeMarkdown(relationship.ForeignKeyName)} | {EscapeMarkdown(relationship.SourceSchemaName)}.{EscapeMarkdown(relationship.SourceTableName)} | {EscapeMarkdown(relationship.SourceColumnName)} | {EscapeMarkdown(relationship.ReferencedSchemaName)}.{EscapeMarkdown(relationship.ReferencedTableName)} | {EscapeMarkdown(relationship.ReferencedColumnName)} |"
            );
        }
    }

    private static bool HasCompleteRelationship(SchemaSnapshotRelationshipDocument relationship) {
        return !string.IsNullOrWhiteSpace(relationship.ForeignKeyName)
            && !string.IsNullOrWhiteSpace(relationship.SourceSchemaName)
            && !string.IsNullOrWhiteSpace(relationship.SourceTableName)
            && !string.IsNullOrWhiteSpace(relationship.SourceColumnName)
            && !string.IsNullOrWhiteSpace(relationship.ReferencedSchemaName)
            && !string.IsNullOrWhiteSpace(relationship.ReferencedTableName)
            && !string.IsNullOrWhiteSpace(relationship.ReferencedColumnName);
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
                Routines = filteredExport.Routines.Count,
                Relationships = filteredExport.Relationships.Count
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
