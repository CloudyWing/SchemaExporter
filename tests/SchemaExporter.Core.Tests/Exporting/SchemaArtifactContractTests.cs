using System.Text.Json;
using CloudyWing.SchemaExporter.Core.Exporting;
using CloudyWing.SchemaExporter.Core.Exporting.Diffs;
using CloudyWing.SchemaExporter.Core.Exporting.Snapshots;

namespace CloudyWing.SchemaExporter.Core.Tests.Exporting;

[TestFixture]
public sealed class SchemaArtifactContractTests {
    [Test]
    public void Serialize_WhenManifestIsProvided_UsesStableCamelCaseContract() {
        ExportManifest manifest = new() {
            ExportedAt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            ConnectionName = "Primary",
            DatabaseType = "SqlServer",
            ProfileName = "Default",
            OutputFilePath = "C:/Exports/TableSchema_Primary.xlsx",
            ResultOptions = new ExportManifestResultOptions {
                UseTimestamp = false,
                TimestampFormat = "yyyyMMdd_HHmmss",
                OverwriteStrategy = "Overwrite",
                OpenOutputFolder = false,
                GenerateManifest = true,
                GenerateJsonSidecar = true,
                GenerateMarkdownSidecar = true,
                GenerateSchemaSummary = true,
                GenerateSchemaSnapshot = true,
                DiffSourceSnapshotPath = "C:/Exports/baseline.snapshot.json"
            },
            Counts = new ExportManifestCounts {
                Objects = 1,
                Columns = 2,
                Indexes = 1,
                Routines = 0
            },
            Diagnostics = [
                new ExportManifestDiagnostic {
                    Severity = "Warning",
                    Category = "ProviderCapability",
                    SupportLevel = "Partial",
                    AffectedObject = "dbo.Users",
                    Message = "Computed columns are not exported."
                }
            ]
        };
        string expectedJson = """
        {
          "exportedAt": "2024-01-01T00:00:00+00:00",
          "connectionName": "Primary",
          "databaseType": "SqlServer",
          "profileName": "Default",
          "outputFilePath": "C:/Exports/TableSchema_Primary.xlsx",
          "resultOptions": {
            "useTimestamp": false,
            "timestampFormat": "yyyyMMdd_HHmmss",
            "overwriteStrategy": "Overwrite",
            "openOutputFolder": false,
            "generateManifest": true,
            "generateJsonSidecar": true,
            "generateMarkdownSidecar": true,
            "generateSchemaSummary": true,
            "generateSchemaSnapshot": true,
            "diffSourceSnapshotPath": "C:/Exports/baseline.snapshot.json"
          },
          "counts": {
            "objects": 1,
            "columns": 2,
            "indexes": 1,
            "routines": 0
          },
          "diagnostics": [
            {
              "severity": "Warning",
              "category": "ProviderCapability",
              "supportLevel": "Partial",
              "affectedObject": "dbo.Users",
              "message": "Computed columns are not exported."
            }
          ]
        }
        """;

        string actualJson = JsonSerializer.Serialize(manifest, SchemaArtifactJsonSerializerOptions.Default);

        AssertJson(actualJson, expectedJson);
    }

    [Test]
    public void Serialize_WhenSnapshotIsProvided_UsesStableCamelCaseContract() {
        SchemaSnapshotDocument snapshot = new() {
            SchemaVersion = 2,
            ExportedAt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            ConnectionName = "Primary",
            DatabaseType = "SqlServer",
            ProfileName = "Default",
            OutputFilePath = "C:/Exports/TableSchema_Primary.xlsx",
            Counts = new SchemaSnapshotCounts {
                Objects = 1,
                Columns = 1,
                Indexes = 0,
                Routines = 0
            },
            Diagnostics = [],
            Objects = [
                new SchemaSnapshotObjectDocument {
                    SchemaName = "dbo",
                    ObjectName = "Users",
                    ObjectType = "TABLE",
                    ObjectDescription = "User table",
                    Columns = [
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
                    ],
                    Indexes = []
                }
            ],
            Routines = []
        };
        string expectedJson = """
        {
          "schemaVersion": 2,
          "exportedAt": "2024-01-01T00:00:00+00:00",
          "connectionName": "Primary",
          "databaseType": "SqlServer",
          "profileName": "Default",
          "outputFilePath": "C:/Exports/TableSchema_Primary.xlsx",
          "counts": {
            "objects": 1,
            "columns": 1,
            "indexes": 0,
            "routines": 0
          },
          "diagnostics": [],
          "objects": [
            {
              "schemaName": "dbo",
              "objectName": "Users",
              "objectType": "TABLE",
              "objectDescription": "User table",
              "columns": [
                {
                  "columnName": "Id",
                  "columnType": "int",
                  "isNullable": "NO",
                  "columnDefault": "",
                  "isPrimaryKey": "YES",
                  "isIdentity": "YES",
                  "columnDescription": "",
                  "columnOrder": 1
                }
              ],
              "indexes": []
            }
          ],
          "routines": []
        }
        """;

        string actualJson = JsonSerializer.Serialize(snapshot, SchemaArtifactJsonSerializerOptions.Default);

        AssertJson(actualJson, expectedJson);
    }

    [Test]
    public void Serialize_WhenDiffIsProvided_UsesStableCamelCaseContractAndStringEnums() {
        SchemaDiffDocument diff = new() {
            SchemaVersion = 2,
            GeneratedAt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            LeftSnapshotPath = "C:/Exports/baseline.snapshot.json",
            RightSnapshotPath = "C:/Exports/current.snapshot.json",
            Summary = new SchemaDiffSummary {
                ModifiedObjects = 1,
                AddedColumns = 1
            },
            ObjectChanges = [
                new SchemaDiffEntry {
                    ChangeType = SchemaChangeType.Modified,
                    Identifier = "dbo.Users (TABLE)",
                    PropertyChanges = new Dictionary<string, SchemaValueChange> {
                        ["ObjectDescription"] = new SchemaValueChange {
                            Previous = "Previous user table",
                            Current = "Current user table"
                        }
                    }
                }
            ],
            ColumnChanges = [
                new SchemaDiffEntry {
                    ChangeType = SchemaChangeType.Added,
                    Identifier = "dbo.Users.Name (TABLE)",
                    PropertyChanges = new Dictionary<string, SchemaValueChange> {
                        ["ColumnType"] = new SchemaValueChange {
                            Current = "nvarchar(128)"
                        }
                    }
                }
            ],
            IndexChanges = [],
            RoutineChanges = []
        };
        string expectedJson = """
        {
          "schemaVersion": 2,
          "generatedAt": "2024-01-01T00:00:00+00:00",
          "leftSnapshotPath": "C:/Exports/baseline.snapshot.json",
          "rightSnapshotPath": "C:/Exports/current.snapshot.json",
          "summary": {
            "addedObjects": 0,
            "removedObjects": 0,
            "modifiedObjects": 1,
            "addedColumns": 1,
            "removedColumns": 0,
            "modifiedColumns": 0,
            "addedIndexes": 0,
            "removedIndexes": 0,
            "modifiedIndexes": 0,
            "addedRoutines": 0,
            "removedRoutines": 0,
            "modifiedRoutines": 0
          },
          "objectChanges": [
            {
              "changeType": "Modified",
              "identifier": "dbo.Users (TABLE)",
              "propertyChanges": {
                "ObjectDescription": {
                  "previous": "Previous user table",
                  "current": "Current user table"
                }
              }
            }
          ],
          "columnChanges": [
            {
              "changeType": "Added",
              "identifier": "dbo.Users.Name (TABLE)",
              "propertyChanges": {
                "ColumnType": {
                  "previous": null,
                  "current": "nvarchar(128)"
                }
              }
            }
          ],
          "indexChanges": [],
          "routineChanges": []
        }
        """;

        string actualJson = JsonSerializer.Serialize(diff, SchemaArtifactJsonSerializerOptions.Default);

        AssertJson(actualJson, expectedJson);
    }

    private static void AssertJson(string actualJson, string expectedJson) {
        Assert.That(NormalizeLineEndings(actualJson), Is.EqualTo(NormalizeLineEndings(expectedJson)));
    }

    private static string NormalizeLineEndings(string value) {
        return value.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
    }
}
