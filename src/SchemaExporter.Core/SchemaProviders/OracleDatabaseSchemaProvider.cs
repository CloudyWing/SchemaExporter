using System.Data.Common;
using Dapper;
using Oracle.ManagedDataAccess.Client;

namespace CloudyWing.SchemaExporter.SchemaProviders;

internal sealed class OracleDatabaseSchemaProvider : IDatabaseSchemaProvider {
    private const string QueryObjectsSql = """
        WITH object_info AS (
            SELECT owner, table_name AS object_name, 'BASE TABLE' AS object_type
            FROM all_tables
            WHERE owner NOT IN ('SYS', 'SYSTEM')

            UNION ALL

            SELECT owner, view_name AS object_name, 'VIEW' AS object_type
            FROM all_views
            WHERE owner NOT IN ('SYS', 'SYSTEM')
        )
        SELECT
            object_info.owner AS SchemaName,
            object_info.object_name AS ObjectName,
            object_info.object_type AS ObjectType,
            NVL(comments.comments, '') AS ObjectDescription
        FROM object_info
        LEFT JOIN all_tab_comments comments
            ON comments.owner = object_info.owner
            AND comments.table_name = object_info.object_name
        ORDER BY object_info.owner, object_info.object_name;
        """;

    private const string QueryColumnsSql = """
        WITH object_info AS (
            SELECT owner, table_name AS object_name, 'BASE TABLE' AS object_type
            FROM all_tables
            WHERE owner NOT IN ('SYS', 'SYSTEM')

            UNION ALL

            SELECT owner, view_name AS object_name, 'VIEW' AS object_type
            FROM all_views
            WHERE owner NOT IN ('SYS', 'SYSTEM')
        ),
        primary_key_columns AS (
            SELECT acc.owner, acc.table_name, acc.column_name
            FROM all_constraints ac
            INNER JOIN all_cons_columns acc
                ON acc.owner = ac.owner
                AND acc.table_name = ac.table_name
                AND acc.constraint_name = ac.constraint_name
            WHERE ac.constraint_type = 'P'
        )
        SELECT
            c.owner AS SchemaName,
            c.table_name AS ObjectName,
            o.object_type AS ObjectType,
            c.column_name AS ColumnName,
            CASE
                WHEN c.data_type IN ('CHAR', 'NCHAR', 'VARCHAR2', 'NVARCHAR2') THEN
                    c.data_type || '(' || c.char_length || ')'
                WHEN c.data_type = 'NUMBER' AND c.data_precision IS NOT NULL AND c.data_scale IS NOT NULL THEN
                    c.data_type || '(' || c.data_precision || ',' || c.data_scale || ')'
                WHEN c.data_type = 'NUMBER' AND c.data_precision IS NOT NULL THEN
                    c.data_type || '(' || c.data_precision || ')'
                WHEN c.data_type LIKE 'TIMESTAMP%' AND c.data_scale IS NOT NULL THEN
                    c.data_type || '(' || c.data_scale || ')'
                WHEN c.data_type IN ('RAW') THEN
                    c.data_type || '(' || c.data_length || ')'
                ELSE c.data_type
            END AS ColumnType,
            CASE WHEN c.nullable = 'Y' THEN 'Yes' ELSE 'No' END AS IsNullable,
            '' AS ColumnDefault,
            CASE WHEN pk.column_name IS NOT NULL THEN 'Yes' ELSE 'No' END AS IsPrimaryKey,
            CASE WHEN NVL(c.identity_column, 'NO') = 'YES' THEN 'Yes' ELSE 'No' END AS IsIdentity,
            NVL(comments.comments, '') AS ColumnDescription,
            c.column_id AS ColumnOrder
        FROM all_tab_columns c
        INNER JOIN object_info o
            ON o.owner = c.owner
            AND o.object_name = c.table_name
        LEFT JOIN primary_key_columns pk
            ON pk.owner = c.owner
            AND pk.table_name = c.table_name
            AND pk.column_name = c.column_name
        LEFT JOIN all_col_comments comments
            ON comments.owner = c.owner
            AND comments.table_name = c.table_name
            AND comments.column_name = c.column_name
        ORDER BY c.owner, c.table_name, c.column_id;
        """;

    private const string QueryIndexesSql = """
        SELECT
            i.table_owner AS SchemaName,
            i.table_name AS ObjectName,
            'BASE TABLE' AS ObjectType,
            i.index_name AS IndexName,
            CASE WHEN pk.constraint_name IS NOT NULL THEN 'Yes' ELSE 'No' END AS IsPrimaryKey,
            'No' AS IsClustered,
            CASE WHEN i.uniqueness = 'UNIQUE' THEN 'Yes' ELSE 'No' END AS IsUnique,
            'No' AS IsForeignKey,
            NVL(LISTAGG(ic.column_name, CHR(10)) WITHIN GROUP (ORDER BY ic.column_position), '') AS Columns,
            '' AS OtherColumns
        FROM all_indexes i
        INNER JOIN all_ind_columns ic
            ON ic.index_owner = i.owner
            AND ic.index_name = i.index_name
            AND ic.table_owner = i.table_owner
            AND ic.table_name = i.table_name
        LEFT JOIN all_constraints pk
            ON pk.owner = i.table_owner
            AND pk.table_name = i.table_name
            AND pk.index_name = i.index_name
            AND pk.constraint_type = 'P'
        WHERE i.table_owner NOT IN ('SYS', 'SYSTEM')
            AND i.generated = 'N'
        GROUP BY
            i.table_owner,
            i.table_name,
            i.index_name,
            pk.constraint_name,
            i.uniqueness

        UNION ALL

        SELECT
            fk.owner AS SchemaName,
            fk.table_name AS ObjectName,
            'BASE TABLE' AS ObjectType,
            fk.constraint_name AS IndexName,
            'No' AS IsPrimaryKey,
            'No' AS IsClustered,
            'No' AS IsUnique,
            'Yes' AS IsForeignKey,
            NVL(LISTAGG(fkc.column_name, CHR(10)) WITHIN GROUP (ORDER BY fkc.position), '') AS Columns,
            ref.owner || '.' || ref.table_name || ':' || CHR(10)
                || NVL(LISTAGG(rcc.column_name, ',' || CHR(10)) WITHIN GROUP (ORDER BY rcc.position), '') AS OtherColumns
        FROM all_constraints fk
        INNER JOIN all_cons_columns fkc
            ON fkc.owner = fk.owner
            AND fkc.table_name = fk.table_name
            AND fkc.constraint_name = fk.constraint_name
        INNER JOIN all_constraints ref
            ON ref.owner = fk.r_owner
            AND ref.constraint_name = fk.r_constraint_name
        INNER JOIN all_cons_columns rcc
            ON rcc.owner = ref.owner
            AND rcc.constraint_name = ref.constraint_name
            AND rcc.position = fkc.position
        WHERE fk.constraint_type = 'R'
            AND fk.owner NOT IN ('SYS', 'SYSTEM')
        GROUP BY
            fk.owner,
            fk.table_name,
            fk.constraint_name,
            ref.owner,
            ref.table_name

        ORDER BY SchemaName, ObjectName, IndexName;
    """;

    private const string QueryRoutinesSql = """
        SELECT
            p.owner AS SchemaName,
            CASE
                WHEN p.procedure_name IS NULL THEN ''
                ELSE p.object_name
            END AS ContainerName,
            CASE
                WHEN p.procedure_name IS NULL THEN p.object_name
                ELSE p.procedure_name
            END AS RoutineName,
            CASE
                WHEN EXISTS (
                    SELECT 1
                    FROM all_arguments ret
                    WHERE ret.owner = p.owner
                        AND NVL(ret.package_name, ' ') = NVL(CASE WHEN p.procedure_name IS NULL THEN NULL ELSE p.object_name END, ' ')
                        AND ret.object_name = CASE WHEN p.procedure_name IS NULL THEN p.object_name ELSE p.procedure_name END
                        AND ret.subprogram_id = p.subprogram_id
                        AND ret.position = 0
                        AND ret.data_level = 0
                ) THEN 'FUNCTION'
                ELSE 'PROCEDURE'
            END AS RoutineType,
            NVL(p.overload, '') AS OverloadIdentifier,
            NVL((
                SELECT LISTAGG(
                    TRIM(
                        NVL(arg.argument_name, '(unnamed)') || ' '
                        || NVL(arg.in_out, 'IN') || ' '
                        || CASE
                            WHEN arg.data_type IN ('CHAR', 'NCHAR', 'VARCHAR2', 'NVARCHAR2') THEN
                                arg.data_type || '(' || arg.char_length || ')'
                            WHEN arg.data_type = 'NUMBER' AND arg.data_precision IS NOT NULL AND arg.data_scale IS NOT NULL THEN
                                arg.data_type || '(' || arg.data_precision || ',' || arg.data_scale || ')'
                            WHEN arg.data_type = 'NUMBER' AND arg.data_precision IS NOT NULL THEN
                                arg.data_type || '(' || arg.data_precision || ')'
                            WHEN arg.data_type = 'RAW' THEN
                                arg.data_type || '(' || arg.data_length || ')'
                            WHEN arg.data_type LIKE 'TIMESTAMP%' AND arg.data_scale IS NOT NULL THEN
                                arg.data_type || '(' || arg.data_scale || ')'
                            WHEN arg.data_type IS NOT NULL THEN
                                arg.data_type
                            WHEN arg.type_owner IS NOT NULL AND arg.type_name IS NOT NULL AND arg.type_subname IS NOT NULL THEN
                                arg.type_owner || '.' || arg.type_name || '.' || arg.type_subname
                            WHEN arg.type_owner IS NOT NULL AND arg.type_name IS NOT NULL THEN
                                arg.type_owner || '.' || arg.type_name
                            ELSE NVL(arg.pls_type, '')
                        END
                    ),
                    ', '
                ) WITHIN GROUP (ORDER BY arg.position, arg.sequence)
                FROM all_arguments arg
                WHERE arg.owner = p.owner
                    AND NVL(arg.package_name, ' ') = NVL(CASE WHEN p.procedure_name IS NULL THEN NULL ELSE p.object_name END, ' ')
                    AND arg.object_name = CASE WHEN p.procedure_name IS NULL THEN p.object_name ELSE p.procedure_name END
                    AND arg.subprogram_id = p.subprogram_id
                    AND arg.data_level = 0
                    AND arg.position > 0
            ), '') AS ParameterSignature,
            NVL((
                SELECT MAX(
                    CASE
                        WHEN arg.data_type IN ('CHAR', 'NCHAR', 'VARCHAR2', 'NVARCHAR2') THEN
                            arg.data_type || '(' || arg.char_length || ')'
                        WHEN arg.data_type = 'NUMBER' AND arg.data_precision IS NOT NULL AND arg.data_scale IS NOT NULL THEN
                            arg.data_type || '(' || arg.data_precision || ',' || arg.data_scale || ')'
                        WHEN arg.data_type = 'NUMBER' AND arg.data_precision IS NOT NULL THEN
                            arg.data_type || '(' || arg.data_precision || ')'
                        WHEN arg.data_type = 'RAW' THEN
                            arg.data_type || '(' || arg.data_length || ')'
                        WHEN arg.data_type LIKE 'TIMESTAMP%' AND arg.data_scale IS NOT NULL THEN
                            arg.data_type || '(' || arg.data_scale || ')'
                        WHEN arg.data_type IS NOT NULL THEN
                            arg.data_type
                        WHEN arg.type_owner IS NOT NULL AND arg.type_name IS NOT NULL AND arg.type_subname IS NOT NULL THEN
                            arg.type_owner || '.' || arg.type_name || '.' || arg.type_subname
                        WHEN arg.type_owner IS NOT NULL AND arg.type_name IS NOT NULL THEN
                            arg.type_owner || '.' || arg.type_name
                        ELSE NVL(arg.pls_type, '')
                    END
                )
                FROM all_arguments arg
                WHERE arg.owner = p.owner
                    AND NVL(arg.package_name, ' ') = NVL(CASE WHEN p.procedure_name IS NULL THEN NULL ELSE p.object_name END, ' ')
                    AND arg.object_name = CASE WHEN p.procedure_name IS NULL THEN p.object_name ELSE p.procedure_name END
                    AND arg.subprogram_id = p.subprogram_id
                    AND arg.data_level = 0
                    AND arg.position = 0
            ), '') AS ReturnType,
            '' AS RoutineDescription,
            NVL((
                SELECT XMLCAST(
                    XMLAGG(XMLELEMENT(e, src.text) ORDER BY src.line).EXTRACT('//text()') AS CLOB
                )
                FROM all_source src
                WHERE src.owner = p.owner
                    AND src.name = p.object_name
                    AND src.type = CASE
                        WHEN p.procedure_name IS NULL THEN p.object_type
                        ELSE 'PACKAGE'
                    END
            ), TO_CLOB('')) AS RoutineDefinition
        FROM all_procedures p
        WHERE p.owner NOT IN ('SYS', 'SYSTEM')
            AND (p.object_type IN ('PROCEDURE', 'FUNCTION') OR p.procedure_name IS NOT NULL)
        ORDER BY
            p.owner,
            CASE WHEN p.procedure_name IS NULL THEN '' ELSE p.object_name END,
            CASE WHEN p.procedure_name IS NULL THEN p.object_name ELSE p.procedure_name END,
            NVL(p.overload, ''),
            p.subprogram_id;
        """;

    public DatabaseType DatabaseType => DatabaseType.Oracle;

    public async Task<DatabaseSchemaExport> LoadSchemaAsync(
        string connectionString,
        CancellationToken cancellationToken = default
    ) {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString, nameof(connectionString));

        using DbConnection connection = new OracleConnection(connectionString);
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
        IReadOnlyList<DatabaseRoutineSchema> routines = [
            ..await connection.QueryAsync<DatabaseRoutineSchema>(
                new CommandDefinition(QueryRoutinesSql, cancellationToken: cancellationToken)
            ).ConfigureAwait(false)
        ];

        return new DatabaseSchemaExport {
            Objects = objects,
            Columns = columns,
            Indexes = indexes,
            Routines = routines
        };
    }
}
