using System.Globalization;
using System.Text;
using System.Text.Json;
using CloudyWing.SchemaExporter.Core.SchemaProviders;

namespace CloudyWing.SchemaExporter.Core.Exporting;

/// <summary>
/// 負責寫入次要匯出成品，包含資訊清單、附屬檔案、快照與差異比對結果。
/// </summary>
internal static class SchemaExportArtifactWriter {
    internal static async Task<ArtifactOutputs> WriteArtifactsAsync(
        string outputFilePath,
        SchemaConnection connection,
        ExportProfile profile,
        FilteredSchemaExport filteredExport,
        List<ExportDiagnostic> diagnostics,
        ExportResultOptions resultOptions,
        CancellationToken cancellationToken
    ) {
        string? manifestFilePath = null;
        if (resultOptions.GenerateManifest) {
            manifestFilePath = await GenerateManifestAsync(outputFilePath, connection, profile, filteredExport, diagnostics, resultOptions, cancellationToken).ConfigureAwait(false);
        }

        SchemaSnapshotDocument snapshot = BuildSnapshot(outputFilePath, connection, profile, filteredExport, diagnostics);
        string? snapshotFilePath = null;
        if (resultOptions.GenerateSchemaSnapshot) {
            snapshotFilePath = BuildArtifactPath(outputFilePath, "snapshot.json");
            await WriteJsonArtifactAsync(snapshotFilePath, snapshot, cancellationToken, "無法產生 schema snapshot 檔案：").ConfigureAwait(false);
        }

        SchemaDiffDocument? diff = null;
        string? diffFilePath = null;
        if (!string.IsNullOrWhiteSpace(resultOptions.DiffSourceSnapshotPath)) {
            string normalizedDiffSourcePath = Path.GetFullPath(resultOptions.DiffSourceSnapshotPath.Trim());
            SchemaSnapshotDocument previousSnapshot = await LoadSnapshotAsync(normalizedDiffSourcePath, cancellationToken).ConfigureAwait(false);
            diff = BuildDiff(previousSnapshot, snapshot, normalizedDiffSourcePath, snapshotFilePath ?? outputFilePath);
            diffFilePath = BuildArtifactPath(outputFilePath, "diff.json");
            await WriteJsonArtifactAsync(diffFilePath, diff, cancellationToken, "無法產生 schema diff 檔案：").ConfigureAwait(false);
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

        return new ArtifactOutputs {
            ManifestFilePath = manifestFilePath,
            JsonSidecarFilePath = jsonSidecarFilePath,
            MarkdownSidecarFilePath = markdownSidecarFilePath,
            SnapshotFilePath = snapshotFilePath,
            DiffFilePath = diffFilePath
        };
    }

    private static SchemaSnapshotDocument BuildSnapshot(
        string outputFilePath,
        SchemaConnection connection,
        ExportProfile profile,
        FilteredSchemaExport filteredExport,
        IReadOnlyCollection<ExportDiagnostic> diagnostics
    ) {
        ILookup<DatabaseObjectKey, DatabaseColumnSchema> columnsByObject = filteredExport.Columns.ToLookup(x => x.ObjectKey);
        ILookup<DatabaseObjectKey, DatabaseIndexSchema> indexesByObject = filteredExport.Indexes.ToLookup(x => x.ObjectKey);

        return new SchemaSnapshotDocument {
            SchemaVersion = 2,
            ExportedAt = DateTimeOffset.Now,
            ConnectionName = connection.Name,
            DatabaseType = connection.DatabaseType.ToString(),
            ProfileName = profile.Name,
            OutputFilePath = outputFilePath,
            Counts = new SchemaSnapshotCounts {
                Objects = filteredExport.Objects.Count,
                Columns = filteredExport.Columns.Count,
                Indexes = filteredExport.Indexes.Count,
                Routines = filteredExport.Routines.Count
            },
            Diagnostics = diagnostics.Select(x => new SchemaSnapshotDiagnostic {
                Severity = x.SeverityText,
                Category = x.Category.ToString(),
                SupportLevel = x.SupportLevelText,
                AffectedObject = x.AffectedObject,
                Message = x.Message
            }).ToList(),
            Objects = filteredExport.Objects.Select(databaseObject => new SchemaSnapshotObjectDocument {
                SchemaName = databaseObject.SchemaName,
                ObjectName = databaseObject.ObjectName,
                ObjectType = databaseObject.ObjectType,
                ObjectDescription = databaseObject.ObjectDescription,
                Columns = columnsByObject[databaseObject.ObjectKey]
                    .OrderBy(x => x.ColumnOrder)
                    .ThenBy(x => x.ColumnName, StringComparer.OrdinalIgnoreCase)
                    .Select(x => new SchemaSnapshotColumnDocument {
                        ColumnName = x.ColumnName,
                        ColumnType = x.ColumnType,
                        IsNullable = x.IsNullable,
                        ColumnDefault = x.ColumnDefault,
                        IsPrimaryKey = x.IsPrimaryKey,
                        IsIdentity = x.IsIdentity,
                        ColumnDescription = x.ColumnDescription,
                        ColumnOrder = x.ColumnOrder
                    })
                    .ToList(),
                Indexes = indexesByObject[databaseObject.ObjectKey]
                    .OrderBy(x => x.IndexName, StringComparer.OrdinalIgnoreCase)
                    .Select(x => new SchemaSnapshotIndexDocument {
                        IndexName = x.IndexName,
                        IsPrimaryKey = x.IsPrimaryKey,
                        IsClustered = x.IsClustered,
                        IsUnique = x.IsUnique,
                        IsForeignKey = x.IsForeignKey,
                        Columns = x.Columns,
                        OtherColumns = x.OtherColumns
                    })
                    .ToList()
            }).ToList(),
            Routines = filteredExport.Routines.Select(x => new SchemaSnapshotRoutineDocument {
                SchemaName = x.SchemaName,
                ContainerName = x.ContainerName,
                RoutineName = x.RoutineName,
                RoutineType = x.RoutineType,
                OverloadIdentifier = x.OverloadIdentifier,
                ParameterSignature = x.ParameterSignature,
                ReturnType = x.ReturnType,
                RoutineDescription = x.RoutineDescription,
                RoutineDefinition = x.RoutineDefinition
            }).ToList()
        };
    }

    private static async Task<SchemaSnapshotDocument> LoadSnapshotAsync(string snapshotPath, CancellationToken cancellationToken) {
        try {
            string json = await File.ReadAllTextAsync(snapshotPath, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
            SchemaSnapshotDocument? snapshot = JsonSerializer.Deserialize<SchemaSnapshotDocument>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );
            if (snapshot is null) {
                throw new ExportValidationException($"無法讀取 schema snapshot 檔案：{snapshotPath}");
            }

            return snapshot;
        } catch (ExportValidationException) {
            throw;
        } catch (JsonException ex) {
            throw new ExportValidationException($"Schema snapshot 檔案格式無效：{snapshotPath}", ex);
        } catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException or PathTooLongException) {
            throw new ExportValidationException($"無法讀取 schema snapshot 檔案：{snapshotPath}", ex);
        }
    }

    private static SchemaDiffDocument BuildDiff(
        SchemaSnapshotDocument previousSnapshot,
        SchemaSnapshotDocument currentSnapshot,
        string leftSnapshotPath,
        string rightSnapshotPath
    ) {
        List<SchemaDiffEntry> objectChanges = BuildChangeEntries(
            previousSnapshot.Objects,
            currentSnapshot.Objects,
            static x => $"{x.SchemaName}|{x.ObjectName}|{x.ObjectType}",
            static x => $"{x.SchemaName}.{x.ObjectName} ({x.ObjectType})",
            static x => new Dictionary<string, string?> {
                [nameof(SchemaSnapshotObjectDocument.ObjectDescription)] = NormalizeComparableValue(x.ObjectDescription)
            }
        );

        List<SchemaDiffEntry> columnChanges = BuildChangeEntries(
            previousSnapshot.Objects.SelectMany(x => x.Columns.Select(column => new { Object = x, Column = column })),
            currentSnapshot.Objects.SelectMany(x => x.Columns.Select(column => new { Object = x, Column = column })),
            static x => $"{x.Object.SchemaName}|{x.Object.ObjectName}|{x.Object.ObjectType}|{x.Column.ColumnName}",
            static x => $"{x.Object.SchemaName}.{x.Object.ObjectName}.{x.Column.ColumnName} ({x.Object.ObjectType})",
            static x => new Dictionary<string, string?> {
                [nameof(SchemaSnapshotColumnDocument.ColumnType)] = NormalizeComparableValue(x.Column.ColumnType),
                [nameof(SchemaSnapshotColumnDocument.IsNullable)] = NormalizeComparableValue(x.Column.IsNullable),
                [nameof(SchemaSnapshotColumnDocument.ColumnDefault)] = NormalizeComparableValue(x.Column.ColumnDefault),
                [nameof(SchemaSnapshotColumnDocument.IsPrimaryKey)] = NormalizeComparableValue(x.Column.IsPrimaryKey),
                [nameof(SchemaSnapshotColumnDocument.IsIdentity)] = NormalizeComparableValue(x.Column.IsIdentity),
                [nameof(SchemaSnapshotColumnDocument.ColumnDescription)] = NormalizeComparableValue(x.Column.ColumnDescription),
                [nameof(SchemaSnapshotColumnDocument.ColumnOrder)] = x.Column.ColumnOrder.ToString(CultureInfo.InvariantCulture)
            }
        );

        List<SchemaDiffEntry> indexChanges = BuildChangeEntries(
            previousSnapshot.Objects.SelectMany(x => x.Indexes.Select(index => new { Object = x, Index = index })),
            currentSnapshot.Objects.SelectMany(x => x.Indexes.Select(index => new { Object = x, Index = index })),
            static x => $"{x.Object.SchemaName}|{x.Object.ObjectName}|{x.Object.ObjectType}|{x.Index.IndexName}",
            static x => $"{x.Object.SchemaName}.{x.Object.ObjectName}.{x.Index.IndexName} ({x.Object.ObjectType})",
            static x => new Dictionary<string, string?> {
                [nameof(SchemaSnapshotIndexDocument.IsPrimaryKey)] = NormalizeComparableValue(x.Index.IsPrimaryKey),
                [nameof(SchemaSnapshotIndexDocument.IsClustered)] = NormalizeComparableValue(x.Index.IsClustered),
                [nameof(SchemaSnapshotIndexDocument.IsUnique)] = NormalizeComparableValue(x.Index.IsUnique),
                [nameof(SchemaSnapshotIndexDocument.IsForeignKey)] = NormalizeComparableValue(x.Index.IsForeignKey),
                [nameof(SchemaSnapshotIndexDocument.Columns)] = NormalizeComparableValue(x.Index.Columns),
                [nameof(SchemaSnapshotIndexDocument.OtherColumns)] = NormalizeComparableValue(x.Index.OtherColumns)
            }
        );

        List<SchemaDiffEntry> routineChanges = BuildChangeEntries(
            previousSnapshot.Routines,
            currentSnapshot.Routines,
            static x => $"{x.SchemaName}|{x.ContainerName}|{x.RoutineName}|{x.RoutineType}|{x.OverloadIdentifier}",
            static x => BuildRoutineIdentifier(x.SchemaName, x.ContainerName, x.RoutineName, x.RoutineType, x.OverloadIdentifier),
            static x => new Dictionary<string, string?> {
                [nameof(SchemaSnapshotRoutineDocument.ParameterSignature)] = NormalizeComparableValue(x.ParameterSignature),
                [nameof(SchemaSnapshotRoutineDocument.ReturnType)] = NormalizeComparableValue(x.ReturnType),
                [nameof(SchemaSnapshotRoutineDocument.RoutineDescription)] = NormalizeComparableValue(x.RoutineDescription),
                [nameof(SchemaSnapshotRoutineDocument.RoutineDefinition)] = NormalizeComparableValue(x.RoutineDefinition)
            }
        );

        return new SchemaDiffDocument {
            SchemaVersion = 2,
            GeneratedAt = DateTimeOffset.Now,
            LeftSnapshotPath = leftSnapshotPath,
            RightSnapshotPath = rightSnapshotPath,
            Summary = new SchemaDiffSummary {
                AddedObjects = CountChanges(objectChanges, SchemaChangeType.Added),
                RemovedObjects = CountChanges(objectChanges, SchemaChangeType.Removed),
                ModifiedObjects = CountChanges(objectChanges, SchemaChangeType.Modified),
                AddedColumns = CountChanges(columnChanges, SchemaChangeType.Added),
                RemovedColumns = CountChanges(columnChanges, SchemaChangeType.Removed),
                ModifiedColumns = CountChanges(columnChanges, SchemaChangeType.Modified),
                AddedIndexes = CountChanges(indexChanges, SchemaChangeType.Added),
                RemovedIndexes = CountChanges(indexChanges, SchemaChangeType.Removed),
                ModifiedIndexes = CountChanges(indexChanges, SchemaChangeType.Modified),
                AddedRoutines = CountChanges(routineChanges, SchemaChangeType.Added),
                RemovedRoutines = CountChanges(routineChanges, SchemaChangeType.Removed),
                ModifiedRoutines = CountChanges(routineChanges, SchemaChangeType.Modified)
            },
            ObjectChanges = objectChanges,
            ColumnChanges = columnChanges,
            IndexChanges = indexChanges,
            RoutineChanges = routineChanges
        };
    }

    private static List<SchemaDiffEntry> BuildChangeEntries<T>(
        IEnumerable<T> previousItems,
        IEnumerable<T> currentItems,
        Func<T, string> keySelector,
        Func<T, string> identifierSelector,
        Func<T, IReadOnlyDictionary<string, string?>> valueSelector
    ) where T : notnull {
        Dictionary<string, T> previousMap = previousItems.ToDictionary(keySelector, StringComparer.OrdinalIgnoreCase);
        Dictionary<string, T> currentMap = currentItems.ToDictionary(keySelector, StringComparer.OrdinalIgnoreCase);
        SortedSet<string> allKeys = [.. previousMap.Keys, .. currentMap.Keys];
        List<SchemaDiffEntry> entries = [];

        foreach (string key in allKeys) {
            bool hasPrevious = previousMap.TryGetValue(key, out T? previousItem);
            bool hasCurrent = currentMap.TryGetValue(key, out T? currentItem);

            if (!hasPrevious && hasCurrent) {
                T ensuredCurrentItem = currentItem ?? throw new InvalidOperationException("Expected a current item for an added diff entry.");
                IReadOnlyDictionary<string, SchemaValueChange> propertyChanges = BuildAddedOrRemovedChanges(valueSelector(ensuredCurrentItem), true);
                entries.Add(new SchemaDiffEntry {
                    ChangeType = SchemaChangeType.Added,
                    Identifier = identifierSelector(ensuredCurrentItem),
                    PropertyChanges = new Dictionary<string, SchemaValueChange>(propertyChanges)
                });
                continue;
            }

            if (hasPrevious && !hasCurrent) {
                T ensuredPreviousItem = previousItem ?? throw new InvalidOperationException("Expected a previous item for a removed diff entry.");
                IReadOnlyDictionary<string, SchemaValueChange> propertyChanges = BuildAddedOrRemovedChanges(valueSelector(ensuredPreviousItem), false);
                entries.Add(new SchemaDiffEntry {
                    ChangeType = SchemaChangeType.Removed,
                    Identifier = identifierSelector(ensuredPreviousItem),
                    PropertyChanges = new Dictionary<string, SchemaValueChange>(propertyChanges)
                });
                continue;
            }

            T ensuredPreviousItemForComparison = previousItem ?? throw new InvalidOperationException("Expected a previous item for a modified diff entry.");
            T ensuredCurrentItemForComparison = currentItem ?? throw new InvalidOperationException("Expected a current item for a modified diff entry.");
            IReadOnlyDictionary<string, string?> previousValues = valueSelector(ensuredPreviousItemForComparison);
            IReadOnlyDictionary<string, string?> currentValues = valueSelector(ensuredCurrentItemForComparison);
            IReadOnlyDictionary<string, SchemaValueChange> differences = BuildModifiedChanges(previousValues, currentValues);
            if (differences.Count == 0) {
                continue;
            }

            entries.Add(new SchemaDiffEntry {
                ChangeType = SchemaChangeType.Modified,
                Identifier = identifierSelector(ensuredCurrentItemForComparison),
                PropertyChanges = new Dictionary<string, SchemaValueChange>(differences)
            });
        }

        return entries;
    }

    private static IReadOnlyDictionary<string, SchemaValueChange> BuildAddedOrRemovedChanges(
        IReadOnlyDictionary<string, string?> values,
        bool isAdded
    ) {
        Dictionary<string, SchemaValueChange> changes = [];
        foreach ((string propertyName, string? value) in values) {
            changes[propertyName] = isAdded
                ? new SchemaValueChange { Current = value }
                : new SchemaValueChange { Previous = value };
        }

        return changes;
    }

    private static IReadOnlyDictionary<string, SchemaValueChange> BuildModifiedChanges(
        IReadOnlyDictionary<string, string?> previousValues,
        IReadOnlyDictionary<string, string?> currentValues
    ) {
        SortedSet<string> propertyNames = [.. previousValues.Keys, .. currentValues.Keys];
        Dictionary<string, SchemaValueChange> changes = [];
        foreach (string propertyName in propertyNames) {
            previousValues.TryGetValue(propertyName, out string? previousValue);
            currentValues.TryGetValue(propertyName, out string? currentValue);
            if (string.Equals(previousValue, currentValue, StringComparison.Ordinal)) {
                continue;
            }

            changes[propertyName] = new SchemaValueChange { Previous = previousValue, Current = currentValue };
        }

        return changes;
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

    private static string NormalizeComparableValue(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return "";
        }

        return value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Trim();
    }

    private static int CountChanges(IEnumerable<SchemaDiffEntry> entries, SchemaChangeType changeType) {
        return entries.Count(entry => entry.ChangeType == changeType);
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
            string json = JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true });
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
            string json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
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
