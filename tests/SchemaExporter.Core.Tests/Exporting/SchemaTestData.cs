using System.Text.Json;
using CloudyWing.SchemaExporter.Core.Exporting;
using CloudyWing.SchemaExporter.Core.Exporting.Snapshots;
using CloudyWing.SchemaExporter.Core.SchemaProviders;

namespace CloudyWing.SchemaExporter.Core.Tests.Exporting;

internal static class SchemaTestData {
    public static DatabaseSchemaExport CreateSchemaExport(
        string objectDescription = "Current user table",
        bool includeNameColumn = true
    ) {
        DatabaseObjectSchema databaseObject = new() {
            SchemaName = "dbo",
            ObjectName = "Users",
            ObjectType = "TABLE",
            ObjectDescription = objectDescription
        };

        List<DatabaseColumnSchema> columns = [
            new DatabaseColumnSchema {
                SchemaName = "dbo",
                ObjectName = "Users",
                ObjectType = "TABLE",
                ColumnName = "Id",
                ColumnType = "int",
                IsNullable = "NO",
                IsPrimaryKey = "YES",
                IsIdentity = "YES",
                ColumnOrder = 1
            }
        ];

        if (includeNameColumn) {
            columns.Add(new DatabaseColumnSchema {
                SchemaName = "dbo",
                ObjectName = "Users",
                ObjectType = "TABLE",
                ColumnName = "Name",
                ColumnType = "nvarchar(128)",
                IsNullable = "NO",
                ColumnDefault = "('unknown')",
                IsPrimaryKey = "NO",
                IsIdentity = "NO",
                ColumnDescription = "Display name",
                ColumnOrder = 2
            });
        }

        return new DatabaseSchemaExport {
            Objects = [databaseObject],
            Columns = columns,
            Indexes = [
                new DatabaseIndexSchema {
                    SchemaName = "dbo",
                    ObjectName = "Users",
                    ObjectType = "TABLE",
                    IndexName = "PK_Users",
                    IsPrimaryKey = "YES",
                    IsClustered = "YES",
                    IsUnique = "YES",
                    Columns = "Id"
                }
            ],
            Routines = [
                new DatabaseRoutineSchema {
                    SchemaName = "dbo",
                    RoutineName = "usp_GetUsers",
                    RoutineType = "PROCEDURE",
                    ParameterSignature = "@IsActive bit",
                    RoutineDescription = "Returns users",
                    RoutineDefinition = "SELECT [Id], [Name] FROM [dbo].[Users];"
                }
            ],
            Relationships = [
                new DatabaseForeignKeySchema {
                    ForeignKeyName = "FK_Users_Customers",
                    SourceSchemaName = "dbo",
                    SourceTableName = "Users",
                    SourceColumnName = "Id",
                    ReferencedSchemaName = "dbo",
                    ReferencedTableName = "Customers",
                    ReferencedColumnName = "Id",
                    ColumnOrder = 1
                }
            ]
        };
    }

    public static SchemaSnapshotDocument CreateSnapshotDocument(
        string outputFilePath,
        string objectDescription = "Current user table",
        bool includeNameColumn = true,
        IReadOnlyList<SchemaSnapshotRelationshipDocument>? relationshipOverride = null,
        IReadOnlyList<SchemaSnapshotDiagnostic>? diagnosticOverride = null
    ) {
        IReadOnlyList<SchemaSnapshotRelationshipDocument> relationships = relationshipOverride ?? [
            new SchemaSnapshotRelationshipDocument {
                ForeignKeyName = "FK_Users_Customers",
                SourceSchemaName = "dbo",
                SourceTableName = "Users",
                SourceColumnName = "Id",
                ReferencedSchemaName = "dbo",
                ReferencedTableName = "Customers",
                ReferencedColumnName = "Id",
                ColumnOrder = 1
            }
        ];
        IReadOnlyList<SchemaSnapshotDiagnostic> diagnostics = diagnosticOverride ?? [];
        List<SchemaSnapshotColumnDocument> columns = [
            new SchemaSnapshotColumnDocument {
                ColumnName = "Id",
                ColumnType = "int",
                IsNullable = "NO",
                ColumnDefault = "",
                IsPrimaryKey = "YES",
                IsIdentity = "YES",
                ColumnDescription = "",
                ColumnOrder = 1
            }
        ];

        if (includeNameColumn) {
            columns.Add(new SchemaSnapshotColumnDocument {
                ColumnName = "Name",
                ColumnType = "nvarchar(128)",
                IsNullable = "NO",
                ColumnDefault = "('unknown')",
                IsPrimaryKey = "NO",
                IsIdentity = "NO",
                ColumnDescription = "Display name",
                ColumnOrder = 2
            });
        }

        return new SchemaSnapshotDocument {
            SchemaVersion = 3,
            ExportedAt = DateTimeOffset.Parse("2024-01-01T00:00:00+00:00", System.Globalization.CultureInfo.InvariantCulture),
            ConnectionName = "Primary",
            DatabaseType = DatabaseType.SqlServer.ToString(),
            ProfileName = "Default",
            OutputFilePath = outputFilePath,
            Counts = new SchemaSnapshotCounts {
                Objects = 1,
                Columns = columns.Count,
                Indexes = 1,
                Routines = 1,
                Relationships = relationships.Count
            },
            Diagnostics = diagnostics,
            Objects = [
                new SchemaSnapshotObjectDocument {
                    SchemaName = "dbo",
                    ObjectName = "Users",
                    ObjectType = "TABLE",
                    ObjectDescription = objectDescription,
                    Columns = columns,
                    Indexes = [
                        new SchemaSnapshotIndexDocument {
                            IndexName = "PK_Users",
                            IsPrimaryKey = "YES",
                            IsClustered = "YES",
                            IsUnique = "YES",
                            Columns = "Id",
                            IncludedColumns = ""
                        }
                    ]
                }
            ],
            Routines = [
                new SchemaSnapshotRoutineDocument {
                    SchemaName = "dbo",
                    ContainerName = "",
                    RoutineName = "usp_GetUsers",
                    RoutineType = "PROCEDURE",
                    OverloadIdentifier = "",
                    ParameterSignature = "@IsActive bit",
                    ReturnType = "",
                    RoutineDescription = "Returns users",
                    RoutineDefinition = "SELECT [Id], [Name] FROM [dbo].[Users];"
                }
            ],
            Relationships = relationships
        };
    }

    public static async Task WriteSnapshotAsync(
        string path,
        string objectDescription = "Current user table",
        bool includeNameColumn = true,
        IReadOnlyList<SchemaSnapshotRelationshipDocument>? relationshipOverride = null,
        IReadOnlyList<SchemaSnapshotDiagnostic>? diagnosticOverride = null
    ) {
        SchemaSnapshotDocument snapshot = CreateSnapshotDocument(
            path,
            objectDescription,
            includeNameColumn,
            relationshipOverride,
            diagnosticOverride
        );
        string json = JsonSerializer.Serialize(snapshot, SchemaArtifactJsonSerializerOptions.Default);
        await File.WriteAllTextAsync(path, json);
    }
}

