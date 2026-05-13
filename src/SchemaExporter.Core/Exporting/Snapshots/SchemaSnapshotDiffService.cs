using System.Globalization;
using System.Text;
using System.Text.Json;
using CloudyWing.SchemaExporter.Core.Exporting.Diffs;

namespace CloudyWing.SchemaExporter.Core.Exporting.Snapshots;

/// <summary>
/// 提供結構描述快照的可重用載入、比較與格式化支援。
/// </summary>
public sealed class SchemaSnapshotDiffService {
    private static readonly JsonSerializerOptions JsonOptions = SchemaArtifactJsonSerializerOptions.Default;

    /// <summary>
    /// 從磁碟載入結構描述快照文件。
    /// </summary>
    /// <param name="snapshotPath">快照檔案路徑。</param>
    /// <param name="cancellationToken">取消語彙基元。</param>
    /// <returns>已還原序列化的快照文件。</returns>
    public async Task<SchemaSnapshotDocument> LoadSnapshotAsync(string snapshotPath, CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotPath);

        string normalizedPath = NormalizePath(snapshotPath);
        try {
            string json = await File.ReadAllTextAsync(normalizedPath, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
            SchemaSnapshotDocument? snapshot = JsonSerializer.Deserialize<SchemaSnapshotDocument>(json, JsonOptions)
                ?? throw new ExportValidationException($"無法讀取 schema snapshot 檔案：{normalizedPath}");

            return snapshot;
        } catch (ExportValidationException) {
            throw;
        } catch (JsonException ex) {
            throw new ExportValidationException($"Schema snapshot 檔案格式無效：{normalizedPath}", ex);
        } catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException or PathTooLongException) {
            throw new ExportValidationException($"無法讀取 schema snapshot 檔案：{normalizedPath}", ex);
        }
    }

    /// <summary>
    /// 比較從磁碟載入的兩份結構描述快照文件。
    /// </summary>
    /// <param name="leftSnapshotPath">基準快照路徑。</param>
    /// <param name="rightSnapshotPath">目前快照路徑。</param>
    /// <param name="cancellationToken">取消語彙基元。</param>
    /// <returns>計算完成的差異比對文件。</returns>
    public async Task<SchemaDiffDocument> CompareAsync(
        string leftSnapshotPath,
        string rightSnapshotPath,
        CancellationToken cancellationToken = default
    ) {
        string normalizedLeftPath = NormalizePath(leftSnapshotPath);
        string normalizedRightPath = NormalizePath(rightSnapshotPath);
        SchemaSnapshotDocument leftSnapshot = await LoadSnapshotAsync(normalizedLeftPath, cancellationToken).ConfigureAwait(false);
        SchemaSnapshotDocument rightSnapshot = await LoadSnapshotAsync(normalizedRightPath, cancellationToken).ConfigureAwait(false);
        return BuildDiff(leftSnapshot, rightSnapshot, normalizedLeftPath, normalizedRightPath);
    }

    /// <summary>
    /// 比較兩份已載入的結構描述快照文件。
    /// </summary>
    /// <param name="leftSnapshot">基準快照文件。</param>
    /// <param name="rightSnapshot">目前快照文件。</param>
    /// <param name="leftSnapshotPath">基準快照來源路徑。</param>
    /// <param name="rightSnapshotPath">目前快照來源路徑。</param>
    /// <returns>計算完成的差異比對文件。</returns>
    public SchemaDiffDocument Compare(
        SchemaSnapshotDocument leftSnapshot,
        SchemaSnapshotDocument rightSnapshot,
        string leftSnapshotPath,
        string rightSnapshotPath
    ) {
        ArgumentNullException.ThrowIfNull(leftSnapshot);
        ArgumentNullException.ThrowIfNull(rightSnapshot);
        ArgumentException.ThrowIfNullOrWhiteSpace(leftSnapshotPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(rightSnapshotPath);

        return BuildDiff(leftSnapshot, rightSnapshot, leftSnapshotPath, rightSnapshotPath);
    }

    /// <summary>
    /// 將差異比對文件以 JSON 格式寫出。
    /// </summary>
    /// <param name="outputPath">目標檔案路徑。</param>
    /// <param name="diff">要序列化的差異比對文件。</param>
    /// <param name="cancellationToken">取消語彙基元。</param>
    public async Task WriteJsonAsync(string outputPath, SchemaDiffDocument diff, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(diff);

        await WriteTextAsync(
            outputPath,
            JsonSerializer.Serialize(diff, JsonOptions),
            "無法產生 schema diff 檔案：",
            cancellationToken
        ).ConfigureAwait(false);
    }

    /// <summary>
    /// 將差異比對文件以 Markdown 格式寫出。
    /// </summary>
    /// <param name="outputPath">目標檔案路徑。</param>
    /// <param name="diff">要格式化的差異比對文件。</param>
    /// <param name="cancellationToken">取消語彙基元。</param>
    public async Task WriteMarkdownAsync(string outputPath, SchemaDiffDocument diff, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(diff);

        await WriteTextAsync(outputPath, BuildMarkdownReport(diff), "無法產生 schema diff 檔案：", cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// 建立結構描述差異比對的 Markdown 報告。
    /// </summary>
    /// <param name="diff">差異比對文件。</param>
    /// <returns>已格式化的 Markdown 報告字串。</returns>
    public string BuildMarkdownReport(SchemaDiffDocument diff) {
        ArgumentNullException.ThrowIfNull(diff);

        StringBuilder markdown = new();
        markdown.AppendLine("# Schema Diff");
        markdown.AppendLine();
        markdown.AppendLine($"- Generated At: {diff.GeneratedAt:O}");
        markdown.AppendLine($"- Left Snapshot: {EscapeMarkdown(diff.LeftSnapshotPath)}");
        markdown.AppendLine($"- Right Snapshot: {EscapeMarkdown(diff.RightSnapshotPath)}");
        markdown.AppendLine();
        markdown.AppendLine("## Summary");
        markdown.AppendLine();
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
        return markdown.ToString();
    }

    private static void AppendDiffSection(StringBuilder markdown, string title, IReadOnlyCollection<SchemaDiffEntry> entries) {
        markdown.AppendLine();
        markdown.AppendLine($"## {title}");
        markdown.AppendLine();
        if (entries.Count == 0) {
            markdown.AppendLine("None");
            return;
        }

        foreach (SchemaDiffEntry entry in entries) {
            markdown.AppendLine($"### {entry.ChangeType}: {EscapeMarkdown(entry.Identifier)}");
            markdown.AppendLine();

            foreach ((string propertyName, SchemaValueChange change) in entry.PropertyChanges.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)) {
                markdown.AppendLine($"- {EscapeMarkdown(propertyName)}");
                markdown.AppendLine($"  - Previous: {EscapeMarkdown(change.Previous ?? "")}");
                markdown.AppendLine($"  - Current: {EscapeMarkdown(change.Current ?? "")}");
            }

            markdown.AppendLine();
        }
    }

    private static SchemaDiffDocument BuildDiff(
        SchemaSnapshotDocument leftSnapshot,
        SchemaSnapshotDocument rightSnapshot,
        string leftSnapshotPath,
        string rightSnapshotPath
    ) {
        List<SchemaDiffEntry> objectChanges = BuildChangeEntries(
            leftSnapshot.Objects,
            rightSnapshot.Objects,
            static x => $"{x.SchemaName}|{x.ObjectName}|{x.ObjectType}",
            static x => $"{x.SchemaName}.{x.ObjectName} ({x.ObjectType})",
            static x => new Dictionary<string, string?> {
                [nameof(SchemaSnapshotObjectDocument.ObjectDescription)] = NormalizeComparableValue(x.ObjectDescription)
            }
        );

        List<SchemaDiffEntry> columnChanges = BuildChangeEntries(
            leftSnapshot.Objects.SelectMany(x => x.Columns.Select(column => new { Object = x, Column = column })),
            rightSnapshot.Objects.SelectMany(x => x.Columns.Select(column => new { Object = x, Column = column })),
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
            leftSnapshot.Objects.SelectMany(x => x.Indexes.Select(index => new { Object = x, Index = index })),
            rightSnapshot.Objects.SelectMany(x => x.Indexes.Select(index => new { Object = x, Index = index })),
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
            leftSnapshot.Routines,
            rightSnapshot.Routines,
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
                entries.Add(new SchemaDiffEntry {
                    ChangeType = SchemaChangeType.Added,
                    Identifier = identifierSelector(ensuredCurrentItem),
                    PropertyChanges = new Dictionary<string, SchemaValueChange>(BuildAddedOrRemovedChanges(valueSelector(ensuredCurrentItem), isAdded: true))
                });
                continue;
            }

            if (hasPrevious && !hasCurrent) {
                T ensuredPreviousItem = previousItem ?? throw new InvalidOperationException("Expected a previous item for a removed diff entry.");
                entries.Add(new SchemaDiffEntry {
                    ChangeType = SchemaChangeType.Removed,
                    Identifier = identifierSelector(ensuredPreviousItem),
                    PropertyChanges = new Dictionary<string, SchemaValueChange>(BuildAddedOrRemovedChanges(valueSelector(ensuredPreviousItem), isAdded: false))
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

            changes[propertyName] = new SchemaValueChange {
                Previous = previousValue,
                Current = currentValue
            };
        }

        return changes;
    }

    private static int CountChanges(IEnumerable<SchemaDiffEntry> entries, SchemaChangeType changeType) {
        return entries.Count(x => x.ChangeType == changeType);
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

    private static string NormalizeComparableValue(string? value) {
        return string.IsNullOrWhiteSpace(value)
            ? ""
            : value.Trim().Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private static string EscapeMarkdown(string value) {
        if (string.IsNullOrEmpty(value)) {
            return "";
        }

        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("\r\n", "<br />", StringComparison.Ordinal)
            .Replace("\n", "<br />", StringComparison.Ordinal);
    }

    private static string NormalizePath(string path) {
        if (string.IsNullOrWhiteSpace(path)) {
            throw new ExportValidationException("快照檔路徑不可為空白。");
        }

        string trimmedPath = path.Trim();
        string normalizedPath;
        try {
            normalizedPath = Path.GetFullPath(trimmedPath);
        } catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) {
            throw new ExportValidationException($"快照檔路徑格式無效：{trimmedPath}", ex);
        }

        if (!File.Exists(normalizedPath)) {
            throw new ExportValidationException($"找不到快照檔：{normalizedPath}");
        }

        return normalizedPath;
    }

    private static async Task WriteTextAsync(
        string outputPath,
        string content,
        string errorPrefix,
        CancellationToken cancellationToken) {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        try {
            await File.WriteAllTextAsync(outputPath, content, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        } catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException or PathTooLongException) {
            throw new ExportOutputException($"{errorPrefix}{outputPath}", ex);
        }
    }
}

