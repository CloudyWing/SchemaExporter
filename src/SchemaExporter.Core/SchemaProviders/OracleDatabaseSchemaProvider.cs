using System.Data.Common;
using Dapper;
using Oracle.ManagedDataAccess.Client;

namespace CloudyWing.SchemaExporter.Core.SchemaProviders;

/// <summary>
/// 提供 Oracle 資料庫結構描述的載入實作。
/// </summary>
internal sealed class OracleDatabaseSchemaProvider : IDatabaseSchemaProvider {
    /// <inheritdoc/>
    public DatabaseType DatabaseType => DatabaseType.Oracle;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<DatabaseObjectSchema>> LoadObjectsAsync(
        string connectionString,
        CancellationToken cancellationToken = default
    ) {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        using DbConnection connection = new OracleConnection(connectionString);
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

        using DbConnection connection = new OracleConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        bool hasIdentityColumn = await connection.QuerySingleAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM all_tab_columns WHERE owner = 'SYS' AND table_name = 'ALL_TAB_COLUMNS' AND column_name = 'IDENTITY_COLUMN'",
                cancellationToken: cancellationToken
            )
        ).ConfigureAwait(false) > 0;

        bool hasListaggOverflow = await connection.QuerySingleAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM all_tab_columns WHERE owner = 'SYS' AND table_name = 'ALL_OBJECTS' AND column_name = 'SHARING'",
                cancellationToken: cancellationToken
            )
        ).ConfigureAwait(false) > 0;

        string objectInClause = BuildObjectInClause(filteredObjects);
        string tableInClause = BuildTableInClause(filteredObjects);

        IReadOnlyList<DatabaseColumnSchema> columns = [
            ..await connection.QueryAsync<DatabaseColumnSchema>(
                new CommandDefinition(
                    BuildQueryColumnsSql(hasIdentityColumn, objectInClause),
                    cancellationToken: cancellationToken
                )
            ).ConfigureAwait(false)
        ];

        IReadOnlyList<DatabaseIndexSchema> indexes = [
            ..await connection.QueryAsync<DatabaseIndexSchema>(
                new CommandDefinition(
                    BuildQueryIndexesSql(tableInClause),
                    cancellationToken: cancellationToken
                )
            ).ConfigureAwait(false)
        ];

        List<DatabaseRoutineSchema> routines = [
            ..await connection.QueryAsync<DatabaseRoutineSchema>(
                new CommandDefinition(
                    BuildQueryRoutinesSql(hasListaggOverflow),
                    cancellationToken: cancellationToken
                )
            ).ConfigureAwait(false)
        ];

        if (!hasListaggOverflow) {
            await FillRoutineDefinitionsAsync(connection, routines, cancellationToken).ConfigureAwait(false);
        }

        return new DatabaseSchemaDetails {
            Columns = columns,
            Indexes = indexes,
            Routines = routines
        };
    }

    private static string BuildObjectInClause(IReadOnlyList<DatabaseObjectSchema> objects) {
        string inList = string.Join(", ", objects.Select(o => $"'{o.ObjectName.Replace("'", "''")}'"));
        return inList;
    }

    private static string BuildTableInClause(IReadOnlyList<DatabaseObjectSchema> objects) {
        IEnumerable<DatabaseObjectSchema> tables = objects.Where(
            o => string.Equals(o.ObjectType, "BASE TABLE", StringComparison.OrdinalIgnoreCase)
        );

        string inList = string.Join(", ", tables.Select(o => $"'{o.ObjectName.Replace("'", "''")}'"));
        return inList;
    }

    private const string QueryObjectsSql = """
        SELECT
            USER AS SchemaName,
            t.table_name AS ObjectName,
            'BASE TABLE' AS ObjectType,
            NVL(c.comments, '') AS ObjectDescription
        FROM user_tables t
        LEFT JOIN user_tab_comments c
            ON c.table_name = t.table_name
            AND c.table_type = 'TABLE'

        UNION ALL

        SELECT
            USER AS SchemaName,
            v.view_name AS ObjectName,
            'VIEW' AS ObjectType,
            NVL(c.comments, '') AS ObjectDescription
        FROM user_views v
        LEFT JOIN user_tab_comments c
            ON c.table_name = v.view_name
            AND c.table_type = 'VIEW'

        ORDER BY ObjectName
        """;

    private static string BuildQueryColumnsSql(bool hasIdentityColumn, string objectInClause) => $"""
        WITH object_info AS (
            SELECT table_name, 'BASE TABLE' AS object_type FROM user_tables
            WHERE table_name IN ({objectInClause})
            UNION ALL
            SELECT view_name, 'VIEW' AS object_type FROM user_views
            WHERE view_name IN ({objectInClause})
        ),
        primary_key_columns AS (
            SELECT acc.table_name, acc.column_name
            FROM user_constraints ac
            INNER JOIN user_cons_columns acc
                ON acc.table_name = ac.table_name
                AND acc.constraint_name = ac.constraint_name
            WHERE ac.constraint_type = 'P'
                AND ac.table_name IN ({objectInClause})
        )
        SELECT
            USER AS SchemaName,
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
            {(hasIdentityColumn ? "CASE WHEN NVL(c.identity_column, 'NO') = 'YES' THEN 'Yes' ELSE 'No' END" : "'No'")} AS IsIdentity,
            NVL(cm.comments, '') AS ColumnDescription,
            c.column_id AS ColumnOrder
        FROM user_tab_columns c
        INNER JOIN object_info o ON o.table_name = c.table_name
        LEFT JOIN primary_key_columns pk
            ON pk.table_name = c.table_name
            AND pk.column_name = c.column_name
        LEFT JOIN user_col_comments cm
            ON cm.table_name = c.table_name
            AND cm.column_name = c.column_name
        ORDER BY c.table_name, c.column_id
        """;

    private static string BuildQueryIndexesSql(string tableInClause) {
        if (string.IsNullOrEmpty(tableInClause)) {
            return "SELECT NULL AS SchemaName, NULL AS ObjectName, NULL AS ObjectType, NULL AS IndexName, NULL AS IsPrimaryKey, NULL AS IsClustered, NULL AS IsUnique, NULL AS IsForeignKey, NULL AS Columns, NULL AS OtherColumns FROM DUAL WHERE 1 = 0";
        }

        return $"""
            SELECT
                USER AS SchemaName,
                i.table_name AS ObjectName,
                'BASE TABLE' AS ObjectType,
                i.index_name AS IndexName,
                CASE WHEN pk.constraint_name IS NOT NULL THEN 'Yes' ELSE 'No' END AS IsPrimaryKey,
                'No' AS IsClustered,
                CASE WHEN i.uniqueness = 'UNIQUE' THEN 'Yes' ELSE 'No' END AS IsUnique,
                'No' AS IsForeignKey,
                NVL(LISTAGG(ic.column_name, CHR(10)) WITHIN GROUP (ORDER BY ic.column_position), '') AS Columns,
                '' AS OtherColumns
            FROM user_indexes i
            INNER JOIN user_ind_columns ic
                ON ic.index_name = i.index_name
                AND ic.table_name = i.table_name
            LEFT JOIN user_constraints pk
                ON pk.table_name = i.table_name
                AND pk.index_name = i.index_name
                AND pk.constraint_type = 'P'
            WHERE i.generated = 'N'
                AND i.table_name IN ({tableInClause})
            GROUP BY
                i.table_name,
                i.index_name,
                pk.constraint_name,
                i.uniqueness

            UNION ALL

            SELECT
                USER AS SchemaName,
                fk.table_name AS ObjectName,
                'BASE TABLE' AS ObjectType,
                fk.constraint_name AS IndexName,
                'No' AS IsPrimaryKey,
                'No' AS IsClustered,
                'No' AS IsUnique,
                'Yes' AS IsForeignKey,
                NVL(LISTAGG(fkc.column_name, CHR(10)) WITHIN GROUP (ORDER BY fkc.position), '') AS Columns,
                fk.r_owner || '.' || ref.table_name || ':' || CHR(10)
                    || NVL(LISTAGG(rcc.column_name, ',' || CHR(10)) WITHIN GROUP (ORDER BY rcc.position), '') AS OtherColumns
            FROM user_constraints fk
            INNER JOIN user_cons_columns fkc
                ON fkc.table_name = fk.table_name
                AND fkc.constraint_name = fk.constraint_name
            INNER JOIN all_constraints ref
                ON ref.owner = fk.r_owner
                AND ref.constraint_name = fk.r_constraint_name
            INNER JOIN all_cons_columns rcc
                ON rcc.owner = ref.owner
                AND rcc.constraint_name = ref.constraint_name
                AND rcc.position = fkc.position
            WHERE fk.constraint_type = 'R'
                AND fk.table_name IN ({tableInClause})
            GROUP BY
                fk.table_name,
                fk.constraint_name,
                fk.r_owner,
                ref.table_name

            ORDER BY SchemaName, ObjectName, IndexName
            """;
    }

    private static async Task FillRoutineDefinitionsAsync(
        DbConnection connection,
        List<DatabaseRoutineSchema> routines,
        CancellationToken cancellationToken
    ) {
        IEnumerable<(string Name, string Type, string Text)> sourceLines =
            await connection.QueryAsync<(string Name, string Type, string Text)>(
                new CommandDefinition(
                    "SELECT name, type, text FROM user_source ORDER BY name, type, line",
                    cancellationToken: cancellationToken
                )
            ).ConfigureAwait(false);

        Dictionary<string, string> sourceByKey = sourceLines
            .GroupBy(r => r.Name + "\x00" + r.Type)
            .ToDictionary(g => g.Key, g => string.Concat(g.Select(r => r.Text)));

        foreach (DatabaseRoutineSchema routine in routines) {
            string objectName = string.IsNullOrEmpty(routine.ContainerName)
                ? routine.RoutineName
                : routine.ContainerName;
            string sourceType = string.IsNullOrEmpty(routine.ContainerName)
                ? routine.RoutineType
                : "PACKAGE";
            string key = objectName + "\x00" + sourceType;
            if (sourceByKey.TryGetValue(key, out string? definition)) {
                routine.RoutineDefinition = definition;
            }
        }
    }

    private static string BuildQueryRoutinesSql(bool hasListaggOverflow) {
        string sourceAggCte = hasListaggOverflow
            ? """
              WITH source_agg AS (
                  SELECT name, type,
                      LISTAGG(text ON OVERFLOW TRUNCATE) WITHIN GROUP (ORDER BY line) AS definition
                  FROM user_source
                  GROUP BY name, type
              ),
              """
            : """
              WITH source_agg AS (
                  SELECT name, type, '' AS definition
                  FROM user_source
                  GROUP BY name, type
              ),
              """;

        return $"""
        {sourceAggCte}
        param_agg AS (
            SELECT
                NVL(package_name, ' ') AS package_name,
                object_name,
                subprogram_id,
                LISTAGG(
                    TRIM(
                        NVL(argument_name, '(unnamed)') || ' '
                        || NVL(in_out, 'IN') || ' '
                        || CASE
                            WHEN data_type IN ('CHAR', 'NCHAR', 'VARCHAR2', 'NVARCHAR2') THEN
                                data_type || '(' || char_length || ')'
                            WHEN data_type = 'NUMBER' AND data_precision IS NOT NULL AND data_scale IS NOT NULL THEN
                                data_type || '(' || data_precision || ',' || data_scale || ')'
                            WHEN data_type = 'NUMBER' AND data_precision IS NOT NULL THEN
                                data_type || '(' || data_precision || ')'
                            WHEN data_type = 'RAW' THEN
                                data_type || '(' || data_length || ')'
                            WHEN data_type LIKE 'TIMESTAMP%' AND data_scale IS NOT NULL THEN
                                data_type || '(' || data_scale || ')'
                            WHEN data_type IS NOT NULL THEN
                                data_type
                            WHEN type_owner IS NOT NULL AND type_name IS NOT NULL AND type_subname IS NOT NULL THEN
                                type_owner || '.' || type_name || '.' || type_subname
                            WHEN type_owner IS NOT NULL AND type_name IS NOT NULL THEN
                                type_owner || '.' || type_name
                            ELSE NVL(pls_type, '')
                        END
                    ),
                    ', '
                ) WITHIN GROUP (ORDER BY position, sequence) AS parameter_signature
            FROM user_arguments
            WHERE data_level = 0
                AND position > 0
            GROUP BY NVL(package_name, ' '), object_name, subprogram_id
        ),
        return_type_agg AS (
            SELECT
                NVL(package_name, ' ') AS package_name,
                object_name,
                subprogram_id,
                MAX(
                    CASE
                        WHEN data_type IN ('CHAR', 'NCHAR', 'VARCHAR2', 'NVARCHAR2') THEN
                            data_type || '(' || char_length || ')'
                        WHEN data_type = 'NUMBER' AND data_precision IS NOT NULL AND data_scale IS NOT NULL THEN
                            data_type || '(' || data_precision || ',' || data_scale || ')'
                        WHEN data_type = 'NUMBER' AND data_precision IS NOT NULL THEN
                            data_type || '(' || data_precision || ')'
                        WHEN data_type = 'RAW' THEN
                            data_type || '(' || data_length || ')'
                        WHEN data_type LIKE 'TIMESTAMP%' AND data_scale IS NOT NULL THEN
                            data_type || '(' || data_scale || ')'
                        WHEN data_type IS NOT NULL THEN
                            data_type
                        WHEN type_owner IS NOT NULL AND type_name IS NOT NULL AND type_subname IS NOT NULL THEN
                            type_owner || '.' || type_name || '.' || type_subname
                        WHEN type_owner IS NOT NULL AND type_name IS NOT NULL THEN
                            type_owner || '.' || type_name
                        ELSE NVL(pls_type, '')
                    END
                ) AS return_type
            FROM user_arguments
            WHERE data_level = 0
                AND position = 0
            GROUP BY NVL(package_name, ' '), object_name, subprogram_id
        ),
        return_types AS (
            SELECT DISTINCT object_name, NVL(package_name, ' ') AS package_name, subprogram_id
            FROM user_arguments
            WHERE position = 0
                AND data_level = 0
        )
        SELECT
            USER AS SchemaName,
            CASE
                WHEN p.procedure_name IS NULL THEN ''
                ELSE p.object_name
            END AS ContainerName,
            CASE
                WHEN p.procedure_name IS NULL THEN p.object_name
                ELSE p.procedure_name
            END AS RoutineName,
            CASE
                WHEN rt.object_name IS NOT NULL THEN 'FUNCTION'
                ELSE 'PROCEDURE'
            END AS RoutineType,
            NVL(p.overload, '') AS OverloadIdentifier,
            NVL(pa.parameter_signature, '') AS ParameterSignature,
            NVL(rta.return_type, '') AS ReturnType,
            '' AS RoutineDescription,
            NVL(sa.definition, '') AS RoutineDefinition
        FROM user_procedures p
        LEFT JOIN return_types rt
            ON rt.object_name = CASE WHEN p.procedure_name IS NULL THEN p.object_name ELSE p.procedure_name END
            AND rt.package_name = NVL(CASE WHEN p.procedure_name IS NULL THEN NULL ELSE p.object_name END, ' ')
            AND rt.subprogram_id = p.subprogram_id
        LEFT JOIN param_agg pa
            ON pa.object_name = CASE WHEN p.procedure_name IS NULL THEN p.object_name ELSE p.procedure_name END
            AND pa.package_name = NVL(CASE WHEN p.procedure_name IS NULL THEN NULL ELSE p.object_name END, ' ')
            AND pa.subprogram_id = p.subprogram_id
        LEFT JOIN return_type_agg rta
            ON rta.object_name = CASE WHEN p.procedure_name IS NULL THEN p.object_name ELSE p.procedure_name END
            AND rta.package_name = NVL(CASE WHEN p.procedure_name IS NULL THEN NULL ELSE p.object_name END, ' ')
            AND rta.subprogram_id = p.subprogram_id
        LEFT JOIN source_agg sa
            ON sa.name = p.object_name
            AND sa.type = CASE WHEN p.procedure_name IS NULL THEN p.object_type ELSE 'PACKAGE' END
        WHERE p.object_type IN ('PROCEDURE', 'FUNCTION')
            OR p.procedure_name IS NOT NULL
        ORDER BY
            CASE WHEN p.procedure_name IS NULL THEN '' ELSE p.object_name END,
            CASE WHEN p.procedure_name IS NULL THEN p.object_name ELSE p.procedure_name END,
            NVL(p.overload, ''),
            p.subprogram_id
        """;
    }
}
