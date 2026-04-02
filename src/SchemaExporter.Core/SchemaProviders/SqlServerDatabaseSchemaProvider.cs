using System.Data.Common;
using Dapper;
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

        return new DatabaseSchemaDetails {
            Columns = columns,
            Indexes = indexes,
            Routines = routines
        };
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
            return "SELECT NULL AS SchemaName, NULL AS ObjectName, NULL AS ObjectType, NULL AS IndexName, NULL AS IsPrimaryKey, NULL AS IsClustered, NULL AS IsUnique, NULL AS IsForeignKey, NULL AS Columns, NULL AS OtherColumns WHERE 1 = 0";
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
                'No' AS IsForeignKey,
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
                ).value('.', 'nvarchar(max)'), 1, 2, ''), '') AS OtherColumns
            FROM sys.indexes AS ind
            INNER JOIN sys.tables AS t ON t.object_id = ind.object_id
            INNER JOIN sys.schemas AS s ON s.schema_id = t.schema_id
            WHERE t.is_ms_shipped = 0
                AND ind.name IS NOT NULL
                AND s.name + '.' + t.name IN ({tableInClause})

            UNION ALL

            SELECT DISTINCT
                s.name AS SchemaName,
                t.name AS ObjectName,
                'BASE TABLE' AS ObjectType,
                fk.name AS IndexName,
                'No' AS IsPrimaryKey,
                'No' AS IsClustered,
                'No' AS IsUnique,
                'Yes' AS IsForeignKey,
                COALESCE(STUFF((
                    SELECT CHAR(10) + COL_NAME(fkc1.parent_object_id, fkc1.parent_column_id)
                    FROM sys.foreign_key_columns AS fkc1
                    WHERE fkc1.constraint_object_id = fk.object_id
                    ORDER BY fkc1.constraint_column_id
                    FOR XML PATH(''), TYPE
                ).value('.', 'nvarchar(max)'), 1, 1, ''), '') AS Columns,
                CONCAT(
                    rs.name,
                    '.',
                    rt.name,
                    ':',
                    CHAR(10),
                    COALESCE(STUFF((
                        SELECT ',' + CHAR(10) + COL_NAME(fkc2.referenced_object_id, fkc2.referenced_column_id)
                        FROM sys.foreign_key_columns AS fkc2
                        WHERE fkc2.constraint_object_id = fk.object_id
                        ORDER BY fkc2.constraint_column_id
                        FOR XML PATH(''), TYPE
                    ).value('.', 'nvarchar(max)'), 1, 2, ''), '')
                ) AS OtherColumns
            FROM sys.foreign_keys AS fk
            INNER JOIN sys.foreign_key_columns AS fkc ON fkc.constraint_object_id = fk.object_id
            INNER JOIN sys.tables AS t ON t.object_id = fk.parent_object_id
            INNER JOIN sys.schemas AS s ON s.schema_id = t.schema_id
            INNER JOIN sys.tables AS rt ON rt.object_id = fk.referenced_object_id
            INNER JOIN sys.schemas AS rs ON rs.schema_id = rt.schema_id
            WHERE t.is_ms_shipped = 0
                AND s.name + '.' + t.name IN ({tableInClause})

            ORDER BY SchemaName, ObjectName, IndexName;
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
