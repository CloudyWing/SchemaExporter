#nullable enable

using System.Data.Common;
using Dapper;
using Microsoft.Data.SqlClient;

namespace CloudyWing.SchemaExporter.SchemaProviders;

internal sealed class SqlServerDatabaseSchemaProvider : IDatabaseSchemaProvider {
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

    private const string QueryColumnsSql = """
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
        ORDER BY s.name, o.name, c.column_id;
        """;

    private const string QueryIndexesSql = """
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

        ORDER BY SchemaName, ObjectName, IndexName;
    """;

    public DatabaseType DatabaseType => DatabaseType.SqlServer;

    public async Task<DatabaseSchemaExport> LoadSchemaAsync(
        string connectionString,
        CancellationToken cancellationToken = default
    ) {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString, nameof(connectionString));

        using DbConnection connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        IReadOnlyList<DatabaseObjectSchema> objects = [
            ..await connection.QueryAsync<DatabaseObjectSchema>(
                new CommandDefinition(QueryObjectsSql, cancellationToken: cancellationToken)
            ).ConfigureAwait(false)
        ];
        IReadOnlyList<DatabaseColumnSchema> columns = [
            ..await connection.QueryAsync<DatabaseColumnSchema>(
                new CommandDefinition(QueryColumnsSql, cancellationToken: cancellationToken)
            ).ConfigureAwait(false)
        ];
        IReadOnlyList<DatabaseIndexSchema> indexes = [
            ..await connection.QueryAsync<DatabaseIndexSchema>(
                new CommandDefinition(QueryIndexesSql, cancellationToken: cancellationToken)
            ).ConfigureAwait(false)
        ];

        return new DatabaseSchemaExport {
            Objects = objects,
            Columns = columns,
            Indexes = indexes
        };
    }
}
