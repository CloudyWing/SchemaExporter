using CloudyWing.SchemaExporter.Core.SchemaProviders;

namespace CloudyWing.SchemaExporter.Core.Exporting.Snapshots;

/// <summary>
/// 將已篩選的 schema 匯出資料轉換為可序列化的 snapshot 文件。
/// </summary>
public sealed class SchemaSnapshotBuilder {
    /// <summary>
    /// 建立 schema snapshot 文件。
    /// </summary>
    /// <param name="outputFilePath">主要 Excel 輸出檔案路徑。</param>
    /// <param name="connection">匯出所使用的資料庫連線設定。</param>
    /// <param name="profile">匯出所使用的設定檔。</param>
    /// <param name="filteredExport">已篩選的 schema 匯出資料。</param>
    /// <param name="diagnostics">匯出過程收集的診斷資訊。</param>
    /// <returns>可序列化的 snapshot 文件。</returns>
    internal SchemaSnapshotDocument Build(
        string outputFilePath,
        SchemaConnection connection,
        ExportProfile profile,
        FilteredSchemaExport filteredExport,
        IReadOnlyCollection<ExportDiagnostic> diagnostics
    ) {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputFilePath);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(filteredExport);
        ArgumentNullException.ThrowIfNull(diagnostics);

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
                ObjectDescription = databaseObject.ObjectDescription ?? "",
                Columns = columnsByObject[databaseObject.ObjectKey]
                    .OrderBy(x => x.ColumnOrder)
                    .ThenBy(x => x.ColumnName, StringComparer.OrdinalIgnoreCase)
                    .Select(x => new SchemaSnapshotColumnDocument {
                        ColumnName = x.ColumnName,
                        ColumnType = x.ColumnType,
                        IsNullable = x.IsNullable,
                        ColumnDefault = x.ColumnDefault ?? "",
                        IsPrimaryKey = x.IsPrimaryKey,
                        IsIdentity = x.IsIdentity,
                        ColumnDescription = x.ColumnDescription ?? "",
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
                        OtherColumns = x.OtherColumns ?? ""
                    })
                    .ToList()
            }).ToList(),
            Routines = filteredExport.Routines.Select(x => new SchemaSnapshotRoutineDocument {
                SchemaName = x.SchemaName,
                ContainerName = x.ContainerName ?? "",
                RoutineName = x.RoutineName,
                RoutineType = x.RoutineType,
                OverloadIdentifier = x.OverloadIdentifier ?? "",
                ParameterSignature = x.ParameterSignature ?? "",
                ReturnType = x.ReturnType ?? "",
                RoutineDescription = x.RoutineDescription ?? "",
                RoutineDefinition = x.RoutineDefinition ?? ""
            }).ToList()
        };
    }
}
