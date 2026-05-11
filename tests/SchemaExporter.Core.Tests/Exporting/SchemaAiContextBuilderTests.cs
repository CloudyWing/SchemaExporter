using CloudyWing.SchemaExporter.Core.Exporting;
using CloudyWing.SchemaExporter.Core.Exporting.Snapshots;

namespace CloudyWing.SchemaExporter.Core.Tests.Exporting;

[TestFixture]
public sealed class SchemaAiContextBuilderTests {
    [Test]
    public void BuildMarkdown_WhenSnapshotContainsRoutineDefinition_OmitsDefinitionBody() {
        SchemaSnapshotDocument snapshot = SchemaTestData.CreateSnapshotDocument(@"C:\Exports\TableSchema.xlsx");

        string result = SchemaAiContextBuilder.BuildMarkdown(snapshot, null);

        using (Assert.EnterMultipleScope()) {
            Assert.That(result, Does.Contain("# Schema Context"));
            Assert.That(result, Does.Contain("dbo.Users"));
            Assert.That(result, Does.Contain("@IsActive bit"));
            Assert.That(result, Does.Contain("Routine definitions are omitted"));
            Assert.That(result, Does.Not.Contain("SELECT [Id], [Name] FROM [dbo].[Users];"));
        }
    }

    [Test]
    public void BuildMarkdown_WhenProviderCapabilitiesExist_IncludesCapabilitySection() {
        SchemaSnapshotDocument snapshot = SchemaTestData.CreateSnapshotDocument(@"C:\Exports\TableSchema.xlsx");

        string result = SchemaAiContextBuilder.BuildMarkdown(snapshot, null);

        using (Assert.EnterMultipleScope()) {
            Assert.That(result, Does.Contain("## Provider Capabilities"));
            Assert.That(result, Does.Contain("Tables"));
            Assert.That(result, Does.Contain("Views"));
            Assert.That(result, Does.Contain("Partial"));
        }
    }
}
