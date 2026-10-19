using CloudyWing.SchemaExporter.Core.Exporting;
using CloudyWing.SchemaExporter.Core.Exporting.Snapshots;

namespace CloudyWing.SchemaExporter.Core.Tests.Exporting;

[TestFixture]
public sealed class SchemaSummaryMarkdownBuilderTests {
    [Test]
    public void BuildMarkdown_WhenSnapshotContainsRoutineDefinition_OmitsDefinitionBody() {
        SchemaSnapshotDocument snapshot = SchemaTestData.CreateSnapshotDocument(@"C:\Exports\TableSchema.xlsx");

        string result = SchemaSummaryMarkdownBuilder.BuildMarkdown(snapshot, null);

        using (Assert.EnterMultipleScope()) {
            Assert.That(result, Does.Contain("# Schema Summary"));
            Assert.That(result, Does.Contain("dbo.Users"));
            Assert.That(result, Does.Contain("@IsActive bit"));
            Assert.That(result, Does.Contain("Routine definitions are omitted"));
            Assert.That(result, Does.Not.Contain("SELECT [Id], [Name] FROM [dbo].[Users];"));
        }
    }

    [Test]
    public void BuildRoutineMarkdown_WhenRoutineContainsDefinition_OmitsDefinitionBody() {
        SchemaSnapshotDocument snapshot = SchemaTestData.CreateSnapshotDocument(@"C:\Exports\TableSchema.xlsx");
        SchemaSnapshotRoutineDocument routine = snapshot.Routines.Single();

        string result = SchemaSummaryMarkdownBuilder.BuildRoutineMarkdown(routine);

        using (Assert.EnterMultipleScope()) {
            Assert.That(result, Does.Contain("- Type: PROCEDURE"));
            Assert.That(result, Does.Contain("- Parameters: @IsActive bit"));
            Assert.That(result, Does.Contain("- Description: Returns users"));
            Assert.That(result, Does.Not.Contain("## Definition"));
            Assert.That(result, Does.Not.Contain("SELECT [Id], [Name] FROM [dbo].[Users];"));
        }
    }

    [Test]
    public void BuildMarkdown_WhenProviderCapabilitiesExist_IncludesCapabilitySection() {
        SchemaSnapshotDocument snapshot = SchemaTestData.CreateSnapshotDocument(@"C:\Exports\TableSchema.xlsx");

        string result = SchemaSummaryMarkdownBuilder.BuildMarkdown(snapshot, null);

        using (Assert.EnterMultipleScope()) {
            Assert.That(result, Does.Contain("## Provider Capabilities"));
            Assert.That(result, Does.Contain("Tables"));
            Assert.That(result, Does.Contain("Views"));
            Assert.That(result, Does.Contain("Partial"));
        }
    }

    [Test]
    public void BuildMarkdown_WhenRelationshipsExist_IncludesRelationshipSectionAndIncludedColumns() {
        SchemaSnapshotDocument snapshot = SchemaTestData.CreateSnapshotDocument(@"C:\Exports\TableSchema.xlsx");

        string result = SchemaSummaryMarkdownBuilder.BuildMarkdown(snapshot, null);

        using (Assert.EnterMultipleScope()) {
            Assert.That(result, Does.Contain("| Relationships | 1 |"));
            Assert.That(result, Does.Contain("## Relationships"));
            Assert.That(result, Does.Contain("| Constraint | Source Table | Source Column | Referenced Table | Referenced Column |"));
            Assert.That(result, Does.Contain("FK_Users_Customers"));
            Assert.That(result, Does.Contain("dbo.Customers"));
            Assert.That(result, Does.Not.Contain("IsForeignKey"));
            Assert.That(result, Does.Not.Contain("OtherColumns"));
        }
    }

    [Test]
    public void BuildMarkdown_WhenRelationshipsAreEmptyOrIncomplete_UsesNoRelationshipsMessage() {
        SchemaSnapshotDocument snapshot = SchemaTestData.CreateSnapshotDocument(
            @"C:\Exports\TableSchema.xlsx",
            relationshipOverride: [
                new SchemaSnapshotRelationshipDocument {
                    ForeignKeyName = "FK_Incomplete",
                    SourceSchemaName = "dbo",
                    SourceTableName = "Users",
                    SourceColumnName = "",
                    ReferencedSchemaName = "",
                    ReferencedTableName = "Customers",
                    ReferencedColumnName = "Id",
                    ColumnOrder = 1
                }
            ]
        );

        string result = SchemaSummaryMarkdownBuilder.BuildMarkdown(snapshot, null);

        using (Assert.EnterMultipleScope()) {
            Assert.That(result, Does.Contain("## Relationships"));
            Assert.That(result, Does.Contain("No relationships found."));
            Assert.That(result, Does.Not.Contain("FK_Incomplete"));
            Assert.That(result, Does.Not.Contain("|  |  |  |  |"));
        }
    }

    [Test]
    public void BuildMarkdown_WhenRelationshipCatalogPermissionIsDenied_UsesUnavailableMessage() {
        const string diagnosticMessage = "FK catalog permission denied; relationships were not retrieved.";
        SchemaSnapshotDocument snapshot = SchemaTestData.CreateSnapshotDocument(
            @"C:\Exports\TableSchema.xlsx",
            relationshipOverride: [],
            diagnosticOverride: [
                new SchemaSnapshotDiagnostic {
                    Severity = "警告",
                    Category = "Execution",
                    Message = diagnosticMessage
                }
            ]
        );

        string result = SchemaSummaryMarkdownBuilder.BuildMarkdown(snapshot, null);

        using (Assert.EnterMultipleScope()) {
            Assert.That(result, Does.Contain("Relationships unavailable: FK catalog permission denied."));
            Assert.That(result, Does.Contain(diagnosticMessage));
            Assert.That(result, Does.Not.Contain("No relationships found."));
        }
    }
}
