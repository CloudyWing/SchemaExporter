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
                    IsForeignKey = "NO",
                    Columns = "Id"
                }
            ],
            Routines = [
                new DatabaseRoutineSchema {
                    SchemaName = "dbo",
                    ContainerName = "",
                    RoutineName = "usp_GetUsers",
                    RoutineType = "PROCEDURE",
                    ParameterSignature = "@IsActive bit",
                    ReturnType = "",
                    RoutineDescription = "Returns users",
                    RoutineDefinition = "SELECT [Id], [Name] FROM [dbo].[Users];"
                }
            ]
        };
    }

    public static SchemaSnapshotDocument CreateSnapshotDocument(
        string outputFilePath,
        string objectDescription = "Current user table",
        bool includeNameColumn = true
    ) {
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
            SchemaVersion = 2,
            ExportedAt = DateTimeOffset.Parse("2024-01-01T00:00:00+00:00", System.Globalization.CultureInfo.InvariantCulture),
            ConnectionName = "Primary",
            DatabaseType = DatabaseType.SqlServer.ToString(),
            ProfileName = "Default",
            OutputFilePath = outputFilePath,
            Counts = new SchemaSnapshotCounts {
                Objects = 1,
                Columns = columns.Count,
                Indexes = 1,
                Routines = 1
            },
            Diagnostics = [],
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
                            IsForeignKey = "NO",
                            Columns = "Id",
                            OtherColumns = ""
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
            ]
        };
    }

    public static async Task WriteSnapshotAsync(
        string path,
        string objectDescription = "Current user table",
        bool includeNameColumn = true
    ) {
        SchemaSnapshotDocument snapshot = CreateSnapshotDocument(path, objectDescription, includeNameColumn);
        string json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions {
            WriteIndented = true
        });
        await File.WriteAllTextAsync(path, json);
    }
}

