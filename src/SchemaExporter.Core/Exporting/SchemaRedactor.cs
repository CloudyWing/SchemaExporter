using System.Text.RegularExpressions;
using CloudyWing.SchemaExporter.Core.SchemaProviders;

namespace CloudyWing.SchemaExporter.Core.Exporting;

internal static class SchemaRedactor {
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(250);

    public static FilteredSchemaExport Apply(
        FilteredSchemaExport source,
        SchemaRedactionOptions options,
        List<ExportDiagnostic> diagnostics
    ) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(diagnostics);

        if (!options.Enabled) {
            return source;
        }

        if (string.IsNullOrWhiteSpace(options.ReplacementText)) {
            throw new ExportValidationException("Schema.Redaction.ReplacementText 不可為空白。");
        }

        List<Regex> namePatterns = BuildPatterns(options.SensitiveNamePatterns, "Schema.Redaction.SensitiveNamePatterns");
        List<Regex> textPatterns = BuildPatterns(options.SensitiveTextPatterns, "Schema.Redaction.SensitiveTextPatterns");
        if (namePatterns.Count == 0 && textPatterns.Count == 0) {
            return source;
        }

        string replacementText = options.ReplacementText;
        int redactedValueCount = 0;
        FilteredSchemaExport redactedExport = new() {
            Objects = RedactObjects(
                source.Objects,
                namePatterns,
                textPatterns,
                replacementText,
                ref redactedValueCount
            ),
            Columns = RedactColumns(
                source.Columns,
                namePatterns,
                textPatterns,
                replacementText,
                ref redactedValueCount
            ),
            Indexes = source.Indexes.Select(CloneIndex).ToList(),
            Routines = RedactRoutines(
                source.Routines,
                namePatterns,
                textPatterns,
                replacementText,
                ref redactedValueCount
            )
        };

        if (redactedValueCount > 0) {
            diagnostics.Add(new ExportDiagnostic {
                Severity = DiagnosticSeverity.Info,
                Category = ExportDiagnosticCategory.Redaction,
                Message = $"Redaction 已套用，共遮罩 {redactedValueCount} 個 metadata 值。"
            });
        }

        return redactedExport;
    }

    private static List<DatabaseObjectSchema> RedactObjects(
        IReadOnlyList<DatabaseObjectSchema> databaseObjects,
        IReadOnlyList<Regex> namePatterns,
        IReadOnlyList<Regex> textPatterns,
        string replacementText,
        ref int redactedValueCount
    ) {
        List<DatabaseObjectSchema> redactedObjects = [];
        foreach (DatabaseObjectSchema databaseObject in databaseObjects) {
            redactedObjects.Add(
                RedactObject(databaseObject, namePatterns, textPatterns, replacementText, ref redactedValueCount)
            );
        }

        return redactedObjects;
    }

    private static List<DatabaseColumnSchema> RedactColumns(
        IReadOnlyList<DatabaseColumnSchema> columns,
        IReadOnlyList<Regex> namePatterns,
        IReadOnlyList<Regex> textPatterns,
        string replacementText,
        ref int redactedValueCount
    ) {
        List<DatabaseColumnSchema> redactedColumns = [];
        foreach (DatabaseColumnSchema column in columns) {
            redactedColumns.Add(
                RedactColumn(column, namePatterns, textPatterns, replacementText, ref redactedValueCount)
            );
        }

        return redactedColumns;
    }

    private static List<DatabaseRoutineSchema> RedactRoutines(
        IReadOnlyList<DatabaseRoutineSchema> routines,
        IReadOnlyList<Regex> namePatterns,
        IReadOnlyList<Regex> textPatterns,
        string replacementText,
        ref int redactedValueCount
    ) {
        List<DatabaseRoutineSchema> redactedRoutines = [];
        foreach (DatabaseRoutineSchema routine in routines) {
            redactedRoutines.Add(
                RedactRoutine(routine, namePatterns, textPatterns, replacementText, ref redactedValueCount)
            );
        }

        return redactedRoutines;
    }

    private static DatabaseObjectSchema RedactObject(
        DatabaseObjectSchema databaseObject,
        IReadOnlyList<Regex> namePatterns,
        IReadOnlyList<Regex> textPatterns,
        string replacementText,
        ref int redactedValueCount
    ) {
        bool redactByName = IsSensitiveName(
            namePatterns,
            databaseObject.ObjectName,
            $"{databaseObject.SchemaName}.{databaseObject.ObjectName}",
            $"{databaseObject.SchemaName}.{databaseObject.ObjectName} ({databaseObject.ObjectType})"
        );

        return new DatabaseObjectSchema {
            SchemaName = databaseObject.SchemaName,
            ObjectName = databaseObject.ObjectName,
            ObjectType = databaseObject.ObjectType,
            ObjectDescription = RedactValue(
                databaseObject.ObjectDescription,
                redactByName,
                textPatterns,
                replacementText,
                ref redactedValueCount
            )
        };
    }

    private static DatabaseColumnSchema RedactColumn(
        DatabaseColumnSchema column,
        IReadOnlyList<Regex> namePatterns,
        IReadOnlyList<Regex> textPatterns,
        string replacementText,
        ref int redactedValueCount
    ) {
        bool redactByName = IsSensitiveName(
            namePatterns,
            column.ColumnName,
            $"{column.SchemaName}.{column.ObjectName}.{column.ColumnName}",
            $"{column.SchemaName}.{column.ObjectName}.{column.ColumnName} ({column.ObjectType})"
        );

        return new DatabaseColumnSchema {
            SchemaName = column.SchemaName,
            ObjectName = column.ObjectName,
            ObjectType = column.ObjectType,
            ColumnName = column.ColumnName,
            ColumnType = column.ColumnType,
            IsNullable = column.IsNullable,
            ColumnDefault = RedactValue(
                column.ColumnDefault,
                redactByName,
                textPatterns,
                replacementText,
                ref redactedValueCount
            ),
            IsPrimaryKey = column.IsPrimaryKey,
            IsIdentity = column.IsIdentity,
            ColumnDescription = RedactValue(
                column.ColumnDescription,
                redactByName,
                textPatterns,
                replacementText,
                ref redactedValueCount
            ),
            ColumnOrder = column.ColumnOrder
        };
    }

    private static DatabaseIndexSchema CloneIndex(DatabaseIndexSchema index) {
        return new DatabaseIndexSchema {
            SchemaName = index.SchemaName,
            ObjectName = index.ObjectName,
            ObjectType = index.ObjectType,
            IndexName = index.IndexName,
            IsPrimaryKey = index.IsPrimaryKey,
            IsClustered = index.IsClustered,
            IsUnique = index.IsUnique,
            IsForeignKey = index.IsForeignKey,
            Columns = index.Columns,
            OtherColumns = index.OtherColumns
        };
    }

    private static DatabaseRoutineSchema RedactRoutine(
        DatabaseRoutineSchema routine,
        IReadOnlyList<Regex> namePatterns,
        IReadOnlyList<Regex> textPatterns,
        string replacementText,
        ref int redactedValueCount
    ) {
        bool redactByName = IsSensitiveName(
            namePatterns,
            routine.RoutineName,
            routine.QualifiedName,
            $"{routine.QualifiedName} ({routine.RoutineType})"
        );

        return new DatabaseRoutineSchema {
            SchemaName = routine.SchemaName,
            ContainerName = routine.ContainerName,
            RoutineName = routine.RoutineName,
            RoutineType = routine.RoutineType,
            OverloadIdentifier = routine.OverloadIdentifier,
            ParameterSignature = RedactValue(
                routine.ParameterSignature,
                redactByName,
                textPatterns,
                replacementText,
                ref redactedValueCount
            ),
            ReturnType = routine.ReturnType,
            RoutineDescription = RedactValue(
                routine.RoutineDescription,
                redactByName,
                textPatterns,
                replacementText,
                ref redactedValueCount
            ),
            RoutineDefinition = RedactValue(
                routine.RoutineDefinition,
                redactByName,
                textPatterns,
                replacementText,
                ref redactedValueCount
            )
        };
    }

    private static string? RedactValue(
        string? value,
        bool redactWholeValue,
        IReadOnlyList<Regex> textPatterns,
        string replacementText,
        ref int redactedValueCount
    ) {
        if (string.IsNullOrEmpty(value)) {
            return value;
        }

        if (redactWholeValue) {
            redactedValueCount++;
            return replacementText;
        }

        string redactedValue = value;
        foreach (Regex textPattern in textPatterns) {
            redactedValue = textPattern.Replace(redactedValue, replacementText);
        }

        if (!string.Equals(value, redactedValue, StringComparison.Ordinal)) {
            redactedValueCount++;
        }

        return redactedValue;
    }

    private static bool IsSensitiveName(IReadOnlyList<Regex> patterns, params string[] candidates) {
        foreach (string candidate in candidates) {
            foreach (Regex pattern in patterns) {
                if (pattern.IsMatch(candidate)) {
                    return true;
                }
            }
        }

        return false;
    }

    private static List<Regex> BuildPatterns(IReadOnlyList<string>? patterns, string path) {
        if (patterns is null) {
            throw new ExportValidationException($"{path} 不可為 null。");
        }

        List<Regex> regexes = [];
        for (int index = 0; index < patterns.Count; index++) {
            string? pattern = patterns[index];
            if (string.IsNullOrWhiteSpace(pattern)) {
                continue;
            }

            try {
                regexes.Add(new Regex(
                    pattern.Trim(),
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                    RegexTimeout
                ));
            } catch (ArgumentException ex) {
                throw new ExportValidationException($"{path}[{index}] 不是有效的規則運算式：{pattern}", ex);
            }
        }

        return regexes;
    }
}
