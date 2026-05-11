using System.Globalization;
using System.Text;
using CloudyWing.SchemaExporter.Core.Exporting.Diffs;
using CloudyWing.SchemaExporter.Core.Exporting.Snapshots;

namespace CloudyWing.SchemaExporter.Core.Exporting;

/// <summary>
/// 建立供 AI Agent 讀取的 schema context Markdown 內容。
/// </summary>
internal static class SchemaAiContextBuilder {
    /// <summary>
    /// 建立 schema context Markdown。
    /// </summary>
    /// <param name="snapshot">目前匯出的 schema snapshot。</param>
    /// <param name="diff">與基準 snapshot 的差異；未執行差異比對時為 <see langword="null"/>。</param>
    /// <returns>Markdown 格式的 schema context。</returns>
    internal static string BuildMarkdown(SchemaSnapshotDocument snapshot, SchemaDiffDocument? diff) {
        ArgumentNullException.ThrowIfNull(snapshot);

        StringBuilder markdown = new();
        markdown.AppendLine("# Schema Context");
        markdown.AppendLine();
        markdown.AppendLine("## Scope");
        markdown.AppendLine();
        markdown.AppendLine($"- Connection: {EscapeMarkdown(snapshot.ConnectionName)}");
        markdown.AppendLine($"- Database Type: {EscapeMarkdown(snapshot.DatabaseType)}");
        markdown.AppendLine($"- Profile: {EscapeMarkdown(snapshot.ProfileName)}");
        markdown.AppendLine($"- Exported At: {snapshot.ExportedAt:O}");
        markdown.AppendLine("- Content: schema metadata only; row data and sample values are not included.");
        markdown.AppendLine("- Routine definitions are omitted; use routine signatures and descriptions for context.");
        markdown.AppendLine();
        markdown.AppendLine("## Counts");
        markdown.AppendLine();
        markdown.AppendLine("| Item | Count |");
        markdown.AppendLine("| --- | --- |");
        AppendTableRow(markdown, "Objects", snapshot.Counts.Objects.ToString(CultureInfo.InvariantCulture));
        AppendTableRow(markdown, "Columns", snapshot.Counts.Columns.ToString(CultureInfo.InvariantCulture));
        AppendTableRow(markdown, "Indexes", snapshot.Counts.Indexes.ToString(CultureInfo.InvariantCulture));
        AppendTableRow(markdown, "Routines", snapshot.Counts.Routines.ToString(CultureInfo.InvariantCulture));

        AppendDiagnostics(markdown, snapshot.Diagnostics);
        AppendProviderCapabilities(markdown, snapshot.DatabaseType);
        AppendDiffSummary(markdown, diff);
        AppendObjectInventory(markdown, snapshot.Objects);
        AppendObjects(markdown, snapshot.Objects);
        AppendRoutines(markdown, snapshot.Routines);

        return markdown.ToString();
    }

    private static void AppendDiagnostics(
        StringBuilder markdown,
        IReadOnlyCollection<SchemaSnapshotDiagnostic> diagnostics
    ) {
        if (diagnostics.Count == 0) {
            return;
        }

        markdown.AppendLine();
        markdown.AppendLine("## Diagnostics");
        markdown.AppendLine();
        markdown.AppendLine("| Severity | Category | Affected Object | Message |");
        markdown.AppendLine("| --- | --- | --- | --- |");
        foreach (SchemaSnapshotDiagnostic diagnostic in diagnostics) {
            AppendTableRow(
                markdown,
                diagnostic.Severity,
                diagnostic.Category,
                diagnostic.AffectedObject ?? "",
                diagnostic.Message
            );
        }
    }

    private static void AppendProviderCapabilities(StringBuilder markdown, string databaseType) {
        IReadOnlyList<ProviderCapability> capabilities = ProviderCapabilityMatrix.GetCapabilities(databaseType);
        if (capabilities.Count == 0) {
            return;
        }

        markdown.AppendLine();
        markdown.AppendLine("## Provider Capabilities");
        markdown.AppendLine();
        markdown.AppendLine("| Area | Support | Notes |");
        markdown.AppendLine("| --- | --- | --- |");
        foreach (ProviderCapability capability in capabilities) {
            AppendTableRow(
                markdown,
                capability.Area,
                capability.SupportLevel.ToString(),
                capability.Notes
            );
        }
    }

    private static void AppendDiffSummary(StringBuilder markdown, SchemaDiffDocument? diff) {
        if (diff is null) {
            return;
        }

        markdown.AppendLine();
        markdown.AppendLine("## Diff Summary");
        markdown.AppendLine();
        markdown.AppendLine($"- Left Snapshot: {EscapeMarkdown(diff.LeftSnapshotPath)}");
        markdown.AppendLine($"- Right Snapshot: {EscapeMarkdown(diff.RightSnapshotPath)}");
        markdown.AppendLine();
        markdown.AppendLine("| Item | Added | Removed | Modified |");
        markdown.AppendLine("| --- | --- | --- | --- |");
        AppendDiffSummaryRow(
            markdown,
            "Objects",
            diff.Summary.AddedObjects,
            diff.Summary.RemovedObjects,
            diff.Summary.ModifiedObjects
        );
        AppendDiffSummaryRow(
            markdown,
            "Columns",
            diff.Summary.AddedColumns,
            diff.Summary.RemovedColumns,
            diff.Summary.ModifiedColumns
        );
        AppendDiffSummaryRow(
            markdown,
            "Indexes",
            diff.Summary.AddedIndexes,
            diff.Summary.RemovedIndexes,
            diff.Summary.ModifiedIndexes
        );
        AppendDiffSummaryRow(
            markdown,
            "Routines",
            diff.Summary.AddedRoutines,
            diff.Summary.RemovedRoutines,
            diff.Summary.ModifiedRoutines
        );
    }

    private static void AppendObjectInventory(
        StringBuilder markdown,
        IReadOnlyCollection<SchemaSnapshotObjectDocument> databaseObjects
    ) {
        if (databaseObjects.Count == 0) {
            return;
        }

        markdown.AppendLine();
        markdown.AppendLine("## Object Inventory");
        markdown.AppendLine();
        markdown.AppendLine("| Object | Type | Columns | Indexes | Description |");
        markdown.AppendLine("| --- | --- | --- | --- | --- |");
        foreach (SchemaSnapshotObjectDocument databaseObject in OrderObjects(databaseObjects)) {
            AppendTableRow(
                markdown,
                BuildObjectIdentifier(databaseObject),
                databaseObject.ObjectType,
                databaseObject.Columns.Count.ToString(CultureInfo.InvariantCulture),
                databaseObject.Indexes.Count.ToString(CultureInfo.InvariantCulture),
                databaseObject.ObjectDescription
            );
        }
    }

    private static void AppendObjects(
        StringBuilder markdown,
        IReadOnlyCollection<SchemaSnapshotObjectDocument> databaseObjects
    ) {
        if (databaseObjects.Count == 0) {
            return;
        }

        markdown.AppendLine();
        markdown.AppendLine("## Objects");
        foreach (SchemaSnapshotObjectDocument databaseObject in OrderObjects(databaseObjects)) {
            markdown.AppendLine();
            string objectIdentifier = EscapeMarkdown(BuildObjectIdentifier(databaseObject));
            string objectType = EscapeMarkdown(databaseObject.ObjectType);
            markdown.AppendLine($"### {objectIdentifier} ({objectType})");

            if (!string.IsNullOrWhiteSpace(databaseObject.ObjectDescription)) {
                markdown.AppendLine();
                markdown.AppendLine(EscapeMarkdown(databaseObject.ObjectDescription));
            }

            AppendColumns(markdown, databaseObject.Columns);
            AppendIndexes(markdown, databaseObject.Indexes);
        }
    }

    private static void AppendColumns(
        StringBuilder markdown,
        IReadOnlyCollection<SchemaSnapshotColumnDocument> columns
    ) {
        markdown.AppendLine();
        markdown.AppendLine("#### Columns");
        markdown.AppendLine();
        if (columns.Count == 0) {
            markdown.AppendLine("No column metadata was exported.");
            return;
        }

        markdown.AppendLine("| Order | Name | Type | Null | Flags | Default | Description |");
        markdown.AppendLine("| --- | --- | --- | --- | --- | --- | --- |");
        foreach (SchemaSnapshotColumnDocument column in columns.OrderBy(x => x.ColumnOrder)) {
            AppendTableRow(
                markdown,
                column.ColumnOrder.ToString(CultureInfo.InvariantCulture),
                column.ColumnName,
                column.ColumnType,
                column.IsNullable,
                BuildColumnFlags(column),
                column.ColumnDefault,
                column.ColumnDescription
            );
        }
    }

    private static void AppendIndexes(
        StringBuilder markdown,
        IReadOnlyCollection<SchemaSnapshotIndexDocument> indexes
    ) {
        if (indexes.Count == 0) {
            return;
        }

        markdown.AppendLine();
        markdown.AppendLine("#### Indexes");
        markdown.AppendLine();
        markdown.AppendLine("| Name | Flags | Columns | Other Columns |");
        markdown.AppendLine("| --- | --- | --- | --- |");
        foreach (SchemaSnapshotIndexDocument index in indexes
            .OrderBy(x => x.IndexName, StringComparer.OrdinalIgnoreCase)
        ) {
            AppendTableRow(markdown, index.IndexName, BuildIndexFlags(index), index.Columns, index.OtherColumns);
        }
    }

    private static void AppendRoutines(
        StringBuilder markdown,
        IReadOnlyCollection<SchemaSnapshotRoutineDocument> routines
    ) {
        if (routines.Count == 0) {
            return;
        }

        markdown.AppendLine();
        markdown.AppendLine("## Routines");
        markdown.AppendLine();
        markdown.AppendLine("| Routine | Type | Parameters | Return Type | Description |");
        markdown.AppendLine("| --- | --- | --- | --- | --- |");
        foreach (SchemaSnapshotRoutineDocument routine in routines
            .OrderBy(x => x.SchemaName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.ContainerName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.RoutineName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.OverloadIdentifier, StringComparer.OrdinalIgnoreCase)
        ) {
            AppendTableRow(
                markdown,
                BuildRoutineIdentifier(routine),
                routine.RoutineType,
                routine.ParameterSignature,
                routine.ReturnType,
                routine.RoutineDescription
            );
        }
    }

    private static void AppendDiffSummaryRow(
        StringBuilder markdown,
        string item,
        int added,
        int removed,
        int modified
    ) {
        AppendTableRow(
            markdown,
            item,
            added.ToString(CultureInfo.InvariantCulture),
            removed.ToString(CultureInfo.InvariantCulture),
            modified.ToString(CultureInfo.InvariantCulture)
        );
    }

    private static IEnumerable<SchemaSnapshotObjectDocument> OrderObjects(
        IEnumerable<SchemaSnapshotObjectDocument> databaseObjects
    ) {
        return databaseObjects
            .OrderBy(x => x.SchemaName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.ObjectName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.ObjectType, StringComparer.OrdinalIgnoreCase);
    }

    private static string BuildObjectIdentifier(SchemaSnapshotObjectDocument databaseObject) {
        return $"{databaseObject.SchemaName}.{databaseObject.ObjectName}";
    }

    private static string BuildRoutineIdentifier(SchemaSnapshotRoutineDocument routine) {
        string qualifiedName = string.IsNullOrWhiteSpace(routine.ContainerName)
            ? $"{routine.SchemaName}.{routine.RoutineName}"
            : $"{routine.SchemaName}.{routine.ContainerName}.{routine.RoutineName}";

        return string.IsNullOrWhiteSpace(routine.OverloadIdentifier)
            ? qualifiedName
            : $"{qualifiedName}#{routine.OverloadIdentifier}";
    }

    private static string BuildColumnFlags(SchemaSnapshotColumnDocument column) {
        List<string> flags = [];
        if (IsEnabled(column.IsPrimaryKey)) {
            flags.Add("primary key");
        }

        if (IsEnabled(column.IsIdentity)) {
            flags.Add("identity");
        }

        return string.Join(", ", flags);
    }

    private static string BuildIndexFlags(SchemaSnapshotIndexDocument index) {
        List<string> flags = [];
        if (IsEnabled(index.IsPrimaryKey)) {
            flags.Add("primary key");
        }

        if (IsEnabled(index.IsClustered)) {
            flags.Add("clustered");
        }

        if (IsEnabled(index.IsUnique)) {
            flags.Add("unique");
        }

        if (IsEnabled(index.IsForeignKey)) {
            flags.Add("foreign key");
        }

        return string.Join(", ", flags);
    }

    private static bool IsEnabled(string value) {
        return string.Equals(value, "YES", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "TRUE", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "1", StringComparison.OrdinalIgnoreCase);
    }

    private static void AppendTableRow(StringBuilder markdown, params string[] values) {
        markdown.Append("| ");
        markdown.Append(string.Join(" | ", values.Select(EscapeMarkdown)));
        markdown.AppendLine(" |");
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
}
