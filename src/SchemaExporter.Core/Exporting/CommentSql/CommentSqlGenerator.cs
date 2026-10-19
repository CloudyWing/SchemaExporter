using System.Globalization;
using System.Text;
using CloudyWing.SchemaExporter.Core.Exporting.Snapshots;

namespace CloudyWing.SchemaExporter.Core.Exporting.CommentSql;

/// <summary>
/// 依 Snapshot 與註解清單產生資料表與欄位註解的 SQL 腳本。
/// </summary>
/// <remarks>
/// 本類別只處理記憶體內的資料，不連線、不執行、不對資料庫下任何查詢。
/// 相同的 Snapshot 與註解清單永遠產生相同的腳本文字。
/// </remarks>
public sealed class CommentSqlGenerator {
    /// <summary>
    /// 依 Snapshot 與註解清單產生註解 SQL。
    /// </summary>
    /// <param name="snapshot">資料庫現況的 Snapshot，提供物件與欄位名稱及現有註解。</param>
    /// <param name="entries">要寫入的註解清單。</param>
    /// <returns>產生結果，含腳本文字與各類筆數。</returns>
    /// <exception cref="ExportValidationException">Snapshot 或清單有任何無法產生 SQL 的項目；訊息逐項列出全部問題。</exception>
    public CommentSqlResult Generate(SchemaSnapshotDocument snapshot, IReadOnlyList<CommentSqlEntry> entries) {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(entries);

        if (!Enum.TryParse(snapshot.DatabaseType, ignoreCase: true, out DatabaseType databaseType)
            || !Enum.IsDefined(databaseType)) {
            throw new ExportValidationException($"Snapshot 的資料庫類型不支援：{snapshot.DatabaseType}");
        }

        ICommentSqlWriter writer = databaseType == DatabaseType.Oracle
            ? new OracleCommentSqlWriter()
            : new SqlServerCommentSqlWriter();

        List<string> errors = [];
        List<PlannedStatement> plannedStatements = [];
        HashSet<(string, string, string)> seenTargets = [];
        SnapshotIndex snapshotIndex = new(snapshot.Objects);
        int unchangedCount = 0;
        int skippedEmptyCount = 0;

        for (int index = 0; index < entries.Count; index++) {
            CommentSqlEntry entry = entries[index];
            string label = $"第 {index + 1} 筆";

            if (!TryResolveTarget(snapshotIndex, entry, label, errors, out ResolvedTarget? resolved)) {
                continue;
            }

            CommentSqlTarget target = resolved.Target;
            string targetLabel = $"{label}（{target.Describe()}）";

            if (entry.Comment is null) {
                errors.Add($"{targetLabel}：缺少 comment 欄位");
                continue;
            }

            if (!seenTargets.Add((target.SchemaName, target.ObjectName, target.ColumnName ?? ""))) {
                errors.Add($"{targetLabel}：重複指定同一個目標");
                continue;
            }

            if (entry.Comment.Length == 0) {
                skippedEmptyCount++;
                continue;
            }

            if (!ValidateTargetAndComment(writer, target, entry.Comment, targetLabel, errors)) {
                continue;
            }

            if (string.Equals(entry.Comment, resolved.ExistingComment, StringComparison.Ordinal)) {
                unchangedCount++;
                continue;
            }

            plannedStatements.Add(new PlannedStatement(target, entry.Comment, resolved.ExistingComment));
        }

        if (errors.Count > 0) {
            throw new ExportValidationException(
                $"註解清單有 {errors.Count} 個問題，未產生 SQL：{Environment.NewLine}"
                + string.Join(Environment.NewLine, errors.Select(x => "- " + x))
            );
        }

        List<PlannedStatement> addStatements = plannedStatements
            .Where(x => x.ExistingComment.Length == 0)
            .OrderBy(x => x, PlannedStatementComparer.Instance)
            .ToList();
        List<PlannedStatement> updateStatements = plannedStatements
            .Where(x => x.ExistingComment.Length > 0)
            .OrderBy(x => x, PlannedStatementComparer.Instance)
            .ToList();

        return new CommentSqlResult {
            DatabaseType = databaseType,
            SqlText = BuildScript(writer, snapshot.ConnectionName, addStatements, updateStatements, unchangedCount, skippedEmptyCount),
            EmitUtf8Bom = writer.EmitUtf8Bom,
            AddCount = addStatements.Count,
            UpdateCount = updateStatements.Count,
            UnchangedCount = unchangedCount,
            SkippedEmptyCount = skippedEmptyCount,
            Warnings = BuildWarnings(snapshot)
        };
    }

    private static bool ValidateTargetAndComment(
        ICommentSqlWriter writer,
        CommentSqlTarget target,
        string comment,
        string targetLabel,
        List<string> errors
    ) {
        int errorCountBefore = errors.Count;

        foreach (string name in new[] { target.SchemaName, target.ObjectName, target.ColumnName ?? "" }) {
            if (name.Length == 0) {
                continue;
            }

            string? nameProblem = CommentSqlText.FindUnsupportedNameCharacter(name) ?? writer.ValidateName(name);
            if (nameProblem is not null) {
                errors.Add($"{targetLabel}：名稱 {CommentSqlText.EscapeForComment(name)} 無法寫入腳本，{nameProblem}");
            }
        }

        string? commentProblem = CommentSqlText.FindUnsupportedCommentCharacter(comment) ?? writer.ValidateComment(comment);
        if (commentProblem is not null) {
            errors.Add($"{targetLabel}：{commentProblem}");
        }

        return errors.Count == errorCountBefore;
    }

    private static bool TryResolveTarget(
        SnapshotIndex index,
        CommentSqlEntry entry,
        string label,
        List<string> errors,
        out ResolvedTarget resolved
    ) {
        resolved = null!;

        if (string.IsNullOrEmpty(entry.Schema) || string.IsNullOrEmpty(entry.ObjectName)) {
            errors.Add($"{label}：schema 與 object 都必須有值");
            return false;
        }

        string requested = string.IsNullOrEmpty(entry.Column)
            ? $"{entry.Schema}.{entry.ObjectName}"
            : $"{entry.Schema}.{entry.ObjectName}.{entry.Column}";
        string requestedText = CommentSqlText.EscapeForComment(requested);

        IReadOnlyList<SchemaSnapshotObjectDocument> objectMatches = index.FindObjects(entry.Schema, entry.ObjectName);
        if (objectMatches.Count == 0) {
            errors.Add($"{label}：Snapshot 找不到物件 {requestedText}");
            return false;
        }

        if (objectMatches.Count > 1) {
            errors.Add($"{label}：Snapshot 內有多個物件只差在大小寫，無法判定 {requestedText}");
            return false;
        }

        SchemaSnapshotObjectDocument databaseObject = objectMatches[0];
        bool? isView = databaseObject.ObjectType.ToUpperInvariant() switch {
            "BASE TABLE" or "TABLE" => false,
            "VIEW" => true,
            _ => null
        };
        if (isView is null) {
            errors.Add($"{label}：物件 {requestedText} 的類型 {databaseObject.ObjectType} 不支援，僅支援資料表與檢視表");
            return false;
        }

        if (string.IsNullOrEmpty(entry.Column)) {
            resolved = new ResolvedTarget(
                new CommentSqlTarget(databaseObject.SchemaName, databaseObject.ObjectName, isView.Value, null),
                databaseObject.ObjectDescription ?? ""
            );
            return true;
        }

        IReadOnlyList<SchemaSnapshotColumnDocument> columnMatches = index.FindColumns(databaseObject, entry.Column);
        if (columnMatches.Count == 0) {
            errors.Add($"{label}：Snapshot 找不到欄位 {requestedText}");
            return false;
        }

        if (columnMatches.Count > 1) {
            errors.Add($"{label}：Snapshot 內有多個欄位只差在大小寫，無法判定 {requestedText}");
            return false;
        }

        resolved = new ResolvedTarget(
            new CommentSqlTarget(databaseObject.SchemaName, databaseObject.ObjectName, isView.Value, columnMatches[0].ColumnName),
            columnMatches[0].ColumnDescription ?? ""
        );
        return true;
    }

    private static string BuildScript(
        ICommentSqlWriter writer,
        string connectionName,
        List<PlannedStatement> addStatements,
        List<PlannedStatement> updateStatements,
        int unchangedCount,
        int skippedEmptyCount
    ) {
        StringBuilder builder = new();
        writer.WriteHeader(builder, connectionName);
        builder.Append(
            CultureInfo.InvariantCulture,
            $"-- Add: {addStatements.Count}, Update: {updateStatements.Count}, Unchanged (skipped): {unchangedCount}, Empty comment (skipped): {skippedEmptyCount}"
        ).Append(CommentSqlText.NewLine);

        builder.Append(CommentSqlText.NewLine);
        builder.Append("-- ===== Add: the database has no comment yet =====").Append(CommentSqlText.NewLine);
        if (addStatements.Count == 0) {
            builder.Append("-- (none)").Append(CommentSqlText.NewLine);
        }

        foreach (PlannedStatement statement in addStatements) {
            builder.Append(CommentSqlText.NewLine);
            builder.Append("-- ").Append(statement.Target.Describe()).Append(CommentSqlText.NewLine);
            writer.WriteStatement(builder, statement.Target, statement.Comment);
        }

        builder.Append(CommentSqlText.NewLine);
        builder.Append("-- ===== Update: the database comment differs; the existing value is shown above each statement =====").Append(CommentSqlText.NewLine);
        if (updateStatements.Count == 0) {
            builder.Append("-- (none)").Append(CommentSqlText.NewLine);
        }

        foreach (PlannedStatement statement in updateStatements) {
            builder.Append(CommentSqlText.NewLine);
            builder.Append("-- ").Append(statement.Target.Describe()).Append(CommentSqlText.NewLine);
            WriteExistingValue(builder, statement.ExistingComment);
            writer.WriteStatement(builder, statement.Target, statement.Comment);
        }

        return builder.ToString();
    }

    private static void WriteExistingValue(StringBuilder builder, string existingComment) {
        string label = string.IsNullOrWhiteSpace(existingComment)
            ? "-- existing (whitespace-only): "
            : "-- existing: ";

        IReadOnlyList<string> lines = CommentSqlText.DescribeValue(existingComment);
        for (int index = 0; index < lines.Count; index++) {
            builder.Append(index == 0 ? label : "-- ...continued: ").Append(lines[index]).Append(CommentSqlText.NewLine);
        }
    }

    private static List<string> BuildWarnings(SchemaSnapshotDocument snapshot) {
        List<string> warnings = [];
        if (snapshot.Diagnostics.Any(x => string.Equals(x.Category, nameof(ExportDiagnosticCategory.Redaction), StringComparison.OrdinalIgnoreCase))) {
            warnings.Add("Snapshot 套用過敏感 metadata 遮罩，其中的現有註解可能已被取代，新增與差異的判定可能不準確。");
        }

        return warnings;
    }

    private sealed class SnapshotIndex {
        private readonly NameIndex<SchemaSnapshotObjectDocument> objects;
        private readonly Dictionary<SchemaSnapshotObjectDocument, NameIndex<SchemaSnapshotColumnDocument>> columnsByObject
            = new(ReferenceEqualityComparer.Instance);

        public SnapshotIndex(IReadOnlyList<SchemaSnapshotObjectDocument> snapshotObjects) {
            objects = new NameIndex<SchemaSnapshotObjectDocument>(snapshotObjects, x => ObjectKey(x.SchemaName, x.ObjectName));
        }

        public IReadOnlyList<SchemaSnapshotObjectDocument> FindObjects(string schema, string objectName) {
            return objects.Find(ObjectKey(schema, objectName));
        }

        public IReadOnlyList<SchemaSnapshotColumnDocument> FindColumns(SchemaSnapshotObjectDocument databaseObject, string column) {
            if (!columnsByObject.TryGetValue(databaseObject, out NameIndex<SchemaSnapshotColumnDocument>? columns)) {
                columns = new NameIndex<SchemaSnapshotColumnDocument>(databaseObject.Columns, x => x.ColumnName);
                columnsByObject[databaseObject] = columns;
            }

            return columns.Find(column);
        }

        // 名稱不會含 NUL，用它分隔 schema 與物件名稱不會讓不同組合產生相同的鍵。
        private static string ObjectKey(string schema, string objectName) => schema + '\0' + objectName;
    }

    private sealed class NameIndex<T> {
        private readonly Dictionary<string, List<T>> exact = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<T>> ignoreCase = new(StringComparer.OrdinalIgnoreCase);

        public NameIndex(IEnumerable<T> items, Func<T, string> keySelector) {
            foreach (T item in items) {
                string key = keySelector(item);
                Add(exact, key, item);
                Add(ignoreCase, key, item);
            }
        }

        // 大小寫完全相符優先，沒有才退回不分大小寫比對。
        public IReadOnlyList<T> Find(string key) {
            if (exact.TryGetValue(key, out List<T>? exactMatches)) {
                return exactMatches;
            }

            return ignoreCase.TryGetValue(key, out List<T>? ignoreCaseMatches) ? ignoreCaseMatches : [];
        }

        private static void Add(Dictionary<string, List<T>> target, string key, T item) {
            if (!target.TryGetValue(key, out List<T>? list)) {
                list = [];
                target[key] = list;
            }

            list.Add(item);
        }
    }

    private sealed record ResolvedTarget(CommentSqlTarget Target, string ExistingComment);

    private sealed record PlannedStatement(CommentSqlTarget Target, string Comment, string ExistingComment);

    private sealed class PlannedStatementComparer : IComparer<PlannedStatement> {
        public static readonly PlannedStatementComparer Instance = new();

        public int Compare(PlannedStatement? x, PlannedStatement? y) {
            ArgumentNullException.ThrowIfNull(x);
            ArgumentNullException.ThrowIfNull(y);

            int result = string.CompareOrdinal(x.Target.SchemaName, y.Target.SchemaName);
            if (result != 0) {
                return result;
            }

            result = string.CompareOrdinal(x.Target.ObjectName, y.Target.ObjectName);
            if (result != 0) {
                return result;
            }

            return string.CompareOrdinal(x.Target.ColumnName ?? "", y.Target.ColumnName ?? "");
        }
    }
}
