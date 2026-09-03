using System.Data.Common;
using Dapper;
using CloudyWing.SchemaExporter.Core.Exporting;
using Microsoft.Data.SqlClient;

namespace CloudyWing.SchemaExporter.Core.SchemaProviders;

/// <summary>
/// 提供 SQL Server 資料庫結構描述的載入實作。
/// </summary>
internal sealed class SqlServerDatabaseSchemaProvider : IDatabaseSchemaProvider {
    /// <inheritdoc/>
    public DatabaseType DatabaseType => DatabaseType.SqlServer;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<DatabaseObjectSchema>> LoadObjectsAsync(
        string connectionString,
        CancellationToken cancellationToken = default
    ) {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        using DbConnection connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        IReadOnlyList<DatabaseObjectSchema> objects = [
            ..await connection.QueryAsync<DatabaseObjectSchema>(
                new CommandDefinition(QueryObjectsSql, cancellationToken: cancellationToken)
            ).ConfigureAwait(false)
        ];

        return objects;
    }

    /// <inheritdoc/>
    public async Task<DatabaseSchemaDetails> LoadDetailsAsync(
        string connectionString,
        IReadOnlyList<DatabaseObjectSchema> filteredObjects,
        CancellationToken cancellationToken = default
    ) {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        if (filteredObjects.Count == 0) {
            return DatabaseSchemaDetails.Empty;
        }

        using DbConnection connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        string objectInClause = BuildObjectInClause(filteredObjects);
        string tableInClause = BuildTableInClause(filteredObjects);

        IReadOnlyList<DatabaseColumnSchema> columns = [
            ..await connection.QueryAsync<DatabaseColumnSchema>(
                new CommandDefinition(BuildQueryColumnsSql(objectInClause), cancellationToken: cancellationToken)
            ).ConfigureAwait(false)
        ];

        IReadOnlyList<DatabaseIndexSchema> indexes = [
            ..await connection.QueryAsync<DatabaseIndexSchema>(
                new CommandDefinition(BuildQueryIndexesSql(tableInClause), cancellationToken: cancellationToken)
            ).ConfigureAwait(false)
        ];

        IReadOnlyList<DatabaseRoutineSchema> routines = [
            ..await connection.QueryAsync<DatabaseRoutineSchema>(
                new CommandDefinition(QueryRoutinesSql, cancellationToken: cancellationToken)
            ).ConfigureAwait(false)
        ];

        List<ExportDiagnostic> diagnostics = [];
        IReadOnlyList<DatabaseForeignKeySchema> relationships = [];
        try {
            relationships = [
                ..await connection.QueryAsync<DatabaseForeignKeySchema>(
                    new CommandDefinition(
                        BuildQueryRelationshipsSql(tableInClause),
                        cancellationToken: cancellationToken
                    )
                ).ConfigureAwait(false)
            ];
        } catch (SqlException exception) when (IsForeignKeyCatalogPermissionDenied(exception.Number)) {
            diagnostics.Add(new ExportDiagnostic {
                Severity = DiagnosticSeverity.Warning,
                Category = ExportDiagnosticCategory.Execution,
                Message = "FK catalog permission denied; relationships were not retrieved."
            });
        }

        return new DatabaseSchemaDetails {
            Columns = columns,
            Indexes = indexes,
            Routines = routines,
            Relationships = relationships,
            Diagnostics = diagnostics
        };
    }

    internal static bool IsForeignKeyCatalogPermissionDenied(int errorNumber) {
        // SQL Server errors 229, 230, 262 and 297 indicate permission failures for catalog access.
        return errorNumber is 229 or 230 or 262 or 297;
    }

    private static string BuildObjectInClause(IReadOnlyList<DatabaseObjectSchema> objects) {
        string inList = string.Join(
            ", ",
            objects.Select(o => $"N'{o.SchemaName.Replace("'", "''")}' + '.' + N'{o.ObjectName.Replace("'", "''")}'")
        );

        return inList;
    }

    private static string BuildTableInClause(IReadOnlyList<DatabaseObjectSchema> objects) {
        IEnumerable<DatabaseObjectSchema> tables = objects.Where(
            o => string.Equals(o.ObjectType, "BASE TABLE", StringComparison.OrdinalIgnoreCase)
        );

        string inList = string.Join(
            ", ",
            tables.Select(o => $"N'{o.SchemaName.Replace("'", "''")}' + '.' + N'{o.ObjectName.Replace("'", "''")}'")
        );

        return inList;
    }

    private const string QueryObjectsSql = """
        SELECT
            s.name AS SchemaName,
            o.name AS ObjectName,
            CASE o.type
                WHEN 'U' THEN 'BASE TABLE'
                WHEN 'V' THEN 'VIEW'
            END AS ObjectType,
            COALESCE(CAST(ep.value AS nvarchar(max)), '') AS ObjectDescription
        FROM sys.objects AS o
        INNER JOIN sys.schemas AS s ON s.schema_id = o.schema_id
        LEFT JOIN sys.extended_properties AS ep
            ON ep.class = 1
            AND ep.major_id = o.object_id
            AND ep.minor_id = 0
            AND ep.name = 'MS_Description'
        WHERE o.type IN ('U', 'V')
            AND o.is_ms_shipped = 0
        ORDER BY s.name, o.name;
        """;

    private static string BuildQueryColumnsSql(string objectInClause) => $"""
        SELECT
            s.name AS SchemaName,
            o.name AS ObjectName,
            CASE o.type
                WHEN 'U' THEN 'BASE TABLE'
                WHEN 'V' THEN 'VIEW'
            END AS ObjectType,
            c.name AS ColumnName,
            CASE
                WHEN st.name IN ('char', 'varchar', 'nchar', 'nvarchar') THEN
                    st.name + '('
                    + CASE
                        WHEN c.max_length = -1 THEN 'MAX'
                        WHEN st.name IN ('nchar', 'nvarchar') THEN CAST(c.max_length / 2 AS varchar(10))
                        ELSE CAST(c.max_length AS varchar(10))
                    END + ')'
                WHEN st.name IN ('decimal', 'numeric') THEN
                    st.name + '(' + CAST(c.precision AS varchar(10)) + ',' + CAST(c.scale AS varchar(10)) + ')'
                WHEN st.name IN ('datetime2', 'datetimeoffset', 'time') THEN
                    st.name + '(' + CAST(c.scale AS varchar(10)) + ')'
                WHEN st.name IN ('binary', 'varbinary') THEN
                    st.name + '(' + CASE WHEN c.max_length = -1 THEN 'MAX' ELSE CAST(c.max_length AS varchar(10)) END + ')'
                ELSE st.name
            END AS ColumnType,
            CASE WHEN c.is_nullable = 1 THEN 'Yes' ELSE 'No' END AS IsNullable,
            COALESCE(dc.definition, '') AS ColumnDefault,
            CASE
                WHEN EXISTS (
                    SELECT 1
                    FROM sys.index_columns AS ic
                    INNER JOIN sys.indexes AS i
                        ON i.object_id = ic.object_id
                        AND i.index_id = ic.index_id
                    WHERE ic.object_id = c.object_id
                        AND ic.column_id = c.column_id
                        AND i.is_primary_key = 1
                ) THEN 'Yes'
                ELSE 'No'
            END AS IsPrimaryKey,
            CASE WHEN c.is_identity = 1 THEN 'Yes' ELSE 'No' END AS IsIdentity,
            COALESCE(CAST(ep.value AS nvarchar(max)), '') AS ColumnDescription,
            c.column_id AS ColumnOrder
        FROM sys.columns AS c
        INNER JOIN sys.objects AS o
            ON o.object_id = c.object_id
            AND o.type IN ('U', 'V')
            AND o.is_ms_shipped = 0
        INNER JOIN sys.schemas AS s ON s.schema_id = o.schema_id
        INNER JOIN sys.types AS st ON st.user_type_id = c.user_type_id
        LEFT JOIN sys.default_constraints AS dc ON dc.object_id = c.default_object_id
        LEFT JOIN sys.extended_properties AS ep
            ON ep.class = 1
            AND ep.major_id = c.object_id
            AND ep.minor_id = c.column_id
            AND ep.name = 'MS_Description'
        WHERE s.name + '.' + o.name IN ({objectInClause})
        ORDER BY s.name, o.name, c.column_id;
        """;

    private static string BuildQueryIndexesSql(string tableInClause) {
        if (string.IsNullOrEmpty(tableInClause)) {
            return "SELECT NULL AS SchemaName, NULL AS ObjectName, NULL AS ObjectType, NULL AS IndexName, NULL AS IsPrimaryKey, NULL AS IsClustered, NULL AS IsUnique, NULL AS Columns, NULL AS IncludedColumns WHERE 1 = 0";
        }

        return $"""
            SELECT
                s.name AS SchemaName,
                t.name AS ObjectName,
                'BASE TABLE' AS ObjectType,
                ind.name AS IndexName,
                CASE WHEN ind.is_primary_key = 1 THEN 'Yes' ELSE 'No' END AS IsPrimaryKey,
                CASE WHEN ind.type_desc = 'CLUSTERED' THEN 'Yes' ELSE 'No' END AS IsClustered,
                CASE WHEN ind.is_unique = 1 THEN 'Yes' ELSE 'No' END AS IsUnique,
                COALESCE(STUFF((
                    SELECT CHAR(10) + COL_NAME(ic.object_id, ic.column_id)
                    FROM sys.index_columns AS ic
                    WHERE ic.object_id = ind.object_id
                        AND ic.index_id = ind.index_id
                        AND ic.is_included_column = 0
                    ORDER BY ic.key_ordinal, ic.index_column_id
                    FOR XML PATH(''), TYPE
                ).value('.', 'nvarchar(max)'), 1, 1, ''), '') AS Columns,
                COALESCE(STUFF((
                    SELECT ',' + CHAR(10) + COL_NAME(ic.object_id, ic.column_id)
                    FROM sys.index_columns AS ic
                    WHERE ic.object_id = ind.object_id
                        AND ic.index_id = ind.index_id
                        AND ic.is_included_column = 1
                    ORDER BY ic.index_column_id
                    FOR XML PATH(''), TYPE
                ).value('.', 'nvarchar(max)'), 1, 2, ''), '') AS IncludedColumns
            FROM sys.indexes AS ind
            INNER JOIN sys.tables AS t ON t.object_id = ind.object_id
            INNER JOIN sys.schemas AS s ON s.schema_id = t.schema_id
            WHERE t.is_ms_shipped = 0
                AND ind.name IS NOT NULL
                AND s.name + '.' + t.name IN ({tableInClause})

            ORDER BY SchemaName, ObjectName, IndexName;
        """;
    }

    private static string BuildQueryRelationshipsSql(string tableInClause) {
        if (string.IsNullOrEmpty(tableInClause)) {
            return "SELECT NULL AS ForeignKeyName, NULL AS SourceSchemaName, NULL AS SourceTableName, NULL AS SourceColumnName, NULL AS ReferencedSchemaName, NULL AS ReferencedTableName, NULL AS ReferencedColumnName, NULL AS ColumnOrder WHERE 1 = 0";
        }

        return $"""
            SELECT
                fk.name AS ForeignKeyName,
                sourceSchema.name AS SourceSchemaName,
                sourceTable.name AS SourceTableName,
                sourceColumn.name AS SourceColumnName,
                referencedSchema.name AS ReferencedSchemaName,
                referencedTable.name AS ReferencedTableName,
                referencedColumn.name AS ReferencedColumnName,
                fkc.constraint_column_id AS ColumnOrder
            FROM sys.foreign_keys AS fk
            INNER JOIN sys.foreign_key_columns AS fkc
                ON fkc.constraint_object_id = fk.object_id
            INNER JOIN sys.tables AS sourceTable
                ON sourceTable.object_id = fkc.parent_object_id
            INNER JOIN sys.schemas AS sourceSchema
                ON sourceSchema.schema_id = sourceTable.schema_id
            INNER JOIN sys.columns AS sourceColumn
                ON sourceColumn.object_id = fkc.parent_object_id
                AND sourceColumn.column_id = fkc.parent_column_id
            INNER JOIN sys.tables AS referencedTable
                ON referencedTable.object_id = fkc.referenced_object_id
            INNER JOIN sys.schemas AS referencedSchema
                ON referencedSchema.schema_id = referencedTable.schema_id
            INNER JOIN sys.columns AS referencedColumn
                ON referencedColumn.object_id = fkc.referenced_object_id
                AND referencedColumn.column_id = fkc.referenced_column_id
            WHERE sourceTable.is_ms_shipped = 0
                AND sourceSchema.name + '.' + sourceTable.name IN ({tableInClause})
            ORDER BY
                sourceSchema.name,
                sourceTable.name,
                fk.name,
                fkc.constraint_column_id;
        """;
    }

    private const string QueryRoutinesSql = """
        SELECT
            s.name AS SchemaName,
            CAST('' AS nvarchar(128)) AS ContainerName,
            o.name AS RoutineName,
            CASE o.type
                WHEN 'P' THEN 'PROCEDURE'
                WHEN 'FN' THEN 'FUNCTION'
                WHEN 'IF' THEN 'FUNCTION'
                WHEN 'TF' THEN 'FUNCTION'
            END AS RoutineType,
            CAST('' AS nvarchar(40)) AS OverloadIdentifier,
            COALESCE(STUFF((
                SELECT ', ' + CONCAT(
                    p.name,
                    ' ',
                    CASE
                        WHEN st.is_user_defined = 1 THEN SCHEMA_NAME(st.schema_id) + '.' + st.name
                        WHEN st.name IN ('char', 'varchar', 'nchar', 'nvarchar') THEN
                            st.name + '('
                            + CASE
                                WHEN p.max_length = -1 THEN 'MAX'
                                WHEN st.name IN ('nchar', 'nvarchar') THEN CAST(p.max_length / 2 AS varchar(10))
                                ELSE CAST(p.max_length AS varchar(10))
                            END + ')'
                        WHEN st.name IN ('decimal', 'numeric') THEN
                            st.name + '(' + CAST(p.precision AS varchar(10)) + ',' + CAST(p.scale AS varchar(10)) + ')'
                        WHEN st.name IN ('datetime2', 'datetimeoffset', 'time') THEN
                            st.name + '(' + CAST(p.scale AS varchar(10)) + ')'
                        WHEN st.name IN ('binary', 'varbinary') THEN
                            st.name + '(' + CASE WHEN p.max_length = -1 THEN 'MAX' ELSE CAST(p.max_length AS varchar(10)) END + ')'
                        ELSE st.name
                    END,
                    CASE WHEN p.is_output = 1 THEN ' OUTPUT' ELSE '' END,
                    CASE WHEN p.is_readonly = 1 THEN ' READONLY' ELSE '' END
                )
                FROM sys.parameters AS p
                INNER JOIN sys.types AS st ON st.user_type_id = p.user_type_id
                WHERE p.object_id = o.object_id
                    AND p.parameter_id > 0
                ORDER BY p.parameter_id
                FOR XML PATH(''), TYPE
            ).value('.', 'nvarchar(max)'), 1, 2, ''), '') AS ParameterSignature,
            CASE
                WHEN o.type IN ('IF', 'TF') THEN 'TABLE'
                WHEN o.type = 'FN' THEN COALESCE((
                    SELECT TOP (1)
                        CASE
                            WHEN st.is_user_defined = 1 THEN SCHEMA_NAME(st.schema_id) + '.' + st.name
                            WHEN st.name IN ('char', 'varchar', 'nchar', 'nvarchar') THEN
                                st.name + '('
                                + CASE
                                    WHEN p.max_length = -1 THEN 'MAX'
                                    WHEN st.name IN ('nchar', 'nvarchar') THEN CAST(p.max_length / 2 AS varchar(10))
                                    ELSE CAST(p.max_length AS varchar(10))
                                END + ')'
                            WHEN st.name IN ('decimal', 'numeric') THEN
                                st.name + '(' + CAST(p.precision AS varchar(10)) + ',' + CAST(p.scale AS varchar(10)) + ')'
                            WHEN st.name IN ('datetime2', 'datetimeoffset', 'time') THEN
                                st.name + '(' + CAST(p.scale AS varchar(10)) + ')'
                            WHEN st.name IN ('binary', 'varbinary') THEN
                                st.name + '(' + CASE WHEN p.max_length = -1 THEN 'MAX' ELSE CAST(p.max_length AS varchar(10)) END + ')'
                            ELSE st.name
                        END
                    FROM sys.parameters AS p
                    INNER JOIN sys.types AS st ON st.user_type_id = p.user_type_id
                    WHERE p.object_id = o.object_id
                        AND p.parameter_id = 0
                ), '')
                ELSE ''
            END AS ReturnType,
            COALESCE(CAST(ep.value AS nvarchar(max)), '') AS RoutineDescription,
            COALESCE(sm.definition, '') AS RoutineDefinition
        FROM sys.objects AS o
        INNER JOIN sys.schemas AS s ON s.schema_id = o.schema_id
        LEFT JOIN sys.extended_properties AS ep
            ON ep.class = 1
            AND ep.major_id = o.object_id
            AND ep.minor_id = 0
            AND ep.name = 'MS_Description'
        LEFT JOIN sys.sql_modules AS sm ON sm.object_id = o.object_id
        WHERE o.type IN ('P', 'FN', 'IF', 'TF')
            AND o.is_ms_shipped = 0
        ORDER BY s.name, o.type, o.name;
        """;
}
