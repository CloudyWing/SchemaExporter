using CloudyWing.SchemaExporter.Core.Exporting;
using CloudyWing.SchemaExporter.Core.SchemaProviders;

namespace CloudyWing.SchemaExporter.Core.Tests.Exporting;

[TestFixture]
public sealed class SchemaRedactorTests {
    [Test]
    public void Apply_WhenRedactionIsDisabled_ReturnsOriginalExport() {
        FilteredSchemaExport schemaExport = CreateFilteredExport();
        List<ExportDiagnostic> diagnostics = [];

        FilteredSchemaExport result = SchemaRedactor.Apply(schemaExport, new SchemaRedactionOptions(), diagnostics);

        using (Assert.EnterMultipleScope()) {
            Assert.That(result, Is.SameAs(schemaExport));
            Assert.That(diagnostics, Is.Empty);
        }
    }

    [Test]
    public void Apply_WhenSensitiveColumnNameMatches_RedactsColumnMetadata() {
        FilteredSchemaExport schemaExport = CreateFilteredExport();
        List<ExportDiagnostic> diagnostics = [];
        SchemaRedactionOptions options = new() {
            Enabled = true,
            ReplacementText = "[MASKED]",
            SensitiveNamePatterns = ["password"],
            SensitiveTextPatterns = []
        };

        FilteredSchemaExport result = SchemaRedactor.Apply(schemaExport, options, diagnostics);

        DatabaseColumnSchema passwordColumn = result.Columns.Single(x => x.ColumnName == "PasswordHash");
        DatabaseColumnSchema displayNameColumn = result.Columns.Single(x => x.ColumnName == "DisplayName");
        using (Assert.EnterMultipleScope()) {
            Assert.That(passwordColumn.ColumnDefault, Is.EqualTo("[MASKED]"));
            Assert.That(passwordColumn.ColumnDescription, Is.EqualTo("[MASKED]"));
            Assert.That(displayNameColumn.ColumnDescription, Is.EqualTo("Public display name"));
            Assert.That(diagnostics.Single().Category, Is.EqualTo(ExportDiagnosticCategory.Redaction));
        }
    }

    [Test]
    public void Apply_WhenSensitiveTextMatches_RedactsMatchingText() {
        FilteredSchemaExport schemaExport = CreateFilteredExport();
        List<ExportDiagnostic> diagnostics = [];
        SchemaRedactionOptions options = new() {
            Enabled = true,
            ReplacementText = "[MASKED]",
            SensitiveNamePatterns = [],
            SensitiveTextPatterns = ["AKIA[0-9A-Z]+"]
        };

        FilteredSchemaExport result = SchemaRedactor.Apply(schemaExport, options, diagnostics);

        DatabaseObjectSchema databaseObject = result.Objects.Single();
        DatabaseRoutineSchema routine = result.Routines.Single();
        using (Assert.EnterMultipleScope()) {
            Assert.That(databaseObject.ObjectDescription, Is.EqualTo("Uses [MASKED] for external integration."));
            Assert.That(routine.RoutineDefinition, Is.EqualTo("SELECT '[MASKED]';"));
            Assert.That(diagnostics.Single().Message, Does.Contain("2"));
        }
    }

    [Test]
    public void Apply_WhenPatternIsInvalid_ThrowsValidationException() {
        FilteredSchemaExport schemaExport = CreateFilteredExport();
        List<ExportDiagnostic> diagnostics = [];
        SchemaRedactionOptions options = new() {
            Enabled = true,
            SensitiveNamePatterns = ["["]
        };

        ExportValidationException? exception = Assert.Throws<ExportValidationException>(
            () => SchemaRedactor.Apply(schemaExport, options, diagnostics)
        );

        Assert.That(exception?.Message, Does.Contain("SensitiveNamePatterns"));
    }

    [Test]
    public void Apply_WhenPatternListIsNull_ThrowsValidationException() {
        FilteredSchemaExport schemaExport = CreateFilteredExport();
        List<ExportDiagnostic> diagnostics = [];
        SchemaRedactionOptions options = new() {
            Enabled = true,
            SensitiveNamePatterns = null!
        };

        ExportValidationException? exception = Assert.Throws<ExportValidationException>(
            () => SchemaRedactor.Apply(schemaExport, options, diagnostics)
        );

        Assert.That(exception?.Message, Does.Contain("SensitiveNamePatterns"));
    }

    private static FilteredSchemaExport CreateFilteredExport() {
        return new FilteredSchemaExport {
            Objects = [
                new DatabaseObjectSchema {
                    SchemaName = "dbo",
                    ObjectName = "Users",
                    ObjectType = "TABLE",
                    ObjectDescription = "Uses AKIA1234567890ABCDEF for external integration."
                }
            ],
            Columns = [
                new DatabaseColumnSchema {
                    SchemaName = "dbo",
                    ObjectName = "Users",
                    ObjectType = "TABLE",
                    ColumnName = "PasswordHash",
                    ColumnType = "nvarchar(256)",
                    IsNullable = "NO",
                    ColumnDefault = "('not-a-real-password')",
                    IsPrimaryKey = "NO",
                    IsIdentity = "NO",
                    ColumnDescription = "Password hash",
                    ColumnOrder = 1
                },
                new DatabaseColumnSchema {
                    SchemaName = "dbo",
                    ObjectName = "Users",
                    ObjectType = "TABLE",
                    ColumnName = "DisplayName",
                    ColumnType = "nvarchar(128)",
                    IsNullable = "NO",
                    IsPrimaryKey = "NO",
                    IsIdentity = "NO",
                    ColumnDescription = "Public display name",
                    ColumnOrder = 2
                }
            ],
            Indexes = [],
            Routines = [
                new DatabaseRoutineSchema {
                    SchemaName = "dbo",
                    RoutineName = "usp_GetSecret",
                    RoutineType = "PROCEDURE",
                    ParameterSignature = "@Id int",
                    RoutineDescription = "Returns secret value",
                    RoutineDefinition = "SELECT 'AKIA1234567890ABCDEF';"
                }
            ]
        };
    }
}
